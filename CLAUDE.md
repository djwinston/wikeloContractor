# Wikelo Contractor

Windows companion app for Wikelo trades in Star Citizen. Two modes: contract **Catalog**
(data from Star Citizen Wiki API) and personal **Inventory** with an in-game overlay.
The roadmap lives in **PLAN.md** — work through it phase by phase, check items off as completed.

## Stack (decided, do not change without asking)

- **.NET 10**, `net10.0-windows`, WPF, C# (user's SDK: 10.0.301)
- **WPF-UI (lepoco) 4.x** + `WPF-UI.DependencyInjection` — FluentWindow, NavigationView, Mica
- **CommunityToolkit.Mvvm** — `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`
- **Microsoft.Extensions.Hosting** — Generic Host, DI, `IHostedService` startup
- Data source: `https://api.star-citizen.wiki` (public, no auth), swagger at `https://docs.star-citizen.wiki`
- Persistence: JSON files in `%AppData%\WikeloContractor\` (settings.json, inventory.json, cache/)
- No cloud/auth for now (optional Supabase + Discord OAuth is Phase 6)
- Package versions are pinned exactly in both csproj files (no floating `*`). `Velopack` is kept in
  lockstep with the `vpk` CLI version in `.github/workflows/release.yml` — bump the two together.

## Environment

- User is on Windows 11, shell: **PowerShell 7** — use PowerShell syntax in instructions
- Editor: **VS Code** (no Visual Studio). `.vscode/` has tasks (build/run/watch) and launch config
- Repo root: `D:\dev\own_repo\starCitizen\wikeloContractor`
- Build: `dotnet build src/WikeloContractor.csproj`
- Run: `dotnet run --project src/WikeloContractor.csproj`
- Test: `dotnet test tests/WikeloContractor.Tests.csproj` (xUnit)

## Docs map — read before working on an area

- `docs/data-pipeline.md` — before touching `src/Services/` or `src/Models/Api/`
  (caching, version invalidation, enrichment, rate limiting, service events, and the
  **sync-state** axis: freshness `CatalogStatus` and completeness `CatalogSyncState` are
  orthogonal — never merge them into one enum)
- `docs/api-item-fields.md` — full field inventory of item/vehicle detail responses;
  consult before extending `RewardDetails` or the contract detail view
- `docs/reward-images.md` / `docs/inventory-images.md` — which reward items and which required
  items still need a manual image in the matching `Resources/img-*-overrides.json`
- `docs/design-system.md` — **before touching any styling**: `src/Views/` or `src/Resources/`.
  The rule is "the WPF-UI Fluent theme is the token layer" — never re-declare a theme brush, never
  hardcode a hex. Covers the theme-swapped brand palette, embedded fonts, geometry scale, chips,
  icon mapping, and which design file is the source of truth for each screen
- `docs/ui-notes.md` — before touching `src/Views/` or `src/ViewModels/`
  (WPF-UI pitfalls, status InfoBar pattern, adaptive icon, formatted localized strings)
- `docs/testing.md` — before adding/changing tests in `tests/`
- `.claude/skills/api-explore/SKILL.md` — before exploring new API endpoints (known facts inside)

## Verification workflow

- After code changes: build and smoke-run the app (launch exe, ~8 s, confirm alive, stop).
  Do **not** ask the user whether to run tests — tests run via the `/verify` command,
  which also fixes/updates failing tests and reviews docs (`.claude/commands/verify.md`).

## Architecture & conventions

- MVVM. Pages implement `INavigableView<TViewModel>`, set `DataContext = this`,
  XAML bindings go through `ViewModel.*`. ViewModels inherit `ViewModels/ViewModel`
  (ObservableObject + INavigationAware).
- Everything is registered in `App.xaml.cs` Generic Host. Pages/VMs are singletons.
  Startup flow lives in `Services/ApplicationHostService` (load settings → apply language
  → apply theme → show MainWindow → navigate to CatalogPage).
- Navigation: WPF-UI `INavigationService` + `AddNavigationViewPageProvider()`;
  nav items are declared in `Views/MainWindow.xaml`.
- **Localization**: en (default) + uk. All UI strings via `{DynamicResource Key}` from
  `Resources/Localization/Strings.en.xaml` / `Strings.uk.xaml`. `LocalizationService`
  swaps the merged dictionary at runtime. Never hardcode UI strings in XAML/C#;
  always add keys to BOTH dictionaries. API data (item/ship names) stays English.
- Settings: `ISettingsService` (JSON, `%AppData%\WikeloContractor\settings.json`).
  Theme: System/Light/Dark via `ApplicationThemeManager` (see `ApplicationHostService.ApplyTheme`).
- File-scoped namespaces, `_camelCase` private fields, nullable enabled.
- **Reuse before writing new code** — check the shared helpers first; adding a second copy
  of one of these is a review finding. Current homes:
  - `Services/AppStorage` — `%AppData%` root, subdirectories, shared `JsonSerializerOptions`
  - `Services/AppHttp` — the User-Agent constant for every outgoing HttpClient
  - `ViewModels/Localized` — code-side localized strings: `Localized.String(key)` /
    `Localized.Format(key, args)` (XAML uses `{DynamicResource}` directly)
  - `Models/ContractRequirement.FormatRange` — min–max display rule ("2", "1–3";
    max-only is the API's fixed amount and renders plain "N"), invariant culture
  - `Models/ContractCategoryDisplay.LabelKey`, `Models/ComponentTypeDisplay.LabelKey`,
    `Models/ReputationTierDisplay.LabelKey` — the `XDisplay.LabelKey(value)` pattern: an
    enum/API-string → localization-key mapping lives as a static class next to the type it maps,
    one per type needing this, not inline switches or string interpolation into a resource key
    (`$"Prefix_{apiValue}"` breaks silently on an unmapped value with no compile-time check)
  - `Services/CompletionService` — the completed-contracts + accumulated Wikelo reputation store
    (`completed.json`, UUID→earned reputation). New per-service JSON stores follow this shape:
    `AppStorage.Root`/`JsonOptions`, load-with-`try/catch(JsonException)`, atomic tmp+`File.Move`
    write (same as `SettingsService`/`ContractCatalogService`). `Services/InventoryStore`
    (`inventory.json`, name→count) is the second store on this shape, `Services/FavoritesService`
    (`favorites.json`, UUID set) the third
  - `ViewModels/ContractListViewModel` — the base for **any page showing a filterable contract
    list**: the cards, the `ICollectionView` over them, the search/category/resource filters, the
    empty state, `OpenDetails`, and the fan-out of the completion/favorites/inventory/sync `Changed`
    events onto every card. `CatalogViewModel` (everything) and `FavoritesViewModel` (starred only)
    differ solely in `RebuildFromCatalog`. A third list page subclasses this — it does not re-filter
  - `ViewModels/RequirementListViewModel` — the item-grid analogue: the base for **any page showing
    the catalog's required items as a category-grouped grid** (the distinct-item projection, the
    grouped `ICollectionView`, the search + category filter, the empty state, the image-preview
    overlay). Rows implement `IRequirementItem` (`Name`/`Category`/`CategoryLabel`).
    `InventoryViewModel` (adds a count store) and `SourcingViewModel` (adds a sourcing note + detail
    nav) override only `CreateItem` (and Sourcing widens `MatchesSearch`). A third item-grid page
    subclasses this — it does not re-implement the grouping/filter/preview
  - `Models/ContractFilter` — the pure search/category/resource matching decision
    (`Matches(contract)`), deliberately free of UI notions so it is testable without a WPF
    `Application`; the VM maps combo box indices onto it. A second copy of this is a review finding
  - `Resources/ContractCard.xaml` — the catalog row `DataTemplate` (`ContractCardTemplate`), shared
    verbatim by `CatalogPage` and `FavoritesPage`. Merged **after** `Chips.xaml` in `App.xaml`
    because it resolves the chip styles via `StaticResource`, which only looks backwards
  - `Resources/ItemThumb.xaml` — the 46 × 46 item thumb (`ItemThumbTemplate`: artwork with a
    category-icon fallback, click-to-preview), shared by `InventoryPage` and `SourcingPage`. Its
    DataContext needs a `Name` + `Category`, and the hosting page must expose `OpenPreviewCommand`
  - `Services/SourcingGuideService` — the "where to find it" knowledge base: one Markdown file per
    item in `docs/sourcing/`, shipped into `Resources/sourcing/` and layered with
    `%AppData%\WikeloContractor\sourcing\`. Two-layer like the override services but resolved **per
    file**, so it scans directories rather than reusing `OverrideFileSet` (a key→value JSON engine).
    The front matter's `name` is the key, never the file name. Format + rules: `docs/sourcing/README.md`
  - `Models/MarkdownDocument` — the only Markdown parser: a small, **total** subset (headings,
    bullets, ordered steps, inline bold/italic/code/link, `<!-- -->` stripping). Pure, so it is
    unit-tested without WPF; malformed input degrades to plain text and never throws.
    `Views/Controls/MarkdownViewer` is the matching renderer (`TextBlock`s over design tokens — not a
    `FlowDocument`, which WPF-UI does not theme). Do not add a second Markdown implementation
  - `Services/OverrideFileSet` — the reusable two-layer (bundled + `%AppData%`) key→value override
    engine with throttled hot-reload and a first-run user template. `ImageOverrideService` (reward
    images) and `InventoryImageOverrideService` (inventory item images, `img-inventory-overrides.json`)
    both delegate to it — a new user-editable override config wraps this, it does not re-implement it.
    It also handles one-time adoption of a pre-rename `%AppData%` user file (`legacyUserFilePath`),
    so renaming an override config carries the user's edits over instead of orphaning them — reuse
    that rather than hand-rolling a migration
  - `Models/InventoryCategoryClassifier.Classify(name, hasScu)` — the single home for the required-item
    → `InventoryCategory` mapping (ordered keyword rules, first match wins; unit-tested).
    `Models/InventoryCategoryDisplay.LabelKey` is its `XDisplay.LabelKey` companion
  - `Models/ReputationLevels` — Wikelo rank thresholds (New 0 / Very Good 340 / Very Best 999,
    not in the API) + `Compute(total)`; the single home for the tier math, unit-tested
  - `ViewModels/ContractCardViewModel` — the per-catalog-card wrapper over a `WikeloContract`
    holding observable completion state and the inventory-readiness state (colored requirement
    chips, `IsReady`, `ReadinessLabel`). `ViewModels/ReputationSummary` — display-ready reputation
    standing for the bar
  - `Models/InventoryReadiness` — the requirement-vs-inventory readiness math (`RequiredAmount`,
    `RequiredCount` for deduction, `Availability` → `RequirementAvailability`), unit-tested; the single
    home for that decision. `ViewModels/RequirementChip` is the display wrapper (name/amount/availability)
    both the catalog card and `ContractDetailViewModel` build; `Views/Converters/AvailabilityToBrushConverter`
    colors it
  - `ViewModels/ContractCompletionInteraction` — the single home for the complete/reopen flow: confirms
    and deducts inventory on completion, warns (no restore) on reopen. Both the catalog card and detail
    page call it; completion is gated on `IsReady`. Uses `Wpf.Ui.Controls.MessageBox` (aliased to avoid
    the `System.Windows.MessageBox` clash), so no dialog-host wiring is needed
  - `ViewModels/ContractDetailViewModel.RewardDisplay` — reward stat/loadout chip composition
    (`ComposeStats`/`ComposeWeapons`/`ComposeComponents`/`FormatEntry`/`JoinNonEmpty`). Lives in
    the VM layer, not `Models/`, because it needs `Localized` (actual localized strings, not
    just a key) — see `docs/ui-notes.md` "Contract detail page"
  - `Resources/Theme/Brand.{Dark,Light}.xaml` — the app-specific colours Fluent does not provide
    (chip tints, blueprint role, XP badge, completed-row wash). Identical key sets, swapped as a
    pair by `ApplicationHostService.ApplyTheme`; a key added to one MUST be added to the other —
    with exactly one documented exception, the three light-theme legibility overrides that live
    only in `Brand.Light.xaml` (see `docs/design-system.md`, "The one exception"). Do not add a
    fourth override without measuring a real screenshot first.
    `Resources/Typography.xaml` (fonts, type ramp, `OverlineTextStyle`/`MonoCaptionStyle`),
    `Resources/Metrics.xaml` (radii/spacing/sizes) and `Resources/Chips.xaml` are the
    theme-independent companions — see `docs/design-system.md`
  - `Resources/Chips.xaml` — **anything two pages render the same way**: chrome styles
    (`ChipStyle`, `BlueprintChipStyle`, `TagStyle`, `ReadinessBarStyle`) plus whole shared templates
    (`RequirementChipTemplate`, `ChipWrapPanel`) and the `StatusBadge` default style. Re-declaring
    one of these in a page's `Page.Resources` is a review finding — that is exactly the drift this
    dictionary exists to stop
  - `Views/Controls/StatusBadge` — the COMPLETED / READY badge (icon + label). A control, not
    repeated markup; `Role` (`Success`/`Caution`) picks the whole brush set so the three brushes
    cannot be mismatched. A new status marker adds a `Role`, it doesn't hand-roll a `Border`
  - `Views/Converters/` — one parameterized converter per concern
    (e.g. `PresenceToVisibilityConverter` with `Invert`), not inverse-twin classes
  - `Views/Pages/ContractDetailPage.xaml` `ChipListStyle` resource — the reward card's
    Weapons/Components chip lists; page-local because only that page has them
  - `tests/Services/StubHandler` — shared HTTP stub for client tests
  - `tests/E2E/ScriptedWikiApi` — the **only** `IStarCitizenWikiClient` fake, used by both the
    service tests and the E2E scenarios (version bump, held enrichment, 429, offline). A second
    fake for this interface is a review finding. `tests/E2E/WpfAppFixture` is the single real
    WPF `Application` (STA, one per process) and `tests/E2E/CatalogHarness` assembles the app
    graph over a temp directory — see `docs/testing.md` for the deadlock and dialog traps
- **Don't repeat a condition across sibling properties/branches** — hoist it to one local
  or computed value first (e.g. a `showX` bool, an `EffectiveX` property on the model) and
  reference that everywhere, instead of writing the same ternary/guard two or three times.
- **Language policy**: all code comments, XML docs, and repo documentation are in **English**.
  Ukrainian is used only in conversation with the user. Localization resources
  (`Strings.uk.xaml`) and displayed UI values are data, not comments — keep them as is.

## Overlay notes (Phase 4)

- Separate borderless `Topmost` window; global hotkey via user32 `RegisterHotKey`;
  click-through = toggling `WS_EX_TRANSPARENT` via `SetWindowLong`.
- Overlays render above Star Citizen even in its "Fullscreen" mode on Windows 11
  (DWM fullscreen optimizations); confirmed by the SCLOC-Verse community app.

## Distribution & updates (Phase 5)

- Packaged with **Velopack** (installer + auto-update); GitHub Releases is the update feed.
  `VelopackApp.Build().Run()` is the **first line** of `App.OnStartup` (handles install/update
  hooks) — do not move it after the host build. `Services/AppUpdateService` wraps `UpdateManager`
  and is a no-op when `UpdateManager.IsInstalled` is false (dev runs), driving the Settings
  "Check for updates" row. Release build is **framework-dependent**; the installer bootstraps the
  .NET Desktop Runtime via `vpk pack --framework net10.0-x64-desktop`.
- **Releases are portable-only until the app is code-signed.** `release.yml` publishes just
  `WikeloContractor-win-Portable.zip` (+ `SHA256SUMS.txt` + a build-provenance attestation). An
  unsigned `Setup.exe` is a self-extracting installer that hardened Windows (Smart App Control / ASR)
  hard-blocks, and auto-update is a no-op while unsigned anyway — so shipping installers now would
  only create dead-end first installs. `vpk pack` still *produces* `Setup.exe`; a **Remove installer
  assets** step unpublishes it (and any `.msi`) after upload. The Velopack update metadata
  (`*.nupkg`/`RELEASES`) is left on the release, harmless with no installer.
- **Re-enable the installers as one change when signing lands** (see the distribution-signing memory):
  add back `--msi`, `--instLocation`, `Either` to `vpk pack`, delete the *Remove installer assets*
  step, restore the `*.exe`/`*.msi` patterns in the checksum + attestation steps, and add the SignPath
  signing step. What the installers give when re-enabled (already verified, keep for that day):
  - `Setup.exe` — one-click, no prompts, installs to `%LocalAppData%\WikeloContractor`; near-instant
    (payload ~6.5 MB) so the progress bar barely shows — expected, not a bug.
  - `.msi` — a real WiX wizard: `InstallScopeDlg` (per-user vs per-machine `Program Files`),
    `BrowseDlg`/`WIXUI_INSTALLDIR` for an **arbitrary install folder**, and a `ProgressDlg`; plus
    Add/Remove Programs, repair/remove, and Group Policy deploy. WiX ships inside `vpk` (no separate
    install on the runner). Verified from the generated MSI's Dialog/Property tables.
  Auto-updates apply the same way regardless of installer — the installer is only the initial
  bootstrap, not part of the update path; and portable builds do not auto-update (`IsInstalled` false).
- Keep `Resources/img-catalog-overrides.json` as loose `<Content>` (do **not** embed it): it ships in the
  install dir as the editable bundled-defaults layer. It is replaced on each Velopack update, so
  persistent personal edits belong in the `%AppData%` override file, which updates never touch.
- CI/release live in `.github/workflows/`; merge gating (tests must pass, approvals) is configured
  in GitHub **Rulesets**, not in the YAML. The CI job is named `build-and-test` — keep that name
  stable, the rulesets reference it.

## API notes (Phase 1)

- Explore swagger first; mission pages exist like `api.star-citizen.wiki/missions/wikelo-arrive-to-system`,
  list endpoints support `?filter[...]` query params.
- Reference implementation of the same idea (web): https://wikelotrades.com
- Cache API responses to disk; the app must remain usable offline.
- Be a polite API citizen: cache aggressively, no polling.
