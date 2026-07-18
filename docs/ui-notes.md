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

## Reward preview images (CatalogPage)

Each contract card shows a 64×64 preview left of the content, loaded asynchronously by the
`helpers:RewardPreview.Contract` attached property on an `Image` (`Views/Helpers/RewardPreview.cs`):

- Candidate order per reward: override → thumbnail → original; the first that downloads
  **and decodes** wins. Overrides are two-layered (`ImageOverrideService`): the bundled
  `src/Resources/image-overrides.json` (in the repo, ships with the app — add shared image
  URLs there) plus the user's `%AppData%` file, which wins per key. A `.webp` thumbnail on a machine without the WebP codec fails decode
  and falls back to the original PNG automatically.
- Bitmaps are decoded on a worker thread (`DecodePixelWidth=128`), frozen, and memoized for
  the session, so filter refreshes don't re-decode.
- The **final result per candidate list** (including "nothing loadable") is also memoized:
  `ICollectionView.Refresh` regenerates every card container on each search keystroke, and
  the memo turns those re-fires into a synchronous `Source` assignment — no placeholder
  flash, no repeated downloads or decode attempts. The memo key includes the override URL,
  so editing `image-overrides.json` still takes effect on refresh; a failed URL is not
  retried until the app restarts.
- After awaiting, the handler re-checks the attached value (`ReferenceEquals`) — the template
  may have been rebound while loading; stale results are dropped.
- The category placeholder (`CategoryToSymbolConverter`) sits under the `Image` and stays
  visible via `PresenceToVisibilityConverter` (`Invert="True"`, the `NullToVisibility`
  resource) bound to the image's `Source` (ElementName binding), so contracts without images
  (Wikelo-exclusive variants) show an icon instead.

## Contract detail page (navigation outside the nav menu)

`ContractDetailPage` is a DI singleton like every page but is **not** a NavigationView menu
item. The flow: catalog card click (a `MouseBinding` on the card `Border`, command bound via
`RelativeSource AncestorType=Page`) → `CatalogViewModel.OpenDetails` sets the contract on the
shared `ContractDetailViewModel` (`Show(contract)`) → `INavigationService.NavigateWithHierarchy`
(navigates with back-stack support for non-menu pages; plain `Navigate` would not). The page's
back button calls `INavigationService.GoBack()`.

Contracts are immutable records and enrichment rebuilds them as new instances, so the detail
VM subscribes to `CatalogUpdated` and swaps its snapshot for the fresh contract by UUID —
otherwise a page opened before enrichment finished would stay reward-less forever.

Reward stat chips are composed in the VM (`RewardDisplay`) from localized format strings via
`TryFindResource` — same pattern as the rate-limit countdown; they refresh on re-navigation,
not live on language switch (accepted trade-off). Damage-type names in resist chips are game
data and stay English; the stored damage **multipliers** (0.7 = takes 70% damage) render as
reduction percentages ("energy −30%"), with ×1.0 entries (no effect) skipped. Ship rewards additionally show two chip groups — Weapons (fixed guns,
mounts, missile count) and Components (power plant, shields, coolers, quantum drive) — from
`RewardDetails.Weapons`/`.Components`; component type labels are resolved via
`ComponentTypeDisplay.LabelKey` (`Details_Comp_*` keys), the same enum→key pattern as
`ContractCategoryDisplay`.
Paint-category contracts suppress all three chip groups: a paint reward is a full vehicle
variant record in the API, but its stats belong to the vehicle, not the paint.

The detail image is decoded at a higher resolution than the 64 px list thumbnail:
`RewardPreview` keys its decode/result memos by decode width (128 list / 640 detail), so the
same URL yields two cached bitmaps of different sizes.

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
