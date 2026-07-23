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
- [x] Pin exact package versions in both csproj files (was floating `4.*` / `8.*` / `10.*`).
      Velopack is pinned to `1.2.0` to match the `vpk` CLI version in `release.yml` — bump both together.
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
- [ ] "Tracked" flag on a contract (persisted) — **superseded by Phase 2.5 (Favorites)**, which is
      the same idea (a persisted per-contract flag) with a page of its own. Tracked here for history;
      do not implement a second flag
- [x] **Readiness indicator (needs Inventory)**: on the catalog card and detail page, each *Required
      items* chip is colored by availability vs. the inventory — default (none), caution tint (partial),
      success tint (full) — plus a "Ready to turn in" badge and an "X / Y satisfied" count. Computed
      state lives on `ContractCardViewModel` (catalog) and `ContractDetailViewModel` (detail); the math
      is `Models/InventoryReadiness`, chips are `ViewModels/RequirementChip`, color via
      `Views/Converters/AvailabilityToBrushConverter`. Both VMs refresh on `IInventoryStore.Changed`.
      Phase 3.6 added `ContractReadiness.Fraction` (→ `ContractCardViewModel.ReadinessFraction`)
      driving the per-row progress bar; a completed contract reads 1 regardless of what is left in
      the inventory, since the items were spent on it

## Phase 2.5 — Favorites

A **Favorites** page that is the catalog filtered to flagged contracts. Deliberately *not* a second
catalog implementation: the same cards, the same filters, the same detail page — only the source
collection differs.

- [x] **Store**: `IFavoritesService` → `favorites.json` (UUID set). Follows the established
      per-service JSON store shape (`AppStorage.Root`/`JsonOptions`, load with
      `try/catch (JsonException)`, atomic tmp + `File.Move`, a `Changed` event) — same as
      `Services/CompletionService` and `Services/InventoryStore`. Registered in `App.xaml.cs`
      and loaded at startup by `ApplicationHostService`.
- [x] **Toggle on the card**: a star `ui:Button` on `ContractCardViewModel` (`IsFavorite`,
      `ToggleFavoriteCommand`), in the title line next to the category tag. The card VM already
      re-reads service state via `Refresh*` on a `Changed` event — favorites follow that pattern,
      no new mechanism. Deliberately a `ui:Button` with a state trigger, **not** a `ui:ToggleButton`:
      `IsFavorite` is computed from the service, and a ToggleButton writes `IsChecked` locally on
      click, which would replace the binding.
- [x] **Toggle on the detail page**: same command surfaced on `ContractDetailViewModel`, so the flag
      can be set from either place and both stay in sync through `IFavoritesService.Changed`.
- [x] **Page + nav item**: `FavoritesPage` / `FavoritesViewModel` after Catalog in
      `Views/MainWindow.xaml`; new `Nav_Favorites` key in **both** `Strings.en.xaml` and
      `Strings.uk.xaml`. Opening a card reuses the existing `ContractDetailPage` via
      `NavigateWithHierarchy` — no new detail view.
- [x] **Shared filtering**: extracted into `ViewModels/ContractListViewModel` (the card list, the
      collection view, the filters, the empty state and `OpenDetails`), which both `CatalogViewModel`
      and `FavoritesViewModel` derive from — they differ only in `RebuildFromCatalog`. The matching
      decision itself is `Models/ContractFilter`, a pure record with no UI notion, so it is unit
      testable without a WPF `Application`. The row `DataTemplate` moved to
      `Resources/ContractCard.xaml` (merged after `Chips.xaml`) and is referenced by both pages.
- [x] **Empty state**: distinct copy for "no favorites yet" (`Favorites_Empty`, driven by
      `FavoritesViewModel.HasNoFavorites`) vs. the catalog's "filters matched nothing"
      (`Catalog_Empty`, driven by the base's `IsEmpty`). Mutually exclusive by construction.
- [x] Unit tests: `FavoritesServiceTests` (round-trip, unflagging, `Changed` only on a real change,
      corrupt-file recovery + rewrite) and `ContractFilterTests` (search over title/description/
      rewards, category incl. the enriched multi-category case, resource, and all criteria combined).
- [ ] Aggregation: combined resource list across all favorited contracts (what to still gather) —
      a natural follow-up once the page exists.

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

- [x] New nav page **after Inventory** (`SourcingPage`, nav key `Nav_Sourcing`), over the same item set as
      the inventory (distinct required items, shared `Models/InventoryCategoryClassifier`, grouped list with
      search + category filter, per-item image). Counter-free — quantities stay on the inventory page. The
      46 × 56 thumb markup moved to `Resources/ItemThumb.xaml` and is now shared with `InventoryPage`.
      Search matches the note text as well as the item name.
- [x] **Knowledge base as Markdown in the repo** — decided against fetching from GitHub at runtime:
      that breaks the offline requirement and needs a second cache layer for text that changes once a
      patch. Instead `docs/sourcing/{slug}.md` is authored in-repo (natural for review + PRs) and
      **shipped inside the release**, copied to `Resources/sourcing/` next to the exe by a csproj
      `Content` glob with a `Link`. `%AppData%\WikeloContractor\sourcing\*.md` layers over it and wins
      per item, so local notes survive updates.
- [x] **One file per item, summary and guide together.** Front matter `name` (the lookup key — the file
      name is cosmetic) + `summary` (the card's one-liner); the body is the "How to obtain" guide.
      `Services/SourcingGuideService` scans both folders and indexes by `name`. This replaced the
      earlier `sourcing-notes.json` + `SourcingNotesService`, which are gone.
      Authoring rules live in `docs/sourcing/README.md`.
- [x] **Markdown rendering**: `Models/MarkdownDocument` is a pure, total parser for a deliberately
      small subset (`##`/`###`, `-` bullets, `1.` steps, inline bold/italic/code/link, `<!-- -->`
      stripped) — no WPF, so it is unit-tested directly. `Views/Controls/MarkdownViewer` renders the
      blocks as `TextBlock`s using the design tokens. Not a `FlowDocument`: WPF-UI does not theme
      `FlowDocumentScrollViewer`, which would fight the token layer. Only `http(s)` links are launched.
- [x] **Detail page** (`SourcingDetailPage`): back button, larger art, name + category tag, the short
      note, then the rendered guide — falling back to `Sourcing_GuidePlaceholder` while an entry is a stub.
- [ ] **Fill in the guides.** 95 files exist, one per required item; **33 carry a summary** from the
      community sheet and only `carinite.md` has a written body (the worked example). The rest are
      stubs by design — do not invent drop locations, an empty section is correct.
- [ ] Still to do from the original scope: a link to the wiki (`web_url`) when available, and a
      per-item deep-link into cstone Finder (investigate whether it supports a query URL).
- [ ] The sheet also covers items the 4.9.0 catalog never requires (Atlasium, Janalite, Picoball,
      Scourge Railgun, Advocacy Badge, Finley plushie, Wowblast pistol, Xanthule Helmet/Suit). No files
      were authored for them — add one if a patch turns any into a requirement.

## Phase 3.6 — Design system

Source: `D:\dev\own_repo\starCitizen\wikeloMedia\Net_10_WPF_claude_design` — *Wikelo Design System*
(spec), *Wikelo Design Review* (rationale, options 1a–2b), *Wikelo DS · Catalog* (working prototypes).
Authored against this exact stack. **Do this before the brand assets below** — the mark draws from
the same palette.

**Integration decision, already settled by the spec**: the design system **layers over** the WPF-UI
Fluent theme, it does not replace it. Surfaces, text and control fills bind to the theme's own keys
via `DynamicResource` (`ApplicationBackgroundBrush`, `CardBackgroundFillColorDefaultBrush`,
`TextFillColorPrimaryBrush`, `SystemFillColorSuccessBrush`, …). Hex values in the spec are reference
only — never hardcoded. Only genuinely app-specific keys are registered in our own
`ResourceDictionary`. So there is **no** `Tokens.Light/Dark.xaml` layer to build; the theme is the
token layer. Mica and the Fluent look stay.

**Approved screens** (`Wikelo DS · Catalog.dc.html`) — build exactly these, Dark **and** Light:
**3a/3b** Catalog (dense list), **4a/4b** Contract details, **5a/5b** Inventory, **6a/6b** About.
The card-grid variants (3c/3d/3e) are **not** built now — see the deferred toggle at the end of this
phase. Screens 1–2 in `Wikelo Design Review.dc.html` are earlier exploration, useful only for
clarifying an individual element.

- [x] **Brand accent**: cyan `#2FD0EE` (dark) / `#0D95B5` (light) applied via
      `ApplicationAccentColorManager.Apply(...)` next to `ApplicationThemeManager` in
      `Services/ApplicationHostService.ApplyTheme`, picked per resolved theme. Always on — a fixed
      part of the app identity, not user-configurable (the earlier opt-out toggle was removed at the
      user's request). Default theme is `AppTheme.System`.
- [x] **Fonts**: the spec calls for **Inter** (UI) + **JetBrains Mono**. Rather than embed ~3 MB of
      fonts, mapped to the nearest **system** faces (user's call): `AppFontFamily` = Segoe UI (the
      native Fluent face, also WPF-UI's default) and `MonoFontFamily` = Cascadia Mono, Consolas
      (Cascadia ships with Win11, Consolas is the universal fallback). Nothing bundled. The embed
      route is documented in `docs/design-system.md` as the fallback if the exact spec faces are
      ever required. *(Initially embedded Inter + JetBrains Mono; reverted to system fonts.)*
- [x] **Custom keys** — `Resources/Theme/Brand.{Dark,Light}.xaml`, swapped as a pair by `ApplyTheme`:
      chip fills/borders/foregrounds per availability role, the reward role, the blueprint role, the
      XP badge, the completed-row wash, the reputation banner. Replaced `BlueprintChipBackgroundBrush`
      (`#0067C0` solid) — the blueprint chip is now a *dashed purple outline*, and since WPF `Border`
      cannot dash, the shared `Resources/Chips.xaml` templates a `Rectangle` with `StrokeDashArray`.
      Blueprint glyph settled: `Molecule24`, already in use and matching the prototype's node graph.
- [x] **Availability chips → brand palette**: `Views/Converters/AvailabilityToBrushConverter` is now
      parameterized (`ConverterParameter=Background|Border|Foreground|Value`) since a chip needs four
      brushes per state. Requirement chips never truncate — they wrap (`WrapPanel`) — and every
      requirement amount is prefixed `×` *(the `×` prefix lands with the Catalog screen)*.
- [x] **Geometry scale** as resources so padding stops being re-typed per page: radius chip 6 /
      control 7–8 / card 10–12, hit target ≥ 28; gaps chip 6, row 15, card 14–16, page 20/26;
      fixed sizes thumb 84×56 (catalog row) and 46×46 (inventory), progress bar height 6,
      nav rail 150.
- [x] **Catalog → dense list (3a/3b)** — a layout change, not just a restyle. Full-width rows over
      hairline top-borders, 84 × 56 thumb, then per row: title + category chip + favourite star,
      a `ui:ProgressBar` (h=6, max-width 360) with a `9/20` mono label, the wrapped requirement
      chips, a `REWARDS` label with reward chips (+ dashed `BP · …` chip), and a right-hand column
      with `+N XP` and the completion toggle. A completed row gets a 2 px success left border and a
      success→transparent gradient wash, plus an inline `✓ COMPLETED` chip next to the title.
      **Note**: rows are variable-height — a 20-requirement contract produces a very tall row. That
      is what the design intends; do not truncate (requirement chips must wrap, never clip).
- [x] **Contract details (4a/4b)**: back button + title + favourite star in the header, version with
      `CloudCheckmark16` and a `Mark completed` ↔ `Reopen` toggle on the right; meta chips row
      (category / `+250 XP` / `Has prerequisites` / `Open on the wiki ↗`); `Required items` heading
      with an inline 200 px progress bar and `10 / 11`; then Blueprints (dashed purple) and a Rewards
      panel with a cyan `#2FD0EE` 22 %-alpha border, 168 × 118 art, stat chips, `WEAPONS` and
      `COMPONENTS` chip groups under mono section labels.
- [x] **Inventory (5a/5b)**: `My Inventory` title, search + category dropdown, uppercase mono section
      header per category, then rows as `#161B23` cards (radius 10, padding 12/16) with a 46 × 46
      thumb — a `Cube24`-style placeholder glyph when there is no art — the name, and a right-aligned
      96 × 34 `ui:NumberBox` whose value is mono. **A `0` value renders muted**, and its decrement
      chevron is dimmed too.
- [x] **About (6a/6b)**: hero panel, version, check-for-updates, API attribution, disclaimer.
      The hero is **composed in XAML**, not a bitmap: 6a is dark and 6b is light, so one PNG cannot
      serve both and two would need a runtime image swap. Built from the theme-swapped
      `AboutHeroBackgroundBrush` plus the vector mark, it is theme-correct by construction and picks
      up new artwork automatically when `BrandIcons.xaml` is regenerated in Phase 3.7 — which also
      means **`src/Assets/about-hero.png` is no longer referenced**; drop it from the deliverables.
- [ ] **Shared controls** the screens imply: `ui:CardControl` per row, `ui:ProgressBar` (h=6),
      completion as a **neutral** toggle (`Checkmark24` ↔ `ArrowUndo24`, *not* `Appearance="Success"`),
      the inline `COMPLETED` chip, and a `ui:ToggleButton` star for favourites (`Star28`) — the exact
      Phase 2.5 control, so build it once here.
- [x] **Nav rail**: 150 px, `PaneDisplayMode="Left"`, active item = accent left bar + tinted
      background — already what WPF-UI renders; verified good as-is. The prototypes push Settings /
      About to the bottom via `FooterMenuItems`, but the user is happy with the current single-list
      layout, so **left unchanged**. Favorites (Phase 2.5) joins the main list. Revisit the footer
      split only if the nav list grows crowded.
- [ ] *(deferred, not now)* **Card-grid view toggle** for the catalog — prototypes 3c/3d (grid) and
      3e (`ui:CardExpander` with a `16 requirements / 1 reward` summary that expands the chips).
      A per-user list/cards switch persisted in `settings.json`. Design exists; build later.
- [ ] **Icon set** (spec §06): standardise on `ui:SymbolIcon` `SymbolRegular` glyphs —
      `Star28` outline→`Filled` (**not** `StarOff28`, which is struck through — see
      docs/design-system.md), `CloudCheckmark16`, `Checkmark24`, `ArrowUndo24`, `ArrowLeft24`,
      `Search24`, `Branch24`, `Open24`, `ArrowDownload24`, `DocumentBulletList`/`Box`/`Info` for nav,
      `Cube24` as the missing-art placeholder. **Open**: the blueprint glyph is not final
      (`ChannelShare16 ?`) — pick one and record it.
- [ ] **Terminology — UI says "XP"**: the badge is the display mask `+{reputation} XP` over the
      existing API value — always what the contract *awards*, on every row regardless of completion
      (`+0 XP` in prototype 3a is placeholder data, not a rule). Rank bar reads `110 / 340 XP`.
      Localization strings in both `Strings.en.xaml` and `Strings.uk.xaml` change accordingly. The
      **domain model stays `reputation`** (`ReputationLevels`, `TotalReputation`, `completed.json`) —
      it matches the API and the in-game ranks; do not rename it to chase a label.
- [x] **Migrate page by page**: Catalog, Contract detail (incl. the shared `ChipListStyle`),
      Inventory, Settings, About. (Favorites is Phase 2.5, still to come.) Verified in **Light and
      Dark** and in **en and uk** — light is the separate palette, not dark inverted, and the uk
      pass found no truncation or fixed-width overflow on any screen. The only English string in uk
      mode is the Wikelo reputation ranks (`Reputation_Tier_*`), which stay English **on purpose** —
      in-game standing names, like item names; already commented in `Strings.uk.xaml`.
- [x] **`docs/design-system.md`** — the reference doc, in English per the repo language policy:
      role → WPF-UI theme key table, the "never re-declare a theme brush, never hardcode a hex" rule,
      the custom-key list and why each exists, the geometry scale, the control/icon mapping, and how
      to add a new role. `docs/ui-notes.md` gets a pointer; the *Docs map* in `CLAUDE.md` gains a
      "read before touching styling" line.

## Phase 3.7 — Brand refresh (icon + banners)

New artwork: a rounded-tile "W" mark on the brand cyan gradient. Source
`D:\dev\own_repo\starCitizen\wikeloMedia\assets\icons` — six SVG masters, nine frame PNGs
(16…256), `icon.png` / `icon-light.png`, `banner.png`, `about-hero.png`. `docs/brand/icon-spec.md`
is already artwork-agnostic and stays the contract the artwork must satisfy; update it **in place**
where the new artwork legitimately changes a rule, do not fork it per revision.

- [x] **Import the masters** to `docs/brand/` as `master-{ondark,onlight}-{full,mid,min}.svg` (old
      `icon-vector-*.svg` removed). Spec's naming paragraph rewritten to the surface-named form.
- [x] **Assemble `src/Assets/app.ico`** from the nine `ico/icon-*.png` frames (16…256), built as a
      PNG-framed ICO container in PowerShell; verified all 9 frames decode via `BitmapDecoder`.
      Cyan-tile (`ondark`) variant.
- [x] **Reconcile three spec deviations** — all three edited into `docs/brand/icon-spec.md`:
      full-bleed rounded tile (deliverables now say "opaque rounded tile on transparent corners");
      stroke-weight compensation between mid/min now explicitly allowed; the `full` inner border
      documented as the one permitted sub-5 % decorative exception (lives only on `full`).
- [x] **Verify contrast per variant** — measured (not eyeballed): dark W on the cyan tile = 9.5–
      13.5:1, cyan-gradient W on the dark tile = 5.95:1 worst case / up to 10.9:1. Both clear WCAG
      AA (4.5:1); the cyan-on-dark worst case sits just under AAA. The old white-eyes 2.03:1 item is
      gone — the new artwork has no eyes.
- [x] **In-app vector** `Resources/BrandIcons.xaml`: regenerated as `DrawingImage`s transcribed from
      the mid masters (rounded-rect tile with a `LinearGradientBrush`, "W" as a round-cap `Pen`
      stroke). `AppMarkDark`/`AppMarkLight` keys kept so no consumer changed. Confirmed crisp at the
      16 px title-bar slot in **both** themes (dark→cyan tile, light→dark tile).
- [x] **GitHub banner** → `docs/banner.png` (1920 × 960); `README.md` already references it.
- [x] ~~**About hero** → `src/Assets/about-hero.png`~~ — **dropped**. Phase 3.6 composes the About
      hero in XAML from the theme-swapped gradient plus the vector mark, which is what the design
      actually calls for (6a dark / 6b light). That resolves the "dark hero in a light theme" open
      question by removing the bitmap: delete `about-hero.png` and its `<Resource>` entry, and drop
      the row from the `icon-spec.md` deliverables table.
- [x] **Smoke-run** — built clean and ran; title-bar mark confirmed in both themes via `PrintWindow`
      capture. Taskbar/Alt-Tab use the same `icon.png`/`icon-light.png` rasters (viewed, correct) and
      the installer/shortcut use `app.ico` (frames validated). About hero uses the same `DrawingImage`
      as the title bar in the Phase 3.6-validated layout. *(A live eyeball of taskbar at 125 % DPI is
      the one thing best confirmed by the user on the real desktop.)*

## Phase 4 — Overlay

- [ ] Separate window: `Topmost`, borderless, semi-transparent background, compact inventory list
- [ ] Global show/hide hotkey (user32 `RegisterHotKey`, default configurable in Settings)
- [ ] Quantity editing from the overlay (+/- buttons, keyboard input)
- [ ] Click-through mode (toggle `WS_EX_TRANSPARENT`), remember position/size
- [ ] Verify on top of SC in Fullscreen (DWM fullscreen optimizations) and Borderless

## Phase 3.9 — Sync visibility + synthetic E2E tests

Triggered by a real incident: a new API build shipped, the catalog refresh started, and nothing in
the app said so. Root cause was a missing concept, not a missing message — freshness
(`CatalogStatus`) existed, completeness did not.

- [x] `Services/CatalogSyncState` — phase (`Idle`/`Contracts`/`Rewards`) + per-phase progress,
      exposed as `IContractCatalogService.SyncState` + `SyncStateChanged`. Kept **orthogonal** to
      `CatalogStatus`: the catalog is normally `Online` *and* syncing. Set synchronously before
      enrichment is queued; cleared before `CatalogUpdated` and again in a `finally`, so an
      aborted run cannot leave the UI blocked forever.
- [x] Catalog page: third badge state (`ArrowSync24`), an informational InfoBar, and — per the
      chosen policy — the whole catalog blocked while syncing (filters disabled + determinate
      overlay over the list). `IsSynced` now requires `!IsSyncing`, so the green cloud stops
      claiming fresh data mid-sync.
- [x] Completion toggle withheld mid-sync on both the card and the detail page. Not cosmetic:
      `ContractCompletionInteraction` deducts `contract.Requirements`, which mid-sync is the
      `hauling_summary` fallback (no SCU amounts, missing entries) — completing then removed the
      wrong amounts from the inventory, irreversibly.
- [x] First synthetic E2E tests (`tests/E2E/`): real services + ViewModels on a real WPF
      `Application` (STA fixture) driven by one scripted API fake. Seven scenarios covering the
      version bump, the blocked filter, the refused completion, an aborted enrichment, an offline
      launch, a rate-limited launch, and a 429 mid-enrichment. Written red first.
- [x] Consolidated the duplicate `IStarCitizenWikiClient` fake into `tests/E2E/ScriptedWikiApi`.
- [ ] Follow-up found while testing: a failed refresh's status is erased on the next catalog
      navigation. Settings shows "offline", the user opens Catalog, the 12 h version-check timer
      has not elapsed, so the cache is re-served as `Online` and the green cloud returns. Same
      class of dishonesty as the sync badge; needs its own decision on what the badge should say.

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
      `vpk pack` also emits an `.msi` (`--msi --instLocation Either`) alongside `Setup.exe`: a WiX
      wizard with per-user/per-machine choice, a Browse dialog for an arbitrary install folder, and
      a visible progress page. `Setup.exe` stays the one-click default. WiX ships inside `vpk`.
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
