# Wikelo Contractor — project plan

Windows companion app for Wikelo trades in Star Citizen.
Two modes: Wikelo contract **Catalog** and **Personal inventory** with an in-game overlay.

## Stack (locked in)

| Layer | Technology |
|---|---|
| Platform | Windows, .NET 10 (`net10.0-windows`), C#, WPF |
| UI | WPF-UI (lepoco) 4.x — FluentWindow, NavigationView, Mica, Fluent icons |
| MVVM | CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) |
| DI | Microsoft.Extensions.Hosting (Generic Host) |
| API | `api.star-citizen.wiki` (public, no auth), swagger: `docs.star-citizen.wiki` |
| Data | Local: JSON in `%AppData%\WikeloContractor\` + API cache |
| Overlay | Topmost window, global hotkey (`RegisterHotKey`), click-through toggle |
| UI languages | English (default) + Ukrainian. API data stays in English, not translated |

Reference (what already exists): https://wikelotrades.com , community Excel spreadsheet (outdated).

---

## Phase 0 — Scaffold ✅ (this archive)

- [x] Solution + project, WPF-UI 4.x, DI via Generic Host
- [x] FluentWindow: TitleBar, NavigationView (Catalog / Inventory / Settings)
- [x] Empty placeholder pages
- [x] Settings: language (en/uk, runtime switching via ResourceDictionary) and theme (System/Light/Dark), persisted to `settings.json`
- [x] VS Code: tasks, launch, extensions, settings
- [ ] **After the first `dotnet restore`: pin exact package versions in the csproj** (currently floating `4.*` / `8.*` / `10.*`)
- [x] App icon (`.ico`) — `<ApplicationIcon>` in the csproj + `ui:TitleBar` icon (`src/Assets/`)

## Phase 1 — API client

- [x] Explore swagger at `docs.star-citizen.wiki`: Wikelo missions come from
      `GET /api/missions?filter[mission_giver]=Wikelo&page[size]=200` (~88 entries, one page;
      the earlier `reputation_scope` filter missed no-reputation top-rank trades like the Idris);
      requirements are in `hauling_summary`, rewards only in `GET /api/missions/{uuid}` (`reward_items`)
- [x] `Services/Api/StarCitizenWikiClient` via `IHttpClientFactory` + `System.Text.Json`
- [x] Models: `WikeloContract` (title, requirements: item+quantity, reputation, game version);
      rewards will be added with the contract detail view (Phase 2) from the mission detail endpoint
- [x] Disk cache of the response (`%AppData%\WikeloContractor\cache\contracts.json`):
      invalidated only when a new LIVE game version appears (`GET /api/game-versions`);
      the version is re-checked at most every 12h + manual "Check for updates" in Settings;
      the app works offline from the cache (stale fallback, offline badge)
- [x] Network error handling (InfoBar in the UI: load error / offline warning)
- [x] Unit tests (`tests/` project, xUnit): DTO/model parsing, API client (429 + Retry-After,
      version selection, item classification), catalog service (cache, rate-limit gate,
      version-based invalidation), localization key parity (en/uk)

## Phase 2 — Catalog

- [x] Contract list: cards with requirements, rewards, reputation; local search box
- [x] Filters: by reward category (Ships / Ground vehicles / Paints / Weapons / Armor / Other,
      derived from reward item data via background enrichment) and by required resource
- [x] Reward preview images in the list: URLs come free with enrichment (`images` in item
      detail), files cached once in `cache/images/` from external CDNs (no API rate-limit
      impact), category icon fallback, custom overrides via `img-catalog-overrides.json`
      (two layers: bundled repo file with shared URLs — missing-image inventory in
      `docs/reward-images.md` — plus a personal `%AppData%` file that wins per key)
- [x] Contract details page: click a card → full requirements (incl. SCU amounts and extra
      entries from `hauling_orders`, e.g. Wikelo Favor) + reward cards with image, description,
      manufacturer, item stats (rarity/resistances/temperature) or vehicle stats
      (cargo/crew/HP/shields/speeds/MSRP + pledge link); back navigation via
      `NavigateWithHierarchy`. See `docs/api-item-fields.md` for what else the API offers
- [x] Settings split into two nav pages: **Settings** (language, theme, catalog data) and **About**
      (version, self-update, attribution, disclaimer) to keep each page focused
- [ ] **In-app editor for `img-catalog-overrides.json`** (on consideration): WPF-UI ships no reusable
      code-editor control (its gallery "Editor" is a rich-text demo window, "Monaco" a WebView2
      embed), so a real JSON editor means Monaco (WebView2) or AvalonEdit. Would edit the `%AppData%`
      user override layer (updates never touch it). Deferred
- [x] Detail page refinements: large reward image (own decode resolution); ship loadout —
      weapons with kind labels ("Laser Repeater") and grades, loaded ordnance with signal
      types (CrossSection/IR/EM), core components incl. jump drive with grade/class; armor
      resistances as reduction percentages + radiation protection/scrub rate; paint rewards
      show no vehicle stats; search also matches reward names; per-contract multi-category
      set so mixed-reward contracts appear under every matching category filter
- [x] Full-window reward image preview on the detail page (click a reward image → full-resolution
      overlay; click anywhere or press Esc to close). Native-resolution decode variant added to the
      `RewardPreview` attached-property loader
- [x] Blueprints granted on completion (crafting recipes/materials from mission-detail `blueprints[]`,
      captured into `WikeloContract.Blueprints` at cache schema v11): the detail page shows a
      **Blueprints** section above Rewards; catalog cards show a compact "BP: <name>" chip after the
      reward pills (fixed blue fill). Only ~5 contracts have any (e.g. the "Metamaterial Test #NNN"
      craft chain). Note: the API sends `"blueprints": null` when absent, which overwrites a non-null
      DTO initializer — the parse guards with `?.`/`?? []` (see `docs/data-pipeline.md`)
- [x] First-load progress uses a `ProgressBar` (indeterminate) instead of a `ProgressRing`
- [x] Wikelo reputation: mark a contract completed on the card and detail page (`ICompletionService`,
      persisted to `completed.json` as UUID → earned reputation); accumulated total drives a rank
      progress bar at the top of the catalog. Ranks: New Customer (0) → Very Good Customer (340) →
      Very Best Customer (999) — thresholds are not in the API (`min_standing`/`rank_index` are null),
      so they live in `Models/ReputationLevels`
- [ ] "Tracked" flag on a contract (persisted)
- [ ] Aggregation: combined resource list across all tracked contracts
- [x] **Readiness indicator (needs Inventory)**: on the catalog card and detail page, each *Required
      items* chip is colored by availability vs. the inventory — default (none), caution tint (partial),
      success tint (full) — plus a "Ready to turn in" badge and an "X / Y satisfied" count. Computed
      state lives on `ContractCardViewModel` (catalog) and `ContractDetailViewModel` (detail); the math
      is `Models/InventoryReadiness`, chips are `ViewModels/RequirementChip`, color via
      `Views/Converters/AvailabilityToBrushConverter`. Both VMs refresh on `IInventoryStore.Changed`

## Phase 3 — Inventory

- [x] Inventory counter store (`IInventoryStore` → `inventory.json`, atomic write, name-keyed),
      auto-populated from every distinct required item across the catalog. Each item has a `+`/`−`
      counter. Items are grouped into categories via a keyword classifier
      (`Models/InventoryCategoryClassifier`, unit-tested) with a per-item image supplied through a
      user-editable config (`img-inventory-overrides.json`, bundled + `%AppData%` layers) analogous
      to `img-catalog-overrides.json`; the two-layer engine is shared as `Services/OverrideFileSet`
- [x] UI: quantity editing via an editable `ui:NumberBox` (type any value — scrips/favors run to
      100s/1000s — or step by one; binding uses `UpdateSourceTrigger=PropertyChanged` so the spin
      buttons commit immediately), quick search by item name, category section headers, category
      filter dropdown
- [x] Full-window item image preview (click a row image that has one → full-resolution overlay; click
      anywhere or press Esc to close), reusing the detail page's overlay pattern via a native-resolution
      `InventoryPreview.PreviewItemName` attached-property variant. Only rows with an override image are
      clickable (a null-`Source` `Image` is not hit-tested)
- [x] Progress + readiness: per-requirement availability coloring, "X / Y satisfied" count, and a
      "Contract ready to turn in" indicator on the catalog card and detail page (see Phase 2 entry)
- [x] Marking a contract completed is gated on readiness (`IsReady`); confirming a dialog deducts the
      required amounts from the inventory. Reopening a completed contract shows a warning dialog (the
      spent items are **not** restored) listing what was deducted. Shared flow lives in
      `ViewModels/ContractCompletionInteraction` (WPF-UI `MessageBox`), called by both the catalog card
      and the detail page; the completion toggle disables until the contract is ready

## Phase 3.5 — Resource sourcing ("where to find")

A small in-app **reference / mini-wiki scoped to the inventory items** — *where to obtain each resource*.
Not a shop database: the SC Wiki `shops` data is almost always empty for our items, so it is not a viable
primary source.

- [ ] New nav page **after Inventory**, over the same item set as the inventory (distinct required items,
      shared `Models/InventoryCategoryClassifier`, grouped list with search + category filter, per-item image).
- [ ] Per resource, surface **acquisition info**: authored sourcing notes (mining / salvage / how it is
      crafted), a link to the wiki (`web_url`) when available, and a link / deep-link into the community
      search tool **cstone Finder** (https://finder.cstone.space/). Investigate whether Finder supports a
      per-item query/deep-link so a row can open its results directly.
- [ ] **Delivery — decide the mix**: (a) curated notes in a **bundled + `%AppData%` override config**
      (same two-layer `Services/OverrideFileSet` pattern as the image overrides), keyed by item name, so
      notes are authored, shipped, and personally extendable; and/or (b) **embed an external tool** (e.g.
      cstone Finder) via **WebView2** (`Microsoft.Web.WebView2` + Evergreen runtime). Keep it lightweight —
      a reference, not a live database.

## Phase 4 — Overlay

- [ ] Separate window: `Topmost`, borderless, semi-transparent background, compact inventory list
- [ ] Global show/hide hotkey (user32 `RegisterHotKey`, default configurable in Settings)
- [ ] Quantity editing from the overlay (+/- buttons, keyboard input)
- [ ] Click-through mode (toggle `WS_EX_TRANSPARENT`), remember position/size
- [ ] Verify on top of SC in Fullscreen (DWM fullscreen optimizations) and Borderless

## Phase 5 — Polish and distribution

- [ ] Tray (WPF-UI.Tray): minimize to tray, quick overlay toggle from the menu
- [ ] Start with Windows (optional)
- [ ] File logging (`Microsoft.Extensions.Logging` + a simple file provider)
- [x] Velopack: installer + auto-update. Framework-dependent build; the installer bootstraps the
      .NET 10 Desktop Runtime (`--framework net10.0-x64-desktop`) if missing. `VelopackApp.Run()`
      runs first in `App.OnStartup`; `Services/AppUpdateService` wraps `UpdateManager` (GitHub
      Releases feed) and drives a "Check for updates" row in Settings (no-op in a dev run).
      GitHub Releases doubles as the update feed. Note: the shipped `img-catalog-overrides.json` lives in
      the install dir (replaced on update); persistent user edits go to the `%AppData%` layer.
- [x] Versioning: SemVer with git tags (`vX.Y.Z`) on GitHub as the single source of truth;
      the tag version is injected into the build (`-p:Version=X.Y.Z`),
      the app shows its version in Settings (reads it from the assembly)
- [x] GitHub Actions — CI (`.github/workflows/ci.yml`): restore + build + run unit tests on
      `windows-latest` for every PR to `dev`/`main` (job `build-and-test`) and pushes to `dev`.
      Merge blocking + required approvals are enforced via GitHub **Rulesets** (main: PR +
      1 approval + Code Owner review + required `build-and-test`; dev: required `build-and-test`,
      owner in the bypass list so direct pushes still work).
- [x] GitHub Actions — release (`.github/workflows/release.yml`): on pushing a `vX.Y.Z` tag,
      publish framework-dependent → `vpk pack` → `vpk upload github`, producing a `Setup.exe` and
      the delta feed on a GitHub Release. Optional code signing is a secret-gated step (dormant
      until an OV/EV cert is provided).

## Phase 6 (optional) — Cloud sync

- [ ] Backend (Supabase: Postgres + RLS, as in SCLOC-Verse) + Discord OAuth for identity
- [ ] Sync inventory and tracked contracts between devices

---

## Technical notes

- **Overlay on top of the game**: on Win11, DWM with fullscreen optimizations composites even SC's
  "Fullscreen" mode, so topmost windows are visible (confirmed by the SCLOC-Verse app). True exclusive
  fullscreen is the only case where the overlay is not drawn.
- **Localization**: runtime swap of the merged ResourceDictionary
  (`Resources/Localization/Strings.{en|uk}.xaml`), XAML uses `{DynamicResource}` only.
- **WPF-UI v4**: pages are resolved by DI via `AddNavigationViewPageProvider()`;
  a page = `INavigableView<TViewModel>`, `DataContext = this`, bindings via `ViewModel.*`.
