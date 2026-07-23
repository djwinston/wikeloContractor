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
  here: the spec maps it to `ControlFillColorSecondaryBrush`, so `AvailabilityToBrushConverter`
  (and the detail page's neutral chips) resolve it straight from the WPF-UI theme — a hardcoded
  navy read as a dark hole on our Mica surface, and re-declaring a Fluent role would violate the
  one rule above.
- `ChipReward{Background,Border,Foreground}Brush` — the cyan reward role.
- `ChipBlueprint{Background,Border,Foreground}Brush` — no Fluent equivalent at all.
- `XpBadgeForegroundBrush` — the `+N XP` badge.
- `CompletedRow{Accent,Wash}Brush` — the completed catalog row's left marker and gradient wash.
- `ReputationBannerBrush` — the rank banner above the contract list.

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
| Radii | `RadiusChip` 6, `RadiusControl` 7, `RadiusCard` 10 |
| Spacing | `SpacingChipGap`, `SpacingRowPadding`, `SpacingCardPadding`, `SpacingPagePadding` |
| Sizes | `SizeCatalogThumb{Width,Height}` 120×80 (grown from the spec's 84×56 — see note), `SizeInventoryThumb` 46, `SizeProgressBarHeight` 6, `SizeNavRailWidth` 150, `SizeHitTarget` 28 |

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
- `BlueprintChipStyle` — the blueprint chip, fully self-colouring from the blueprint brand brushes.
  Same solid-bordered geometry as `ChipStyle`; the purple hue is the schematic cue. (It was a dashed
  outline earlier — a `Rectangle` with `StrokeDashArray`, since `Border` cannot dash — but the dash
  was dropped at the user's request in favour of a solid border.)
- `TagStyle` — the small outline marker that qualifies a title: the catalog row's contract category
  and the detail page's reward rarity. Set `Content` to a plain string; the style's font and colour
  setters inherit into the generated `TextBlock`, so no nested `TextBlock` is needed.
- `ReadinessBarStyle` — the requirement-coverage `ProgressBar`. Height/scale are fixed here; only
  `Width` stays with the caller (360 on a catalog row, 200 in the detail heading).

Whole templates (identical on both pages):

- `RequirementChipTemplate` — the `Name × Amount` chip. Both pages bind a `ViewModels/RequirementChip`
  list, so the template is shared outright; availability drives all four brushes through
  `ChipAvailabilityToBrush`.
- `ChipWrapPanel` — the `ItemsPanelTemplate` every chip list uses.

`Views/Controls/StatusBadge` is the COMPLETED / READY badge — a control, not markup, because the
icon-plus-label composition is identical on both pages and only `Symbol`, `Text` and `Role` vary.
`Role` (`Success` / `Caution`) picks the whole brush set, so a caller cannot mismatch the three
brushes. Its default style lives in `Chips.xaml` with everything else.

Named text styles live in `Typography.xaml`, not per page: `OverlineTextStyle` (9 px mono uppercase
label — `REWARDS`, tags, badge text) and `MonoCaptionStyle` (technical values — readiness counts,
the API version, reputation progress; override `Foreground` when the value carries a status colour).

Requirement chip colour is chosen by `Views/Converters/AvailabilityToBrushConverter` from
`Models/InventoryReadiness`' `RequirementAvailability`.

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
| Blueprint | `Molecule24` |
| Mark done (pending) / reopen | `Circle24` / `ArrowUndo24` |
| Completed badge | `Checkmark24` |
| Back | `ArrowLeft24` |
| Search | `Search24` |
| Prerequisites | `Branch24` |
| External wiki link | `Open24` |
| App update | `ArrowDownload24` |
| Nav: Catalog / Inventory / About | `DocumentBulletList` / `Box` / `Info` |
| Missing artwork placeholder | `Cube24` |

## Terminology

The UI says **XP** — `+250 XP`, `110 / 340 XP` — as a display mask over the reputation value from
the API. The badge always shows what the contract *awards*, on every row regardless of completion.

The **domain model stays `reputation`** (`Models/ReputationLevels`, `TotalReputation`,
`completed.json`): it matches the API and the in-game rank names. Do not rename the model to match
the label.

## Adding something new

1. Can a WPF-UI theme brush express it? Use that. Stop here.
2. Is it a derived tint/stroke of a theme colour, or a role Fluent has no concept of? Add the key
   to **both** `Brand.Dark.xaml` and `Brand.Light.xaml`, with a comment saying why Fluent could not
   cover it, and document it above.
3. Is it geometry or type? `Metrics.xaml` / `Typography.xaml`.
4. Verify in **System, Light and Dark**, and in **both en and uk** — Ukrainian strings are longer
   and expose fixed-width assumptions.
