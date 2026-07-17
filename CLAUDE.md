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
