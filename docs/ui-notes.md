# UI notes & WPF-UI quirks

Read this before touching anything under `src/Views/` or `src/ViewModels/`.
Base conventions (MVVM, navigation, localization rules) are in `CLAUDE.md`; this file
collects patterns and pitfalls discovered while building the UI.

## WPF-UI (lepoco) pitfalls

- **Do not use `ListView`** for item lists: WPF-UI 4.x registers no `ListViewItem` style
  resource, so the app crashes at runtime with *"Cannot find resource named
  'System.Windows.Controls.ListViewItem'"*. Use `ItemsControl` inside a `ScrollViewer`
  (see `CatalogPage.xaml`).
- `ui:TitleBar` icon is set from code-behind, not XAML — see "Adaptive app icon" below.
- `ui:InfoBar` needs `IsOpen` bound with `Mode=OneWay` (or `TwoWay` when `IsClosable="True"`
  so the close button can clear the VM flag).

## Status surface pattern (CatalogPage)

One `StackPanel` row hosts all transient states; each is an InfoBar/element bound to its
own VM flag, only one is normally visible at a time:

| Flag | Element | Meaning |
|---|---|---|
| `HasLoadError` | Error InfoBar | no network **and** no cache — nothing to show |
| `IsOffline` | Warning InfoBar | API unreachable, stale cache shown |
| `RateLimit.IsActive` + `RateLimit.Message` | Warning InfoBar (closable) | HTTP 429, live countdown text (shared watcher) |
| `IsLoading` | ProgressRing + caption | first fetch in progress |
| `IsEmpty` | TextBlock | filters matched nothing |

`IsSynced` and `IsOffline` are **computed** from the service's single `CatalogStatus`
(`Online` / `Offline` / `RateLimited`) plus `HasLoadError`, so they can never contradict each
other; the backing `Status`/`HasLoadError` carry `[NotifyPropertyChangedFor]` for both.
The sync badge in the header (`IsSynced` → green `CloudCheckmark24`, `IsOffline` →
caution `CloudOff24`) plus `GameVersion` text is the persistent counterpart.

The rate-limit countdown lives in a shared `RateLimitWatcher` (singleton in `ViewModels/`,
injected into both `CatalogViewModel` and `SettingsViewModel`) so both pages show identical
state. It subscribes to the service's `RateLimitChanged` event, reads the authoritative
`RateLimitedUntil` deadline, and ticks a `DispatcherTimer` (1 s) to compose the message from
the `Catalog_RateLimited_Retry` format string (`{DynamicResource}` cannot inject the number).

## Adaptive app icon

The window/taskbar icon follows the theme: light icon on the dark theme and vice versa.
`MainWindow` subscribes to `ApplicationThemeManager.Changed`, swaps `TitleBarControl.Icon`
and `Window.Icon` between `Assets/icon.png` (dark art) and `Assets/icon-light.png`,
and unsubscribes in `OnClosed`. The exe's `app.ico` stays static (Explorer/shortcuts).

## Localized strings with parameters

`{DynamicResource}` only handles static text. For formatted messages, store a format
string in BOTH dictionaries (e.g. `Catalog_RateLimited_Retry` = "… resumes in {0} s."),
fetch it with `Application.Current.TryFindResource(key)`, `string.Format` it in the VM and
bind the resulting property. A localization unit test asserts placeholder parity between
languages, so `{0}` counts must match.

## ViewModel conventions

- The contract list is an `ICollectionView` (`ListCollectionView` over the loaded list) with a
  `Filter` predicate. Filter `OnXChanged` hooks call `Contracts.Refresh()` (re-evaluates in
  place) instead of rebuilding an `ObservableCollection` on every keystroke; a fresh view is
  created only when a new catalog is loaded. `IsEmpty` reads `Contracts.IsEmpty`.
- Prefer deriving read-only UI state from one source over hand-syncing parallel bools:
  `IsSynced`/`IsOffline` are computed from `CatalogStatus` (see the status surface above).
- Guard first-time initialization with an `_isInitialized` flag when OnChanged hooks
  persist settings (see `SettingsViewModel`) — assign through the generated property,
  never the backing field (MVVMTK0034).
- Service events arrive on background threads — wrap handler bodies in
  `Application.Current.Dispatcher.Invoke`.
