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
      impact), category icon fallback, custom overrides via `image-overrides.json`
      (two layers: bundled repo file with shared URLs — missing-image inventory in
      `docs/reward-images.md` — plus a personal `%AppData%` file that wins per key)
- [x] Contract details page: click a card → full requirements (incl. SCU amounts and extra
      entries from `hauling_orders`, e.g. Wikelo Favor) + reward cards with image, description,
      manufacturer, item stats (rarity/resistances/temperature) or vehicle stats
      (cargo/crew/HP/shields/speeds/MSRP + pledge link); back navigation via
      `NavigateWithHierarchy`. See `docs/api-item-fields.md` for what else the API offers
- [x] Detail page refinements: large reward image (own decode resolution); ship loadout —
      weapons with kind labels ("Laser Repeater") and grades, loaded ordnance with signal
      types (CrossSection/IR/EM), core components incl. jump drive with grade/class; armor
      resistances as reduction percentages + radiation protection/scrub rate; paint rewards
      show no vehicle stats; search also matches reward names; per-contract multi-category
      set so mixed-reward contracts appear under every matching category filter
- [ ] "Tracked" flag on a contract (persisted)
- [ ] Aggregation: combined resource list across all tracked contracts

## Phase 3 — Inventory

- [ ] `InventoryItem` model (item ref + quantity), persisted to `inventory.json` (atomic write)
- [ ] UI: add/edit quantities, quick search by item name
- [ ] Progress: "collected X of Y" for each tracked contract (inventory × requirements)
- [ ] "Contract ready to turn in" indicator

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
- [ ] Velopack: auto-update + installer
- [ ] Versioning: SemVer with git tags (`vX.Y.Z`) on GitHub as the single source of truth;
      the tag version is injected into the build (`dotnet build -p:Version=X.Y.Z`),
      the app shows its version in Settings (already reads it from the assembly)
- [ ] GitHub Actions — CI: run unit tests + build on every PR to `dev`/`main`
- [ ] GitHub Actions — release: on pushing a `vX.Y.Z` tag, build with the tag version,
      package (installer/zip) and publish a GitHub Release with the artifacts

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
