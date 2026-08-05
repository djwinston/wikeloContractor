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
- Every `##` section is wrapped in a stock `Expander`, **expanded by default** — a long step list can
  be folded away, but a guide opens readable. The stock control is used on purpose: WPF-UI ships
  `DefaultExpanderStyle`, so it lands on the token layer without a hand-rolled disclosure control.
- `![alt](url)` renders as a picture, resolved through `Views/Helpers/ThumbnailLoader` — the same
  disk-cached path as the reward thumbs, so a remote slide downloads once and a relative path
  resolves against the install directory. Loading is fire-and-forget and never upscales
  (`StretchDirection.DownOnly`); a missing image leaves an empty slot instead of failing the page.
  Image references are limited to `http`/`https`/local paths for the same reason links are.

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

### The completion filter

A starred contract stays starred once it is done, so a working library fills up with finished rows.
`CompletionIndex` (0 = either, 1 = not completed, 2 = completed) hides them without un-starring, and
both list pages render the same three-item combo — the axis itself is `bool? Completed` on
`Models/ContractFilter`, so neither page owns it.

`ContractFilter.Matches` takes the completion flag as a **parameter** rather than reading it:
completion is `ICompletionService` state keyed by UUID, not something a `WikeloContract` carries, and
injecting a service would cost that record the purity that makes it testable without a window.

**`OnCompletionChanged` refreshes the collection view, not just the cards.** The filter reads a card
property the view cannot observe, so completing a contract while "not completed" is selected would
leave its row on screen until something else happened to re-filter. Unconditional is fine here —
completion changes at click rate. **Do not copy this into `OnInventoryChanged`**: that is the ~30×/s
held-hotkey path, where a collection `Reset` is exactly the churn `SyncGathering` exists to avoid.

### Two tabs, not one column

The page is a stock `TabControl`: **Contracts** and **What to still gather**. WPF-UI themes
`TabControl`/`TabItem`, so the chrome is the token layer's — except that three of WPF-UI's tab
brushes all resolve to `CardStrokeColorDefault` (`#19000000`, 10 % **black**) or to a `#282828` fill
on a `#202020` page, which on dark leaves the selected tab, its outline **and the rule under the
strip** invisible. All three are re-pointed to the card recipe the same page already uses: the strip
rule through the `BorderBrush` property the template exposes, the other two in `TabControl.Resources`.
Full rationale and the rules for doing this again: `docs/design-system.md`, "When a WPF-UI token has
no answer".

It started as a collapsed `Expander` above the list, and the two halves fought over the same
vertical space: expanding the plan pushed the contract list — the reason the page exists — down to
one visible card, which is why the panel had to ship collapsed. Tabs remove the cause rather than
the symptom, and give the plan the room to be a grid instead of a mass of chips.

Three rules learned in the doing:

- **Never bind `TabItem.Visibility`.** WPF does not move the selection off a tab that disappears, so
  hiding the selected one leaves a blank content area. Both tabs are always present; the states live
  inside them (`HasOutstanding` → the grid, `HasNothingToGather` → the "already covered" line,
  `HasNoFavorites` → the "nothing starred yet" block, which both tabs render from one page-local
  template).
- **The count badge moved to the tab header.** It is the only place it stays glanceable while the
  other tab is open, which is the job the expander header used to do.
- **A `TabItem` whose header is a panel has no accessible name** — it reports its own `ToString` to
  UI Automation. The gathering tab sets `AutomationProperties.Name` explicitly.

The filters live inside the Contracts tab. "The plan ignores the page's filters" used to be a rule
stated only in prose; with the two on separate tabs nothing implies otherwise.

### The gathering plan

The **What to still gather** tab is the combined shortfall across the starred contracts. The
arithmetic is `Models/GatheringPlan`; the page only renders it. A card states **one** quantity —
`Have / Required` beside the shared `ReadinessBarStyle` meter — and leaves the shortfall as the gap
between the two.

It briefly also carried a "still to gather N" line, and dropping it is worth recording:

- It repeated the tab's own title on every card, which at six cards across is six copies of the
  answer to a question the header already answered.
- The number it held is `Required − Have`, already on the card.
- It was the only element here that wanted a **status colour**, and that is a fight not worth
  having. `Chip*ValueBrush` is built to sit on a tinted chip fill; on a bare card surface the caution
  band is a pale amber that vanishes on white and the neutral band is plain body text — legible in
  one theme at the cost of the other. Nothing on the card needs colour to be understood now.

Cards in a `UniformGrid` whose `Columns` come from the window width via
`Views/Converters/WidthToColumnsConverter` (minimum column width as `ConverterParameter`). Not a
`WrapPanel` with a fixed `ItemWidth`: that flows items at their own width and leaves a ragged gutter
on the right, where uniform columns stretch to fill the row.

Four rules, each of which is wrong in an obvious-in-hindsight way if reversed:

- **Completed contracts are excluded.** Completing already deducted their items, so counting them
  again sends the player out for things they have handed over. This is the correctness of the whole
  feature.
- **The inventory is one pool.** Two contracts asking for 36 SCU of Gold need 72 between them —
  precisely what a per-contract readiness chip cannot say, and the reason the panel exists.
- **Fully covered items are left out**, and the amounts are the same whole units
  `InventoryReadiness.RequiredCount` deducts, so the list a player mines against is the list
  completing will actually consume.
- **It ignores the page's filters.** They are a way to find a row; a shopping list that changes
  because a search box has text in it is not a shopping list. The rebuild instead hangs off the four
  things that genuinely move the number — starring, completing, an inventory edit, enrichment — via
  `OnContractsSet` / `OnCompletionChangedCore` / `OnInventoryChangedCore` / `OnSyncStateChangedCore`.

**The rows are reconciled, never cleared and refilled** (`FavoritesViewModel.SyncGathering`). One of
those four triggers is `IInventoryStore.Changed`, which a held overlay hotkey raises about thirty
times a second — so a clear-and-refill would discard every row and every `PinToggle` on it (a
resource lookup apiece) and raise a collection `Reset` that re-materializes every card, all to move
one number. Rows are keyed by name and both sequences are ordered by name, so one walk reconciles
them and an edit touches only the row that actually changed. This is the same concern
`InventoryViewModel.ForEachRow` is written around; keep the two consistent.

Chrome decisions, each measured on a real screen rather than reasoned about:

- Card chrome is the **inventory row's** (same brushes, radius and padding), so a required item looks
  the same wherever it is counted, and no new design token was needed.
- **No status colour anywhere on the card.** See above — the availability brushes are chip-context
  brushes, and the card is not a chip.
- The `Have / Required` pair labels itself through a **tooltip**. At six cards across, a word on
  every one of them is noise, and the pair reads as a ratio on sight.
- The count is a **badge built from the brand caution palette**, bound straight to `Gathering.Count`
  (`ObservableCollection` raises `PropertyChanged` for it, so no mirrored property). It has to stay
  legible on an unselected tab, which "(27)" appended to the header does not.
  **Not `ui:InfoBadge`**: its template is sized for a single digit, so a two-digit value is clipped on
  all four sides, and its `Severity` colours are identical in both themes — the brand palette is the
  layer that has a light and a dark answer.
- The explanation sits at the **bottom**, at caption size — it answers "how were these numbers
  reached", which is only a question once you have looked at them. It is a composed icon + text row,
  **not `ui:InfoBar`**: that control top-aligns its glyph with a fixed 2 px nudge tuned for the
  default font size, so shrinking the message leaves the text riding high against the icon, and the
  offset cannot be corrected from outside the template. Its icon and text sit in a **`Grid`, not a
  horizontal `StackPanel`** — a stack hands its children infinite width in the stacking direction, so
  `TextWrapping` never engages and the sentence is simply clipped at a narrow window.
- The budget counter is **right-aligned and outlined**. It is the same pill shape as the chips under
  it, so left-aligned and borderless it read as the first chip of the list rather than as its budget.

### Pinning from the plan

The plan is the second place overlay pins are made, because it is where the decision is actually
taken: the player reads what they still need and picks what to count in game from that same list.
Sending them to the Inventory page to find each name again was the manual step this removes.

- `ViewModels/PinToggle` is the affordance — pinned state, slot digit, `CanPin`, tooltip, command —
  shared by the inventory row and the gathering row. A second copy of those five members is the drift
  this repo treats as a review finding: the tooltip keys and the "full" rule would have to agree by
  hand forever.
- `ViewModels/OverlayPinsViewModel` is the "Overlay 3/10" budget and its reset, a **singleton** bound
  by both pages. One set of pins, one counter; two would only be two things to keep in step.
- `GatheringCardTemplate` is page-local; only the pin **styles** it uses are shared. The button is a
  plain `ui:Button` with a `DataTrigger` — never `ui:ToggleButton`, for the documented reason.
- The tenth pin greys out every remaining button (`CanPin`), and the cap itself stays in
  `PinnedItemsService`, so a full overlay refuses no matter which page asks.
- The slot digit uses `PinBadge*`, **not** `OverlaySlotBadge*`. The latter is drawn on the HUD's dark
  glass, which stays dark in the light theme too, so on a page it is a pale cyan on a pale card. Both
  pages got this wrong first — see `docs/design-system.md`, "Brand palette".
- The digit sits **beside the pin button**, and the pair keeps its own corner of the card — never
  leading the row. Leading, it was read as a second quantity ("how many I have" against the amount
  after it). Same grouping the inventory row uses, and the reason it is worth copying rather than
  inventing a new arrangement. As a chip this needed an explicit divider to separate the two
  meanings; a card separates them by position, so the divider went away with it.

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
  **and decodes** wins. Overrides are two-layered (`CatalogImageOverrideService`): the bundled
  `src/Resources/img-catalog-overrides.json` (in the repo, ships with the app — add shared image
  URLs there) plus the user's `%AppData%` file, which wins per key. An override value may also be a
  path instead of a URL: relative (`Resources/img/catalog/…`, an image bundled next to the exe) or
  absolute (the user's own disk) — see docs/data-pipeline.md and `src/Resources/img/README.md`.
  A `.webp` thumbnail on a machine without the WebP codec fails decode
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
(override URL → disk cache → decode; category icon placeholder until it loads). A value may equally be
a bundled `Resources/img/inventory/…` path (all 95 required items are covered that way or by URL, see
docs/inventory-images.md) or an absolute local path. The two-layer +
hot-reload mechanics are shared with reward overrides via `Services/OverrideFileSet`. Clicking a row
image that has one opens a full-window preview (`InventoryPreview.PreviewItemName`; see the overlay
pattern below).

**Readiness** compares requirements against inventory counts (`Models/InventoryReadiness`). On the
catalog card and detail page, each requirement chip is colored by `AvailabilityChipStyle`
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

## In-game overlay (Phase 4)

A second window: a small always-on-top HUD listing up to **ten user-pinned items** with their counts,
driven by global hotkeys so it never needs the mouse while Star Citizen holds the cursor.
`Models/OverlaySlots.MaxSlots` is the cap and `PinnedItemsService` (`pinned.json`, an ordered name
list) enforces it — the order **is** the slot assignment, and the slot number is what the hotkey digit
selects. Unpinning compacts the slots, so unpinning slot 3 renumbers everything below it.

**Layering.** `Views/OverlayWindow` is a plain `Window`, deliberately **not** `ui:FluentWindow`: the
Mica backdrop and title-bar chrome do not compose with a layered HUD drawn over another application.
Four settings are load-bearing and must be changed together or not at all:

- `AllowsTransparency="True"` is what makes WPF give the window **`WS_EX_LAYERED`**, which
  `WS_EX_TRANSPARENT` (click-through) requires. A future "drop transparency for perf" change would
  silently break click-through with no error anywhere.
- `ShowActivated="False"` plus **`WS_EX_NOACTIVATE`** — without both, showing the HUD steals focus and
  can minimise a fullscreen game.
- `WS_EX_TOOLWINDOW` keeps it out of Alt+Tab.
- Opacity lives in the **brush alpha**, never `Window.Opacity`: fading the window fades the text, and
  the text is the whole point at 1080p over a bright cockpit.

**The height always follows the content** (`SizeToContent="Height"`), and the saved height is
deliberately restored into `OverlayPlacement.Clamp` but **not** applied to the window. Restoring it
made the HUD a fixed size, so pinning a tenth item just clipped the tenth row — no scrollbar, no sign
anything was missing, the counter reading 10/10 while the overlay showed nine and the "0" badge never
appeared. With ten rows maximum the growth is bounded, so growing beats hiding. Only Left/Top/Width
are restored.

Corners are **square** (`RadiusOverlay` = 0) and must stay square: on an `AllowsTransparency` window a
rounded corner is anti-aliased against the transparent backdrop rather than against what is behind it,
which over a moving game reads as a ragged step rather than a curve.

**Placement.** Saved geometry outlives the monitor it was saved on, and a borderless, click-through,
Alt-Tab-invisible window restored onto a screen that is gone cannot be reached by any means short of
editing `settings.json`. `Models/OverlayPlacement.Clamp` is the single home for that rule (pure, so it
is unit-tested without WPF); Settings' **Reset position** is the second escape hatch. The window tracks
its bounds on `LocationChanged`/`OnRenderSizeChanged` rather than reading `RestoreBounds` on demand —
`Application.Shutdown` closes every window **before** raising `Exit`, so by the time the host's
`StopAsync` asks, the window is already gone and the geometry the user just dragged into place would be
lost on every exit.

**Hotkeys.** `Services/HotkeyService` owns the Win32 surface and nothing about the domain: it applies
what a `Models/HotkeyPlan` asks for and reports presses as `Pressed`. Two settings rows instead of
twenty: a modifier **pattern** (`Ctrl+Alt`) plus the slot digit, expanded per pinned slot by
`HotkeyPlan.Build`, which also detects our own collisions up front. Auto-repeat is deliberately kept —
holding the key to add twenty ore in one go is the gesture the overlay exists for.

**`RegisterHotKey` does not work in game, and this was learned the hard way.** It delivers through the
system hotkey table, and a foreground application can take that table out of service for everyone (Raw
Input's own `RIDEV_NOHOTKEYS` does exactly that). The symptom is the worst kind: registration succeeds,
no error is reported anywhere, the keys work on the desktop, and nothing arrives once Star Citizen has
focus. It was first misdiagnosed here as UIPI — Windows not delivering hotkeys to a lower-integrity
process — and an elevation workaround was built for it. **Running elevated does not fix it**, verified
in the field. Do not re-add that workaround.

So delivery is a strategy, `Services/IHotkeyBackend`:

- **`RawInputBackend` (default)** — a keyboard subscription with `RIDEV_INPUTSINK`. The system posts
  `WM_INPUT` straight to the window that asked, whoever is in front; the hotkey table is not involved.
- **`RegisterHotkeyBackend` (fallback)** — the old path, kept because it is the only one that claims a
  combination *exclusively*, and because Raw Input's subscription can in principle be refused.

`OverlaySettings.HotkeyBackend` (`Auto` / `RawInput` / `RegisterHotKey`) forces one for comparison;
`Auto` prefers Raw Input and falls back. The live backend is named in the log at startup — that line is
the first thing to ask for when someone reports dead hotkeys.

The sink is a dedicated hidden `HwndSource`, not MainWindow and not the overlay — WPF destroys windows
before `StopAsync`, so hooking a real window would make teardown depend on window-close ordering. It is
a normal top-level window that is never shown, **not** a message-only one: `WM_HOTKEY` would reach a
message-only window, but a Raw Input sink registered against one never receives `WM_INPUT` at all.

Four things that will bite:

- **Raw Input does not claim the combination.** Star Citizen still receives the same keystroke, so a
  binding the player also uses in game fires both. That is the opposite failure to `RegisterHotKey`'s
  and just as invisible — the Settings hint says so; do not remove it.
- **The sink sees every keystroke on the machine.** `HotkeyLookup.IsTrigger` runs first for that
  reason: unrelated input is dropped in one set lookup, before anything is read, stored or logged.
  Keep it that way.
- **Partial failure is never rolled back.** Under `RegisterHotKey` another application may already own
  one combination; abandoning the other nineteen over it would be worse. `HotkeyApplyResult` carries
  the losers and the Settings `InfoBar` names them. Under Raw Input nothing can fail, so `Failed` is
  always empty there — conflicts between two of *our* bindings still surface either way.
- **The click-through lockout.** If `ToggleInteractive` fails to register and the HUD starts
  click-through, there is no way back. `OverlayService.Initialize` forces interactive mode in exactly
  that case — this is the one failure that produces "the app is broken and I can't fix it".

**Testability.** `OverlayViewModel`/`OverlaySlotViewModel` hold no `Window` reference and
`OverlayService` reaches the window through `IOverlayWindow`, so the whole feature — hotkey to store to
readiness chip — is exercised by raising `IHotkeyService.Pressed`. See `docs/testing.md`.

Overlay rows use a **category glyph, not `ItemThumbTemplate`**: that template binds
`RelativeSource AncestorType=Page` and cannot resolve inside a `Window`. Widening it would push a
page-shaped assumption into a shared dictionary to serve one HUD.

**Anti-cheat:** no injection, no `SetWindowsHookEx`, no reading SC memory — a topmost layered window,
`RegisterHotKey` and a Raw Input sink are ordinary windowing APIs. Raw Input is *passive*: it observes
and cannot swallow, alter or inject a keystroke, which is exactly what separates it from a hook. Do not
"improve" this into a low-level keyboard hook, which is what EAC exists to stop. `uiAccess=true` is
also out: it requires signing *and* installation under `Program Files`, and the project is
portable-and-unsigned until SignPath lands.

### Inventory pins

The pin button is a plain `ui:Button` with a `DataTrigger` on the read-only `IsPinned` — **not**
`ui:ToggleButton`, same pitfall as the favourite star (a `ToggleButton` writes `IsChecked` locally on
click and kills the binding). An `Overlay N/10` counter sits beside the search box, because a cap that
only shows up when the eleventh pin silently fails is not a cap the user can plan around.

`InventoryItemViewModel.RefreshCount` is wired to `IInventoryStore.Changed` through **one**
subscription on `InventoryViewModel`, fanned onto the rows (the same shape as
`ContractListViewModel.OnInventoryChanged`). Both halves of its guard earn their place: the equality
check means 94 of 95 rows exit immediately on every hotkey press, and the `_suppressWrite` flag makes
"a store-driven refresh must not write back" a readable local invariant rather than something inferred
from the store's de-duplication.

### Hotkey capture

`Views/Controls/HotkeyBox` is a read-only WPF-UI `TextBox` that captures a combination instead of text,
in two modes: a full binding, or `PatternOnly` for the two modifier rows. It swallows **every** key
while focused, Tab included — a capture box that lets Tab escape cannot bind Tab and gives the user no
clue why. Alt-combinations arrive as `Key.System` with the real key in `SystemKey`. Esc/Delete clears a
row, which is how a hotkey is disabled: `HotkeyPlan.Build` skips an unparseable entry. Modifier-less
bindings are rejected — owning a bare "O" globally would swallow the key in every application on the
machine.

## Notification area (Phase 5)

`WPF-UI.Tray`'s `NotifyIcon`, declared in `MainWindow.xaml`; `ViewModels/TrayViewModel` holds the
menu commands **and** the minimize rule, reaching the window only through the `Services/ITrayHost`
seam — which is what makes the behaviour assertable without a `Window` (`tests/E2E/TrayScenarios`).

The window seam is the same idea as `OverlayService` / `IOverlayWindow`, but note where the
coordinator sits: the overlay's decisions live in a **service** because hotkeys drive them from
outside any view, while the tray's live in a **view model** because every one of them is a menu
item. `OnWindowStateChanged` is the single exception — called from the window, bound to nothing. If
the tray grows behaviour that is not a menu item (close-to-tray, start minimized, a balloon on
update), that is the point to split a `TrayService` out; do not keep piling window-lifecycle policy
into the menu's view model.

**Why a tray at all.** The window is not what the player looks at while playing — the overlay is —
so the shell spends most of a session out of the way. The menu is therefore three items: open the
window, toggle the HUD, quit.

Seven things that will bite:

- **`NotifyIcon` registers on its first `OnRender`, not on `Loaded`.** It has to stay in the visual
  tree; it draws nothing and takes no space, but move it into something collapsed and it silently
  never appears. `MainWindow.OnContentRendered` logs `IsRegistered` for exactly this reason —
  failure is reported nowhere else, and "no icon" is indistinguishable from "the user keeps it in
  the overflow flyout".
- **A `ContextMenu` is not in the visual tree**, so it inherits no `DataContext` from the window.
  `NotifyIcon` does forward its own, but only while the menu's is still null and only once it has
  one itself. `MainWindow` assigns `TrayMenu.DataContext = this` outright — the failure mode of
  getting this wrong is a menu of greyed-out items, with no binding error that reaches the user.
- **`FocusOnLeftClick` cannot bring back a hidden window.** WPF-UI's handler un-minimizes and
  activates, but never calls `Show()`. It is off, and `LeftClick` runs `ShowAppCommand` instead.
- **`Show()` first, `WindowState` second — the intuitive order silently does not work.** A window
  hidden to the tray is normally *also* still `Minimized`, because hiding is what the minimize did.
  Writing `WindowState = Normal` while it is hidden looks like the right way round and leaves the
  HWND **iconic**: WPF defers a state written to a hidden window, so `WindowState` then reports
  `Normal` and `IsVisible` reports `true` while the window is not on screen. What the user sees is a
  taskbar button appearing and nothing else — which is exactly how this shipped and was reported
  from the field. `Views/WindowRestore.Restore` is the three-line rule, split out of `MainWindow` so
  the ordering is pinned against a real `Window` in `tests/E2E/WindowRestoreTests`, and those tests
  assert on `IsIconic` rather than on the managed properties, because trusting the properties is
  what let it through. `MainWindow` remembers the last non-minimized state, so a window minimized
  from maximized comes back maximized.
- **Exit closes the shell, it does not call `Application.Shutdown()`.** `MainWindow.OnClosed` is the
  app's single exit trigger and the only path that reaches `StopAsync`, which flushes the inventory
  store. A menu item that shut down directly would drop the counts edited in the last few seconds
  in game.
- **Nothing disposes a control**, so `NotifyIcon` never unregisters on its own — `OnClosed` calls
  `Unregister()` explicitly. Left to the finalizer the icon lingers as a ghost that disappears only
  when the user hovers over it.
- **An Explorer restart takes the icon and does not give it back.** The shell rebuilds the
  notification area from nothing and broadcasts `TaskbarCreated` so applications can put their icons
  back; `Wpf.Ui.Tray` 4.3.0 does not handle that message anywhere, so the icon is gone for the rest
  of the session while `IsRegistered` still cheerfully says otherwise. `MainWindow.OnSourceInitialized`
  hooks the shell window and re-registers on the broadcast — `TrayManager.Register` repopulates the
  whole `NOTIFYICONDATA`, icon and tooltip included, so nothing has to be re-applied afterwards.
  Verified by broadcasting the message at a running app, not by restarting Explorer.

## Hiding is conditional on the icon existing

The failure this feature can produce that no other can: `Hide()` takes the window out of the taskbar
**and** out of Alt+Tab, so hiding into a notification area with no icon of ours leaves Task Manager
as the only way back to the app.

`ITrayHost.IsTrayAvailable` (`NotifyIcon.IsRegistered`) is therefore read immediately before every
hide, never cached, and a minimize with no icon simply minimizes normally. This is the same guard
`OverlayService.Initialize` makes when the interactive-mode hotkey fails to register — the setting is
a preference, not a promise. `TrayScenarios.A_window_is_never_hidden_when_there_is_no_icon_to_come_back_from`
pins it.

Minimize-to-tray is **off by default** (`AppSettings.MinimizeToTray`) and read live, at the moment
of the minimize, so the Settings switch takes effect without a restart. The icon itself is always
registered — with the shell in the tray, that menu is the only way to reach the overlay, so it must
be somewhere the user can rely on finding.

## Adaptive app icon

`MainWindow.UpdateAppIcon` follows `ApplicationThemeManager.Changed` (unsubscribed in `OnClosed`)
and feeds two surfaces from **different** assets — they are not interchangeable:

- **Title bar** (`TitleBarControl.Icon`) — the vector mark from `Resources/BrandIcons.xaml`
  (`AppMarkDark` / `AppMarkLight`), so it stays crisp at any DPI instead of downscaling a PNG.
  Follows the **app** theme: this app paints that surface.
- **Taskbar / Alt-Tab** (`Window.Icon`) — must stay a raster `BitmapImage` (WPF hands it to Win32
  as an `HICON`). Follows the **Windows shell** theme, read from `SystemUsesLightTheme`.
- **Notification area** (`TrayIcon.Icon`) — the same bitmap as the taskbar, by the same rule: both
  sit on a surface Windows paints. Assigning it after registration is supported; `NotifyIcon`
  re-sends the icon to the shell.

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
