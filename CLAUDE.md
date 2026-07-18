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

TODO after first successful `dotnet restore`: pin exact package versions in the csproj
(currently floating `4.*` / `8.*` / `10.*`).

## Environment

- User is on Windows 11, shell: **PowerShell 7** — use PowerShell syntax in instructions
- Editor: **VS Code** (no Visual Studio). `.vscode/` has tasks (build/run/watch) and launch config
- Repo root: `D:\dev\own_repo\starCitizen\wikeloContractor`
- Build: `dotnet build src/WikeloContractor.csproj`
- Run: `dotnet run --project src/WikeloContractor.csproj`
- Test: `dotnet test tests/WikeloContractor.Tests.csproj` (xUnit)

## Docs map — read before working on an area

- `docs/data-pipeline.md` — before touching `src/Services/` or `src/Models/Api/`
  (caching, version invalidation, enrichment, rate limiting, service events)
- `docs/api-item-fields.md` — full field inventory of item/vehicle detail responses;
  consult before extending `RewardDetails` or the contract detail view
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
  - `Models/ContractCategoryDisplay.LabelKey`, `Models/ComponentTypeDisplay.LabelKey` — the
    `XDisplay.LabelKey(value)` pattern: an enum/API-string → localization-key mapping lives as
    a static class next to the type it maps, one per type needing this, not inline switches
    or string interpolation into a resource key (`$"Prefix_{apiValue}"` breaks silently on an
    unmapped value with no compile-time check)
  - `ViewModels/ContractDetailViewModel.RewardDisplay` — reward stat/loadout chip composition
    (`ComposeStats`/`ComposeWeapons`/`ComposeComponents`/`FormatEntry`/`JoinNonEmpty`). Lives in
    the VM layer, not `Models/`, because it needs `Localized` (actual localized strings, not
    just a key) — see `docs/ui-notes.md` "Contract detail page"
  - `Views/Converters/` — one parameterized converter per concern
    (e.g. `PresenceToVisibilityConverter` with `Invert`), not inverse-twin classes
  - `Views/Pages/ContractDetailPage.xaml` `ChipListStyle` resource — the WrapPanel/Border/
    TextBlock chip look shared by every reward chip list (Stats/Weapons/Components); a new
    chip list applies this `Style`, it doesn't redefine the template
  - `tests/Services/StubHandler` — shared HTTP stub for client tests
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

## API notes (Phase 1)

- Explore swagger first; mission pages exist like `api.star-citizen.wiki/missions/wikelo-arrive-to-system`,
  list endpoints support `?filter[...]` query params.
- Reference implementation of the same idea (web): https://wikelotrades.com
- Cache API responses to disk; the app must remain usable offline.
- Be a polite API citizen: cache aggressively, no polling.
