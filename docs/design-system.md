# Design system

How this app is styled. Read before touching anything under `src/Views/` or `src/Resources/`.

Source of truth for the visual design is the Claude Design project in
`D:\dev\own_repo\starCitizen\wikeloMedia\Net_10_WPF_claude_design`:

| File | What it is |
|---|---|
| `Wikelo Design System.dc.html` | The spec — tokens, type, geometry, chips, control mapping, icons |
| `Wikelo DS · Catalog.dc.html` | The approved screens: **3a/3b** Catalog, **4a/4b** Details, **5a/5b** Inventory, **6a/6b** About (Dark/Light each) |
| `Wikelo Design Review.dc.html` | Earlier exploration. Useful for clarifying a single element; **not** the target |

The card-grid catalog variants (3c/3d/3e) are designed but deliberately not built — see PLAN.md
Phase 3.6 for the deferred list/cards toggle.

## The one rule

**The WPF-UI Fluent theme *is* the token layer. Do not rebuild it, do not hardcode a hex.**

Surfaces, text, control fills and status colours come from WPF-UI's own keys via
`{DynamicResource}`:

| Role | Key |
|---|---|
| Window background | `ApplicationBackgroundBrush` |
| Card / raised layer | `CardBackgroundFillColorDefaultBrush` |
| Control fill | `ControlFillColorDefaultBrush` |
| Divider / stroke | `ControlStrokeColorDefaultBrush` |
| Text primary / secondary | `TextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush` |
| Accent | `AccentFillColorDefaultBrush` |
| Success / caution | `SystemFillColorSuccessBrush` / `SystemFillColorCautionBrush` |

Re-declaring any of these in our own dictionary is a review finding: it breaks Mica, breaks the
user's accent, and drifts from the theme on the next WPF-UI update. The hex values written in the
design spec are **reference only** — they document what the theme already produces.

App-owned resources exist only where Fluent has no equivalent. That is a short list, below.

The same holds for **controls**: WPF-UI's `ControlsDictionary` styles the stock WPF controls, not
only its own `ui:` ones — `TabControl`/`TabItem` (the Favorites tabs) and `ProgressBar` among them.
Reach for the stock control first and check whether it already themes; writing a style for one that
does is the same mistake as re-declaring a brush. When a style *is* needed on a themed control,
`BasedOn="{StaticResource {x:Type X}}"` is mandatory — assigning a bare `Style` replaces the
implicit one outright and drops the control back to WPF's 2006 template (see `ReadinessBarStyle`).

### When a WPF-UI token has no answer: re-point it, scoped

Occasionally one of WPF-UI's own tokens is simply wrong for our surface. The Favorites tab strip is
the worked example, and the shape of the fix is the precedent:

- **Measured, not guessed.** The selected tab is `TabViewItemHeaderBackgroundSelected` =
  `SolidBackgroundFillColorTertiary` (`#282828`) on a `#202020` page that Mica lightens further, and
  its outline is `TabViewSelectedItemBorderBrush` = `CardStrokeColorDefault` = `#19000000` — **10 %
  black**, invisible on a dark surface by construction. The tab read as neither shape nor outline.
- **Fix the whole strip, not the part you noticed.** The same `CardStrokeColorDefault` also draws the
  rule under the tabs (the top edge of the content `Border`), so the first pass made the tab legible
  and left it floating over an invisible line. A themed control is a composition; check every edge of
  it in the failing theme before declaring it fixed.
- **Prefer a property over a resource override** where the template exposes one. That rule is
  `{TemplateBinding BorderBrush}`, so setting `BorderBrush` on the `TabControl` is enough — no
  re-pointed key, nothing scoped to reason about later.
- **Re-point the key in the control's own `Resources`** only for what the template does *not* expose,
  and never in a dictionary. The templates use `DynamicResource`, so a key declared on the
  `TabControl` is found by its descendants and by nothing else. That is an override in one place, not
  a redefinition of a shared token.
- **Point it at a recipe already proven on that screen** — here the card fill plus the control
  stroke, which the cards on the same page use — rather than inventing a value.
- **Colours through `DynamicResource`, never a brush alias.** `<SolidColorBrush Color="{DynamicResource
  CardBackgroundFillColorDefault}" />` keeps following the theme swap; a `StaticResource` alias to a
  `…Brush` key resolves once and would freeze the dark value into the light theme.
- **Check both themes on a real screenshot** before calling it done. The two are not symmetric: the
  fault above exists only in dark, because the token is a black tint.

### The one exception: light-theme legibility

Three Fluent keys **are** overridden, only in `Brand.Light.xaml`, because measurement on the real
light surface (`#F9F9F9`) showed them failing:

| Key | Fluent renders | We use | Contrast before → after |
|---|---|---|---|
| `TextFillColorSecondaryBrush` | `#A6A6A6` | `#5B5B5B` | 2.4:1 → **6.2:1** |
| `TextFillColorTertiaryBrush` | fainter still | `#6E6E6E` | → **4.7:1** |
| `ControlStrokeColorDefaultBrush` | `#EAEAEA` | `#B8B8B8` | 1.1:1 → 1.8:1 |

That one secondary key carries most of the app's quiet text — requirement chip names, the category
tag, `REWARDS`, the readiness count, About and Settings labels, the detail description and stat
labels — so three keys fixed the whole surface without touching a single page.

Rules for this exception:

- **Light only.** `Brand.Dark.xaml` deliberately does *not* redeclare them — this is the documented
  exception to "same keys in both files". Swapping the palette *removes* the light dictionary, so
  Fluent's dark values resolve again on their own; copying them into the dark file would mean
  maintaining a second palette that silently drifts from WPF-UI.
- **Opaque, not alpha over black.** They must render exactly as specified, including on top of the
  reputation banner and the completed-row wash.
- The stroke deliberately stops short of the 3:1 WCAG threshold for essential UI boundaries: a chip
  border is a container hint, not the carrier of meaning, and a 20-chip row at 3:1 reads as a
  spreadsheet. The *text* inside clears AA comfortably.
- Do not add a fourth key here without measuring first — take a real screenshot and sample it, the
  way these three were derived.

## Dictionaries

Merged in `src/App.xaml`, in this order — later dictionaries win on duplicate keys, which is how
`Typography.xaml` overrides WPF-UI's default font:

| Dictionary | Contents | Swapped at runtime? |
|---|---|---|
| `ui:ThemesDictionary` / `ui:ControlsDictionary` | WPF-UI Fluent theme | yes, by `ApplicationThemeManager` |
| `Resources/BrandIcons.xaml` | The app mark as a `DrawingImage` | no |
| `Resources/Typography.xaml` | Font families, type ramp, named text styles | no |
| `Resources/Metrics.xaml` | Radii, spacing, fixed sizes | no |
| `Resources/Theme/Brand.{Dark,Light}.xaml` | App-specific colours | **yes**, by `ApplicationHostService.ApplyTheme` |
| `Resources/Chips.xaml` | Shared chip/tag/badge chrome and cross-page templates | no |

Order matters beyond the font override: `Chips.xaml` is merged last because its styles reference
`Typography.xaml` and `Metrics.xaml` keys through `StaticResource`, which only resolves backwards
through the merge list.

### Brand palette (theme-swapped)

`Brand.Dark.xaml` and `Brand.Light.xaml` carry **identical key sets** with different values. A key
added to one must be added to the other — otherwise it silently resolves to nothing in that theme
and the element renders transparent. There is no compile-time check for this.

Light is a genuinely separate palette, not the dark one inverted: the accent is darker (`#0D95B5`
vs `#2FD0EE`) and chip fills are pale washes with saturated text rather than bright pills.

Keys, and why each one is not just a Fluent brush:

- `BrandAccentColor` — read back by `ApplyTheme` and handed to `ApplicationAccentColorManager`.
- `Chip{Caution,Success}{Background,Border,Foreground,Value}Brush` — the *partial* and *fully
  covered* requirement chips. Fluent ships `SystemFillColorSuccessBrush` etc. as *solid* fills; the
  design calls for a low-alpha tint plus a stronger border plus a readable foreground, which is
  three derived values Fluent does not provide. `…ValueBrush` is the brighter colour used for the
  quantity inside the chip. The **neutral** ("not in stock") requirement chip is deliberately *not*
  here: the spec maps it to `ControlFillColorSecondaryBrush`, so `AvailabilityChipStyle` takes it
  straight from the WPF-UI theme as its default state — a hardcoded navy read as a dark hole on our
  Mica surface, and re-declaring a Fluent role would violate the one rule above.
- `ChipReward{Background,Border,Foreground}Brush` — the cyan reward role.
- `ChipBlueprint{Background,Border,Foreground}Brush` — no Fluent equivalent at all.
- `XpBadgeForegroundBrush` — the `+N XP` badge.
- `CompletedRow{Accent,Wash}Brush` — "this line is done": a left marker and a gradient wash, shared
  by the completed catalog row and the fully gathered card on the Favorites plan. One role, one
  pair of keys — a second green for the second surface is how two shades of "done" get shipped.
- `ReputationBannerBrush` — the rank banner above the contract list.
- `Overlay{Background,Border,RowBackground,InteractiveBorder}Brush`,
  `OverlaySlotBadge{Background,Foreground}Brush` — the in-game HUD. Fluent has no "translucent panel
  floating over **another application**" role, so these are ours. Two rules that are easy to get
  wrong here:
  - **The light theme is not a white box.** What sits behind this window is Star Citizen, not the
    app's `#F9F9F9` surface, so the light-theme overlay is still dark glass — one shade lighter and
    a touch more transparent. Inverting it would put a white panel over a night-side cockpit.
  - **Alpha lives in the brush, never in `Window.Opacity`.** Fading the window fades the text with
    it, and the text is the only thing the overlay exists to show.
  Do **not** copy the `#CC000000` full-window scrim value into these: that literal is the project's
  one design-system violation and already appears in three pages — do not make it four.
- `PinBadge{Background,Foreground}Brush` — the overlay slot digit as an **app page** draws it
  (the inventory row, the Favorites gathering plan). A separate pair from `OverlaySlotBadge*` on
  purpose, and the reason is the trap above read from the other side: because the HUD is dark glass
  in *both* themes, its light-theme cyan (`#9BE6F6`) is tuned for a dark backdrop, and reusing it on
  the light page surface lands at roughly 1.3:1 — the badge is there, and unreadable. **Same role,
  different surface, separate keys.** This shipped wrong once on both pages because every colour
  choice was checked against the dark theme only; when picking a value, look at the element on
  `#F9F9F9` as well, not just on the dark surface.

### Theme application

`Services/ApplicationHostService.ApplyTheme(theme)` is the **single home** for theme changes;
startup and `SettingsViewModel` both call it. It does three things in order:

1. `ApplicationThemeManager.Apply(...)` with `updateAccent: false`. Passing `true` here would
   re-derive the accent from Windows on every theme change and fight step 3.
2. Swap `Brand.Light`/`Brand.Dark`, keyed off `ApplicationThemeManager.GetAppTheme()` — read the
   *applied* theme back, because `AppTheme.System` only resolves to a concrete theme after step 1.
3. `ApplicationAccentColorManager.Apply(BrandAccentColor, applied)` — the app always uses the
   Wikelo brand accent, not the Windows system accent.

The default theme is `AppTheme.System` (follows Windows), overridable in **Settings → Theme**. The
brand accent is not user-configurable — it is a fixed part of the app's identity.

## Typography

Two families, both **system fonts** — deliberately not embedded, so nothing is bundled into the
binary:

- **`AppFontFamily` = Segoe UI** — all UI text. The native Fluent face, and already WPF-UI's
  default `ContentControlThemeFontFamily`, so UI text needs **no** `FontFamily` at all — leave it
  unset and it inherits Segoe UI.
- **`MonoFontFamily` = Cascadia Mono, Consolas** — quantities, versions, counts like `9 / 20`,
  SKUs. Cascadia Mono ships with Windows 11; Consolas is the fallback (present on every Windows).

Set `FontFamily` explicitly on an element only to opt into `MonoFontFamily`. The type ramp
(`FontSize*` keys) is size-only; weight is set per usage.

> The design spec was authored with Inter + JetBrains Mono. We map those to the nearest system
> faces (Segoe UI / Cascadia Mono) rather than embedding, a deliberate dependency-weight decision.
> If a future revision must match the spec typefaces exactly, embed the static weights as
> `<Resource>` and point these two keys at `pack://…/#Family Name` — and note WPF cannot drive
> variable-font axes, so use the static-weight files, and verify the family resolves every weight
> with `Fonts.GetFontFamilies(dir)` (many free fonts register each weight as its own family).

### Overline labels and casing

Small uppercase labels (`REWARDS`, `WEAPONS`, `COMPONENTS`, inventory section headers) are a
deliberate device. WPF has no `text-transform`, so casing has to come from somewhere — and the
choice depends on whether the string is *only* ever an overline:

- **Uppercase in the resource** when the string appears nowhere else
  (`Details_Loadout_Weapons` = `WEAPONS`, `Catalog_RewardsLabel` = `REWARDS`).
- **`ToUpperConverter`** when the same string is also shown in mixed case elsewhere. Inventory
  category names are section headers *and* filter dropdown entries, so uppercasing the resource
  would wreck the dropdown.

Overline labels use `TextFillColorSecondaryBrush`, not tertiary: at 9 px the tertiary brush is
effectively invisible on the light theme's surface.

## Geometry

From `Metrics.xaml`. Use these instead of re-typing literals — a value that appears in two XAML
files belongs here.

| Group | Keys |
|---|---|
| Radii | `RadiusChip` 6, `RadiusControl` 7, `RadiusCard` 10, `RadiusOverlay` **0** (square on purpose — see below) |
| Spacing | `SpacingChipGap`, `SpacingRowPadding`, `SpacingCardPadding`, `SpacingPagePadding` |
| Sizes | `SizeCatalogThumb{Width,Height}` 120×80 (grown from the spec's 84×56 — see note), `SizeInventoryThumb` 46, `SizeProgressBarHeight` 6, `SizeNavRailWidth` 150, `SizeHitTarget` 28 |
| Overlay | `SizeOverlayDefaultWidth` 280, `SizeOverlayMin{Width,Height}` 220×120, `SizeOverlaySlotBadge` 22 |

The overlay minimums are not decoration: `OverlayPlacement.Clamp` grows a restored window up to them,
so a hand-edited `settings.json` cannot shrink the HUD to an unclickable sliver.

`RadiusOverlay` is the one radius that is **0**, and it is not an oversight. Every other surface sits
on an opaque page background, where a rounded corner anti-aliases against a known colour. The overlay
sits on an `AllowsTransparency` window, so its corner is anti-aliased against the *transparent*
backdrop instead of against the game behind it — over moving footage that reads as a ragged step, not
a curve. Confirmed in game, not theorised. Do not "restore consistency" by giving it a radius.

The catalog thumbnail is deliberately larger than the design spec's 84×56. The mockup rows are
short (a couple of requirement chips); real contracts carry many, so the row is far taller and the
84×56 thumb left a tall empty gutter beside it. Grown to 120×80 (same 3:2 ratio) at the user's
request. The rounded `Grid.Clip` rect in `CatalogPage.xaml` is hand-matched to these numbers — keep
the two in sync if the size changes again.

## Chips

A chip is a small labelled container. Requirement chips **wrap, never truncate** (`WrapPanel`), and
every requirement amount is prefixed `×`.

`Resources/Chips.xaml` is the single home for anything two pages render the same way. **The rule:
if the catalog row and the detail page draw the same thing, it lives here as a whole template; only
genuinely page-specific content stays inline and wraps itself in one of the chrome styles.** These
had drifted into per-page copies once already — that is what this dictionary exists to prevent.

Chrome styles (caller supplies the content):

- `ChipStyle` — solid-bordered chip. Geometry only; the caller sets `Background`, `BorderBrush`
  and `Foreground`, because those vary by availability.
- `AvailabilityChipStyle` — `ChipStyle` plus those three brushes, driven off the DataContext's
  `Availability` by `Style.Triggers` + `{DynamicResource}` (see "Availability colour is a trigger,
  never a converter" below — this is the only shape that survives a runtime theme swap). The single
  home for "availability paints a chip": the catalog row and the detail page both read it from here,
  so a fourth availability band is one edit rather than a hunt through the pages.
- `AvailabilityValueStyle` — the same mapping for the quantity `Run` inside the chip, which takes the
  stronger `…ValueBrush` of each pair. Only ever used **with** the chip fill behind it; these brushes
  are picked to read against `Chip*BackgroundBrush` and are a legibility problem on any other surface.
- `PinSlotBadgeStyle` / `PinSlotDigitStyle` / `PinButtonStyle` — the overlay pin affordance: the
  slot digit and the button that sets it, on the inventory row and the Favorites gathering card.
  **Styles rather than one template on purpose** — the sites differ only in badge geometry, and a
  local value beats a style setter, so each page overrides size and spacing and nothing else. Their
  contract is a `PinToggle Pin` on the DataContext.
  `PinButtonStyle` carries `x:Shared="False"`, and that is not decoration: `IconElement` derives
  from `FrameworkElement` and `{ui:SymbolIcon}` is evaluated once per Style, so a shared style hands
  the *same* icon element to all ~120 pin buttons (measured: 20 rows → 1 instance shared, 20 with
  the flag). WPF-UI happens not to parent it today, so it still renders — that is luck, not a
  contract. **Any dictionary-level style with an `Icon` setter needs this flag** — `FavoriteStarStyle`
  and `CompletionToggleStyle` below carry it for the same reason. An `Icon` setter that sits inline
  in a `DataTemplate` does not need it, because the template re-instantiates on its own.
- `FavoriteStarStyle` / `CompletionToggleStyle` — the two per-contract actions, on the catalog row
  and the contract detail page. See "Shared controls" below for what they replaced.
- `BlueprintChipStyle` — the blueprint chip, fully self-colouring from the blueprint brand brushes.
  Same solid-bordered geometry as `ChipStyle`; the purple hue is the schematic cue. (It was a dashed
  outline earlier — a `Rectangle` with `StrokeDashArray`, since `Border` cannot dash — but the dash
  was dropped at the user's request in favour of a solid border.)
- `TagStyle` — the small outline marker that qualifies a title: the catalog row's contract category
  and the detail page's reward rarity. Set `Content` to a plain string; the style's font and colour
  setters inherit into the generated `TextBlock`, so no nested `TextBlock` is needed.
- `ReadinessBarStyle` — the app's 0..1 meter, named for its first use (requirement coverage) but
  worn by every progress bar of that shape, the catalog's rank banner included. Height/scale are
  fixed here; only `Width` stays with the caller (360 on a catalog row, 200 in the detail heading,
  the card width on a gathering card, the column on the rank banner), so a meter reads the same
  wherever it is shown.

Whole templates (identical on both pages):

- `RequirementChipTemplate` — the `Name × Amount` chip. Both pages bind a `ViewModels/RequirementChip`
  list, so the template is shared outright; it wears `AvailabilityChipStyle` for the chrome and only
  the value `Run` sets a brush of its own.
- `OverlayPinBudgetTemplate` — the "Overlay 3/10" counter and its reset button, on every page that
  offers pins. `OverlayPinsViewModel` is a singleton so the two pages cannot disagree on the number;
  this is the other half of that argument. DataContext is the view model itself, and the outer
  container stays with the caller (the inventory grid puts it in the filter bar, the gathering tab
  right-aligns it over the cards).
- `ChipWrapPanel` — the `ItemsPanelTemplate` every chip list uses.

Two layout rules the gathering tab paid for, worth stating once:

- **A width-driven grid is a `UniformGrid` whose `Columns` come from
  `Views/Converters/WidthToColumnsConverter`** (minimum column width as `ConverterParameter`), bound
  to the hosting `ItemsControl.ActualWidth`. A `WrapPanel` with a fixed `ItemWidth` needs no code at
  all and was rejected on looks: items keep their width and leave a ragged gutter on the right.
- **Wrapping text does not belong in a horizontal `StackPanel`.** A stack hands its children infinite
  width in the stacking direction, so `TextWrapping` never engages and the text is clipped instead —
  invisible until the window is narrow enough. Use a `Grid` with an `Auto` and a `*` column.

Named text styles live in `Typography.xaml`, not per page: `OverlineTextStyle` (9 px mono uppercase
label — `REWARDS`, tags, badge text) and `MonoCaptionStyle` (technical values — readiness counts,
the API version, reputation progress; override `Foreground` when the value carries a status colour).

Requirement chip colour is chosen from `Models/InventoryReadiness`' `RequirementAvailability` by
`AvailabilityChipStyle` (chrome) and `AvailabilityValueStyle` (the quantity).

### Availability colour is a trigger, never a converter

This was a value converter — `Availability` → `Brush`, with the part chosen by `ConverterParameter`
— and that is a **theme-swap trap** worth stating once, because it applies to any converter that
resolves a resource key:

> A converter runs when its binding evaluates. It returns a resolved `Brush`, and **nothing re-runs
> it when a resource dictionary is swapped** — the binding's source has not changed. So after a
> runtime light/dark flip, every converter-supplied colour stays on the old palette while the
> `DynamicResource`-supplied chrome around it moves.

The symptom is a mismatch that no single element explains: on the Favorites gathering card the
shortfall number kept its dark-theme brush (pale amber, or plain white for the neutral band) against
a live light card, i.e. was invisible. Chips hid the same fault for months because a chip's fill,
border and text all came from the converter — they went stale *together* and stayed internally
legible, just in the wrong palette.

The replacement is `Style.Triggers` + `{DynamicResource}`, which re-resolve on the swap. **Do not
resolve a theme resource inside an `IValueConverter`.** If a mapping needs the theme, it belongs in
triggers.

A second lesson from the same episode: **`Chip*ForegroundBrush` / `Chip*ValueBrush` are
chip-context brushes.** They are picked to read against `Chip*BackgroundBrush`, not against a card or
the page. Used as bare text on any other surface they are a legibility problem in one theme or the
other, whatever the swap does. If a value outside a chip wants a status colour, either give it the
chip (fill, border and text together) or give it no colour at all — the gathering card took the
second option and lost nothing.

## Shared controls

**If two screens render the same thing, it is built once and reused. A second copy is a review
finding — not a style preference.**

The reason is not tidiness, it is drift, and this project has already paid for it twice. The
favourite star and the completion toggle were each written separately on the catalog row and the
contract detail page, and the copies diverged: the detail page's toggle showed `Checkmark24` for
"not done yet" while the row showed `Circle24`, so the *same state* read as two different things
depending on which screen you reached it from. Nobody changed anything to cause that — the second
copy was simply written on a different day. Two copies of a control do not stay identical; they
stay identical only for as long as someone remembers to edit both.

Where a shared thing goes, in order of how much it carries:

| What is repeated | Where it lives | Example |
|---|---|---|
| Chrome only — brushes, radius, padding, a state trigger | a `Style` in `Resources/Chips.xaml` | `ChipStyle`, `FavoriteStarStyle`, `PinButtonStyle` |
| Content **and** chrome, identical on every site | a `DataTemplate` in `Resources/*.xaml` | `RequirementChipTemplate`, `ContractCardTemplate`, `ItemThumbTemplate` |
| Markup **plus** behaviour or invariants a caller could get wrong | a control in `Views/Controls/` | `StatusBadge`, `HotkeyBox`, `MarkdownViewer` |
| A decision, not a visual | a model or service, never the view | `Models/ContractFilter`, `Models/InventoryReadiness` |

What exists today, and what each one's callers must provide:

| Shared control | Used by | Contract |
|---|---|---|
| `FavoriteStarStyle` | catalog row, contract detail header | `IsFavorite` + `ToggleFavoriteCommand` on the DataContext |
| `CompletionToggleStyle` | catalog row, contract detail header | `IsCompleted` + `ToggleCompletedCommand` + `ShowCompletionToggle` |
| `PinSlotBadgeStyle` / `PinSlotDigitStyle` / `PinButtonStyle` | inventory row, gathering card | a `PinToggle Pin` on the DataContext |
| `RequirementChipTemplate` | catalog row, contract detail | a `ViewModels/RequirementChip` |
| `OverlayPinBudgetTemplate` | inventory grid, gathering tab | the `OverlayPinsViewModel` itself |
| `ContractCardTemplate` | catalog page, favorites page | a `ContractCardViewModel` |
| `ItemThumbTemplate` | inventory grid, sourcing grid | `Name` + `Category`, and an `OpenPreviewCommand` on the page |
| `StatusBadge` | catalog row, contract detail | `Role` + `Symbol` + `Text` |
| `ReadinessBarStyle` | catalog row, contract detail, gathering card | a value in [0, 1]; caller sets `Width` |

Three rules that keep this workable:

- **A style's contract is its DataContext.** `FavoriteStarStyle` binds `IsFavorite` and
  `ToggleFavoriteCommand` by name, so `ContractCardViewModel` and `ContractDetailViewModel` keep
  those members aligned, and the detail page scopes the button with `DataContext="{Binding ViewModel}"`
  so the same names resolve. That alignment is the price of one control instead of two, and it is
  the cheaper side of the trade.
- **Geometry stays with the caller.** A local value beats a style setter, so the sites can differ in
  padding, size and appearance without a second style: the row's completion toggle is a compact
  ghost button, the detail page's a full-size one, and only the glyph pair, the label and the
  command are shared. When *only* geometry differs, prefer styles over one template for exactly this
  reason (the pin trio).
- **Behaviour that can be got wrong belongs in a control, not a style.** `StatusBadge` is a control
  because `Role` picks all three brushes at once — a caller cannot mismatch them. A new status
  marker adds a `Role`; it does not hand-roll a `Border`.

Extracting a duplicate into `Chips.xaml` has one trap: **a dictionary-level `Style` with an `Icon`
setter needs `x:Shared="False"`** — see the `PinButtonStyle` note above. Inline in a `DataTemplate`
it does not, which is why the duplicates that lived in templates worked before they were shared.

## Icons

`ui:SymbolIcon` with `SymbolRegular` glyphs only — no bitmap icons, no bespoke paths.

A **state** toggle keeps one glyph and switches `Filled`; it does not swap to a different glyph.
`ui:SymbolIcon` (and its markup extension) take `Filled="True"` for the solid variant — that is the
outline/solid pair Fluent intends. The `…Off` glyphs are struck-through ("this feature is disabled")
and belong to mute/disable actions, never to an "unset" state.

| Purpose | Symbol |
|---|---|
| Favourite | `Star28` outline (not starred) → `Star28` `Filled="True"` + `FavoriteStarBrush` (starred) |
| Data status / sync | `CloudCheckmark16` |
| Blueprint | `Ribbon24` (detail page only — see below) |
| Mark done (pending) / reopen | `Circle24` / `ArrowUndo24` |
| Completed badge | `Checkmark24` |
| Back | `ArrowLeft24` |
| Search | `Search24` |
| Prerequisites | `Branch24` |
| External wiki link | `Open24` |
| App update | `ArrowDownload24` |
| Nav: Catalog / Favourites / Inventory / Where to Find | `ClipboardTaskListLtr24` / `Star28` / `Box24` / `MyLocation16` |
| Nav: Settings / About | `Settings24` / `Info24` |
| Missing artwork placeholder | `Cube24` |

The blueprint glyph was the last one left open (`ChannelShare16 ?` in the spec, `Molecule24` in the
code — the decision was never actually taken). It is `Ribbon24`, and it appears in exactly **one**
place: the Blueprints chip on the contract detail page. The catalog card's blueprint chip is the
`BP · name` abbreviation and stays text-only — at that size a 13 px glyph before a two-word chip is
noise, and the card already carries a row of reward pills beside it.

`Ribbon24` rather than `Reward24` from the same Fluent family: the detail page renders a **Rewards**
section directly below these chips, and a glyph literally named for it on a chip that is not one of
them is a collision worth avoiding. Not `DocumentRibbon24` either — a document plus a seal has too
many strokes to survive 13 px.

## Terminology

The UI says **XP** — `+250 XP`, `110 / 340 XP` — as a display mask over the reputation value from
the API. The badge always shows what the contract *awards*, on every row regardless of completion.

The **domain model stays `reputation`** (`Models/ReputationLevels`, `TotalReputation`,
`completed.json`): it matches the API and the in-game rank names. Do not rename the model to match
the label. The point of the mask is that if the game ever renames what it shows, **only the
resource strings change** — so every identifier named after the label is one more place to chase.
`ReputationStatus.TotalXp` was exactly that and is now `TotalReputation`; its own XML doc had said
"accumulated Wikelo reputation" the whole time.

The mask lives in the localization strings and nowhere else: `Catalog_XpBadge` (`+{0} XP`),
`Reputation_Progress` (`{0} / {1} XP`) and `Reputation_Max` (`{0} XP · Max rank`), identical keys in
both dictionaries. The rank names (`Reputation_Tier_*`) stay **English in Ukrainian too** — they are
in-game standing names, like item names.

**A contract that awards nothing still shows `+0 XP`.** The API sends an explicit null for top-rank
trades and 12 of the 67 contracts in the 4.9.0 catalog are in that group, so this is a real state,
not a parse failure. Hiding the badge at zero was considered and rejected: an absent badge and a row
whose data has not arrived look identical, and "+0" is the honest answer to "what does this pay". The
`+0 XP` in design prototype 3a was placeholder data and is unrelated to this decision.

## Adding something new

1. Can a WPF-UI theme brush express it? Use that. Stop here.
2. Is it a derived tint/stroke of a theme colour, or a role Fluent has no concept of? Add the key
   to **both** `Brand.Dark.xaml` and `Brand.Light.xaml`, with a comment saying why Fluent could not
   cover it, and document it above.
3. Is it geometry or type? `Metrics.xaml` / `Typography.xaml`.
4. Verify in **System, Light and Dark**, and in **both en and uk** — Ukrainian strings are longer
   and expose fixed-width assumptions.
