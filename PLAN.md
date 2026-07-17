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
- [ ] App icon (`.ico`) — add `<ApplicationIcon>` to the csproj + `ui:TitleBar Icon`

## Phase 1 — API client

- [ ] Explore swagger at `docs.star-citizen.wiki`: find Wikelo mission endpoints
      (cross-check with pages like `api.star-citizen.wiki/missions/...`, `?filter[...]` filters)
- [ ] `Services/Api/StarCitizenWikiClient` via `IHttpClientFactory` + `System.Text.Json`
- [ ] Models: `WikeloContract` (name, Emporium location, requirements: item+quantity, rewards, game version)
- [ ] Disk cache of the response (`%AppData%\WikeloContractor\cache\contracts.json` + timestamp),
      TTL + manual refresh; the app works offline from the cache
- [ ] Network error handling (InfoBar in the UI)
- [ ] Unit tests for model parsing (`tests/` project)

## Phase 2 — Catalog

- [ ] Contract list: cards or table (`ui:Card` / `ListView`), search (`AutoSuggestBox`)
- [ ] Filters: by reward type (Ship / Weapon / Armor / Other), by Emporium location
- [ ] Contract details: full list of requirements and rewards
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
- [ ] GitHub Actions: build + release

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
