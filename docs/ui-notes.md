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
- **No `ui:ProgressBar` control** — WPF-UI 4.x ships `ui:ProgressRing` but *not* a ProgressBar;
  use the plain WPF `<ProgressBar>` (WPF-UI themes it via implicit styles). Likewise the gallery's
  "Editor" (a rich-text demo) and "Monaco" (a WebView2 embed) are sample *windows*, not reusable
  controls — there is no drop-in code-editor control.
- **`ui:NumberBox` is the numeric counter control** (WPF-UI 4.3 *does* ship it — an earlier note here
  wrongly claimed it did not). The inventory row uses it with `SpinButtonPlacementMode="Inline"`,
  `Minimum="0"`, `MaxDecimalPlaces="0"`, `SmallChange="1"` so the player can **type** a value directly
  (scrips/favors run to 100s/1000s) or step by one. Its `Value` is a `double?`; two-way bound to the
  VM's `int Count`, WPF's default numeric conversion bridges the two. Persistence is on `Count`'s
  `partial void OnCountChanged` (clamps, then `IInventoryStore.SetCountAsync`) — not a command — so
  typed edits and spin steps persist identically. **The `Value` binding must set
  `UpdateSourceTrigger=PropertyChanged`** — without it the source (`Count`) commits only on focus
  loss, so the inline +/- spin buttons appear to do nothing (they change `Value` programmatically but
  never reach the VM until the box is blurred). The store de-dups no-op writes, so committing on every
  change is cheap.
- **Overlay scrollbars overlap content** — WPF-UI restyles `ScrollViewer` with a thin overlay
  scrollbar drawn at the right edge *on top of* the content (not in its own column), so cards/buttons
  under it get clipped. Fix: give the **scrolled content** a right `Margin` (~16) so the scrollbar sits
  in that gutter — applied on `CatalogPage`, `ContractDetailPage` and `InventoryPage`. (`ScrollViewer.Padding`
  is not reliably honored by the restyled template; a content `Margin` always works.)
- **Dialogs use `Wpf.Ui.Controls.MessageBox`**, a self-contained Fluent window with `ShowDialogAsync()`
  → `MessageBoxResult` — no `ContentDialogService`/dialog-host wiring needed. Alias it
  (`using UiMessageBox = Wpf.Ui.Controls.MessageBox;`) to avoid the clash with the global-using
  `System.Windows.MessageBox`. Set `Owner = Application.Current.MainWindow` to center it.
- **`ui:HyperlinkButton` ignores `Foreground`** — its template hard-codes the text color to the theme
  keys `HyperlinkButtonForeground` / `…PointerOver` / `…Pressed`. To recolor it (e.g. the detail page's
  accent "Open on the wiki" link) override those keys **locally** in the button's `.Resources`, not via
  the `Foreground` property. It navigates natively through `NavigateUri` (no command needed); a plain
  `ui:Button` would need a launch command instead.
- **Do not use `ui:ToggleButton` for a flag the *service* owns** (the favourite star, the completion
  toggle). Clicking a ToggleButton writes `IsChecked` locally, and a local value **replaces** the
  binding — so `IsChecked="{Binding IsFavorite, Mode=OneWay}"` works exactly once and then goes deaf
  to the store. Use a plain `ui:Button` bound to a command, and swap the icon/colour/tooltip from a
  `DataTrigger` on the read-only VM property (`Resources/ContractCard.xaml`, `ContractDetailPage.xaml`).
- **`SymbolRegular.XxxOff` is a struck-through glyph, not an "empty" one.** `StarOff28` is a star
  with a slash across it — it means "favourites disabled", so using it for "not starred yet" reads as
  a broken feature. For a two-state toggle keep the *same* glyph and flip `Filled`:
  `{ui:SymbolIcon Symbol=Star28}` → `{ui:SymbolIcon Symbol=Star28, Filled=True}`. Both `ui:SymbolIcon`
  and its markup extension expose `Filled`.

## Sourcing knowledge base (Phase 3.5)

- The **Where to Find** list is the inventory's item set without counters, built the same way
  (distinct requirements → `InventoryCategoryClassifier` → grouped `ListCollectionView`). The 46 × 46
  thumb is the shared `ItemThumbTemplate` (`Resources/ItemThumb.xaml`); any page using it must expose
  an `OpenPreviewCommand`.
- Content comes from `docs/sourcing/*.md`, **shipped in the release**, not fetched at runtime — the
  app has to work offline. The front matter's `name` must equal the requirement name exactly; the
  file name is cosmetic.
- **Render Markdown with `Views/Controls/MarkdownViewer`, never a `FlowDocumentScrollViewer`.**
  WPF-UI does not theme the latter, so it arrives with its own fonts, scrollbar and white page and
  fights the token layer. The viewer builds `TextBlock`s from `Models/MarkdownDocument`'s blocks.
- The parser is **total on purpose**: unknown syntax becomes plain text rather than throwing, so a
  bad guide can never break the page. `<!-- comments -->` are stripped at load, which is why a
  comments-only stub correctly reports "no body" and shows `Sourcing_GuidePlaceholder`.
- `MarkdownViewer` only launches `http`/`https` links. The `%AppData%` layer is user-writable, so a
  `file:` or custom-scheme URI must never reach the shell.

## Favourites (Phase 2.5)

The Favourites page is the catalog with a narrower source — not a second catalog:

- `ViewModels/ContractListViewModel` is the shared base (cards, `ICollectionView`, filters, empty
  state, `OpenDetails`, and the fan-out of service `Changed` events onto every card).
  `CatalogViewModel` and `FavoritesViewModel` override **only** `RebuildFromCatalog`.
- The row markup lives once in `Resources/ContractCard.xaml` as `ContractCardTemplate`. It is merged
  **after** `Chips.xaml` in `App.xaml`, because it resolves `ChipStyle`/`RequirementChipTemplate`/
  `ChipWrapPanel` via `StaticResource`, which only looks backwards through merged dictionaries.
  The template binds the row click through `{RelativeSource AncestorType=Page}` → `ViewModel.OpenDetailsCommand`,
  which is why it works unchanged on both pages: both expose the base's command under `ViewModel`.
- `FavoritesViewModel` rebuilds (not just refreshes) on `IFavoritesService.Changed` — un-starring a
  contract has to remove its row, not merely redraw the star.
- It also rebuilds in `OnNavigatedTo`: the VM is constructed on the first navigation to the page,
  which is usually long after the catalog finished loading, so its `CatalogUpdated` never arrived.
- Two mutually exclusive empty states, two keys: `Favorites_Empty` ("nothing starred", from
  `HasNoFavorites`) and `Catalog_Empty` ("filters matched nothing", from the base's `IsEmpty`).

## Status surface pattern (CatalogPage)

One `StackPanel` row hosts all transient states; each is an InfoBar/element bound to its
own VM flag, only one is normally visible at a time:

| Flag | Element | Meaning |
|---|---|---|
| `HasLoadError` | Error InfoBar | no network **and** no cache — nothing to show |
| `IsOffline` | Warning InfoBar | API unreachable, stale cache shown |
| `RateLimit.IsActive` + `RateLimit.Message` | Warning InfoBar (closable) | HTTP 429, live countdown text (shared watcher) |
| `IsLoading` | ProgressBar (indeterminate) + caption | first fetch in progress |
| `IsEmpty` | TextBlock | filters matched nothing |

`IsSynced` and `IsOffline` are **computed** from the service's single `CatalogStatus`
(`Online` / `Offline` / `RateLimited`) plus `HasLoadError`, so they can never contradict each
other; the backing `Status`/`HasLoadError` carry `[NotifyPropertyChangedFor]` for both.
The sync badge in the header (`IsSynced` → green `CloudCheckmark24`, `IsOffline` →
caution `CloudOff24`) plus `GameVersion` text is the persistent counterpart.

The catalog and contract-detail headers show the version **without** the API build number
(`4.9.0-LIVE`), via `Models/GameVersionDisplay.WithoutBuild` — the single home for that formatting.
The build counts API data revisions, not game patches, so beside a game version it reads as a patch
number it is not. Settings keeps the full string, labelled **API version**
(`SettingsViewModel.DataApiVersion`).

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
  `src/Resources/img-catalog-overrides.json` (in the repo, ships with the app — add shared image
  URLs there) plus the user's `%AppData%` file, which wins per key. A `.webp` thumbnail on a machine without the WebP codec fails decode
  and falls back to the original PNG automatically.
- Bitmaps are decoded on a worker thread (`DecodePixelWidth=128`), frozen, and memoized for
  the session, so filter refreshes don't re-decode.
- The **final result per candidate list** (including "nothing loadable") is also memoized:
  `ICollectionView.Refresh` regenerates every card container on each search keystroke, and
  the memo turns those re-fires into a synchronous `Source` assignment — no placeholder
  flash, no repeated downloads or decode attempts. The memo key includes the override URL,
  so editing `img-catalog-overrides.json` still takes effect on refresh; a failed URL is not
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

Blueprints granted on completion (`Contract.Blueprints`, from mission-detail `blueprints[]`;
only ~5 contracts have any) get their own section **above Rewards**: a `Details_Blueprints`
heading (same 18 px SemiBold style as Requirements/Rewards) plus one `Molecule24` name-only pill
per entry, the whole `StackPanel` gated on `ViewModel.HasBlueprints`. The catalog card shows the
same list compactly **after the reward pills** as "BP: <name>" chips (`Catalog_BlueprintAbbrev` =
"BP"; an empty `ItemsControl` source renders nothing, so no visibility flag there). Blueprint
names are English game data; only the heading and the "BP" abbreviation are localized. Both badge
kinds use a **fixed** `#0067C0` blue fill with fixed `White` text (not a theme brush): the Fluent
`SystemFillColorAttentionBackground` tint is too faint to read as a colored badge on a dark card, and
because the fill is fixed, the foreground is fixed too so contrast holds (~5.7:1) in either theme.

The detail image is decoded at a higher resolution than the 64 px list thumbnail:
`RewardPreview` keys its decode/result memos by decode width (128 list / 640 detail), so the
same URL yields cached bitmaps of different sizes. The full-window preview (below) adds a third
variant at **native** resolution (`DecodePixelWidth=0`); that one is deliberately **not** memoized
— only one preview is on screen at a time, so pinning its multi-MB bitmaps for the whole session
isn't worth the memory (`memoize = decodePixelWidth != 0` gates both memos).

## Full-window reward image preview (ContractDetailPage)

Clicking a reward image opens a full-window overlay — the app's only overlay pattern. The page
root is a `Grid` wrapping the `ScrollViewer` plus a sibling full-bleed `Grid` (later in XAML =
higher Z-order, semi-transparent `#CC000000`) whose `Visibility` binds `IsPreviewOpen`.
`OpenPreview(reward)` — a `MouseBinding` on the reward `Image`, command reached via
`RelativeSource AncestorType=Page` — sets `PreviewReward` and opens it; a `MouseBinding` on the
overlay and a page-level `Esc` `KeyBinding` both call `ClosePreview`. The overlay `Image` uses the
`RewardPreview.PreviewReward` attached property (the native-resolution variant). `OnContractChanged`
closes any open preview when the shown contract changes.

**`InventoryPage` reuses the same overlay pattern** for item images: the page root wraps its content
`Grid` plus a sibling `#CC000000` overlay bound to `InventoryViewModel.IsPreviewOpen`; the row `Image`
carries `Cursor="Hand"` + a `MouseBinding` to `OpenPreview(Name)`, the overlay/`Esc` call
`ClosePreview`, and the overlay `Image` uses `InventoryPreview.PreviewItemName` (native-resolution,
unmemoized). Because a null-`Source` `Image` is not hit-tested, only rows that actually have an
override image are clickable — no empty-overlay no-op is needed. `BuildItems` closes any open preview
on rebuild.

## Contract completion & Wikelo reputation

`ICompletionService` persists completed contracts to `%AppData%\WikeloContractor\completed.json`
as a UUID→earned-reputation map (storing the amount, not just the id, keeps the running total
correct when a contract rotates out of the catalog across patches). `TotalReputation` feeds
`ReputationLevels.Compute` (thresholds New 0 / Very Good 340 / Very Best 999 — the API leaves
`min_standing`/`rank_index` null, so they live in `Models/ReputationLevels`) → `ReputationSummary`
(localized rank label + `Fraction` for the catalog's top progress bar, `Maximum="1"`).

Catalog cards bind a per-item `ContractCardViewModel` wrapper (not the raw record) so completion is
observable and it is the home for the readiness indicator (below). The completion toggle lives
on both the card and the detail VM; completing/reopening now routes through
`ContractCompletionInteraction` (see "Inventory & readiness"). Both rely on the service's `Changed`
event to refresh — the **list** is refreshed by `CatalogViewModel.OnCompletionChanged` iterating its
cards (one subscription total, not one per card), while the single **detail** VM self-subscribes.
Rank names stay English in both dictionaries (game standings); the surrounding text is localized.

## Inventory & readiness

The **Inventory page** is the second data-driven list. Its items are auto-derived from every distinct
required-item name across the catalog (`InventoryViewModel` flattens `Contract.Requirements`), each
wrapped in an `InventoryItemViewModel` with a persisted editable `ui:NumberBox` counter
(`IInventoryStore` → `inventory.json`; type a value or step by one). Items are grouped into category sections via a `ListCollectionView` with a
`PropertyGroupDescription` on `CategoryLabel` (`GroupStyle` renders the headers) plus a `Filter`
combining the search box and a category dropdown — the same collection-view idiom as the catalog.
Categories come from `InventoryCategoryClassifier` (name-keyword rules; see `CLAUDE.md`), the placeholder
icon per category from `InventoryCategoryToSymbolConverter`.

Item **images** have no API source, so they load purely from a user-editable override config
(`InventoryImageOverrideService` → `img-inventory-overrides.json`, bundled + `%AppData%` layers)
through the `helpers:InventoryPreview.ItemName` attached property — a simpler cousin of `RewardPreview`
(override URL → disk cache → decode; category icon placeholder until it loads). The two-layer +
hot-reload mechanics are shared with reward overrides via `Services/OverrideFileSet`. Clicking a row
image that has one opens a full-window preview (`InventoryPreview.PreviewItemName`; see the overlay
pattern below).

**Readiness** compares requirements against inventory counts (`Models/InventoryReadiness`). On the
catalog card and detail page, each requirement chip is colored by `AvailabilityToBrushConverter`
(none → default, partial → caution tint, full → success tint), plus a "Ready to turn in" badge and an
"X / Y satisfied" count. Both `ContractCardViewModel` and `ContractDetailViewModel` recompute on
`IInventoryStore.Changed`; `ShowReadiness` hides the badge/count once a contract is completed (its
chips render neutral, since availability is then moot).

Completion is wired to the inventory through `ViewModels/ContractCompletionInteraction`: the toggle is
gated on `IsReady` (`RelayCommand.CanExecute`, so the button disables until the inventory covers the
requirements). Completing shows a confirm dialog then **deducts** `InventoryReadiness.RequiredCount`
per requirement; reopening shows a warning dialog and lists what was deducted but does **not** restore
it (the inventory is the source of truth — the user updates it manually). Deductions fire
`IInventoryStore.Changed`, so sibling contracts recompute their readiness immediately.

## Adaptive app icon

`MainWindow.UpdateAppIcon` follows `ApplicationThemeManager.Changed` (unsubscribed in `OnClosed`)
and feeds two surfaces from **different** assets — they are not interchangeable:

- **Title bar** (`TitleBarControl.Icon`) — the vector mark from `Resources/BrandIcons.xaml`
  (`AppMarkDark` / `AppMarkLight`), so it stays crisp at any DPI instead of downscaling a PNG.
  Follows the **app** theme: this app paints that surface.
- **Taskbar / Alt-Tab** (`Window.Icon`) — must stay a raster `BitmapImage` (WPF hands it to Win32
  as an `HICON`). Follows the **Windows shell** theme, read from `SystemUsesLightTheme`.

**The two themes are set independently, so they must not share one signal.** Windows 11 exposes
`AppsUseLightTheme` and `SystemUsesLightTheme` separately, and this app's own theme setting
(System/Light/Dark) is a third input. Driving the taskbar icon from the app theme puts the navy
mark on a dark taskbar at a **1.2:1** contrast ratio whenever they disagree — effectively
invisible. Picking per surface keeps both at ≥ 7:1.

`SystemUsesLightTheme` changes do not raise `ApplicationThemeManager.Changed`, hence the extra
`SystemEvents.UserPreferenceChanged` subscription (unsubscribed in `OnClosed` alongside the other).

Each asset is a full-bleed rounded "W" tile (opaque tile, transparent corners), and the key names
follow the surface it is drawn for: a dark surface takes the cyan-tile art (`AppMarkLight` /
`icon-light.png`), a light surface the dark-tile art (`AppMarkDark` / `icon.png`). Driving either
surface from the wrong theme drops the mark's contrast below AA.

Do not set an explicit size on the title bar `ImageIcon`: the `ui:TitleBar` template constrains
its icon slot and clips anything larger flat at the top and bottom instead of scaling it.

`Resources/BrandIcons.xaml` is a hand-transcription of the **`mid`** masters
(`docs/brand/master-{ondark,onlight}-mid.svg`, 200 × 200 viewBox) — WPF cannot load `.svg` itself,
so the SVGs stay reference masters and are deliberately not csproj `<Resource>` entries. A colour
or geometry change in the mid master must be copied into `BrandIcons.xaml` by hand. Re-export the
rasters from the masters rather than upscaling PNGs. See `docs/brand/icon-spec.md` for which master
feeds which `app.ico` frame.

## Localized strings with parameters

`{DynamicResource}` only handles static text. For formatted messages, store a format
string in BOTH dictionaries (e.g. `Catalog_RateLimited_Retry` = "… resumes in {0} s."),
fetch it with `Application.Current.TryFindResource(key)`, `string.Format` it in the VM and
bind the resulting property. A localization unit test asserts placeholder parity between
languages, so `{0}` counts must match.

## ViewModel conventions

- The contract list is an `ICollectionView` (`ListCollectionView` over the per-contract
  `ContractCardViewModel` wrappers) with a `Filter` predicate. Filter `OnXChanged` hooks call
  `Contracts.Refresh()` (re-evaluates in place) instead of rebuilding an `ObservableCollection`
  on every keystroke; a fresh view is created only when a new catalog is loaded. `IsEmpty` reads
  `Contracts.IsEmpty`.
- Prefer deriving read-only UI state from one source over hand-syncing parallel bools:
  `IsSynced`/`IsOffline` are computed from `CatalogStatus` (see the status surface above).
- Guard first-time initialization with an `_isInitialized` flag when OnChanged hooks
  persist settings (see `SettingsViewModel`) — assign through the generated property,
  never the backing field (MVVMTK0034).
- Service events arrive on background threads — wrap handler bodies in
  `Application.Current.Dispatcher.Invoke`.
