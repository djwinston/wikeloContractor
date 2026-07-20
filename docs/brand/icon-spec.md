# App icon

Current state of the mark. Update in place; do not fork a file per revision.

WPF consumes a standard Windows `.ico` plus ordinary PNGs — there is no ".NET icon format".
All coordinates below are in the 200 × 200 viewBox of the vector masters.

## Masters and where each is used

Small sizes are produced by **deleting elements** from the full composition, never by redrawing.
That rule is what keeps the variants one mark instead of four similar ones.

| Master | Elements | `app.ico` frames |
|---|---|---|
| `icon-vector-dark.svg` / `icon-vector-light.svg` | 2 rings, mask, eyes, nose | 40, 48, 64, 128, 256 |
| `icon-vector-24.svg` | outer ring, mask, eyes | 24, 32 |
| `icon-vector-16.svg` | outer ring, mask | 16, 20 |

The geometry is identical across all of them — mask 75 × 132 centred at (100, 100), outer ring
`r=92` stroke `10`, so the outer edge lands at 97 % of the canvas half-width. Only the element
set changes. The in-app title bar uses the `icon-vector-24` geometry (see `docs/ui-notes.md`).

## Colour rules

- **Dark and light variants differ in colour values only.** Identical coordinates, proportions
  and stroke widths. A geometry difference between them is a bug.
- Names describe the **artwork**, not the target theme: `icon-vector-dark` / `icon.png` are the
  navy mark for light surfaces; `icon-vector-light` / `icon-light.png` are the cyan one for dark
  surfaces.
- **Eyes are navy `#16324A` on the cyan variant, white `#F7FDFF` on the navy variant.** White on
  the bright gradient measures 1.9:1 and washes out; navy gives 6.9:1. Do not "fix" this by
  darkening the gradient — the cyan silhouette needs to stay bright to hold 7.2:1 on a dark
  taskbar.

## Constraints that have bitten us

- **Strokes below ~5 % of canvas die on downscale.** The original hairline rings were 0.8 % and
  rendered at 0.19 px in a 24 px frame. Current stroke is 5 %.
- **Nothing may extend past the canvas.** SVG clips at the viewBox, leaving stub artifacts in the
  square assets. Keep the whole composition, glow included, inside 200 × 200.
- **A `DrawingImage` has no viewBox** — it sizes to its drawing bounds, so any stray element
  outside the intended box silently shrinks the mark in the title bar.

## Deliverables

`app.ico` (16/20/24/32/40/48/64/128/256, 32-bit BGRA), `icon.png` and `icon-light.png` at
1024 × 1024 transparent, the four vector masters, plus `about-hero.png` (1200 × 674) and
`github-banner.png` (1920 × 960). All rasters exported from the masters, never resampled.

## Out of scope

The title bar icon renders at the WPF-UI default of 16 × 16; that slot is fixed by the
`ui:TitleBar` template and a larger `ImageIcon.Width`/`Height` clips the artwork rather than
scaling it. The icon grows only by the artwork filling more of its 16 px box.

## Known open item

`icon-vector-24.svg` still carries white eyes on the flat `#2FC1DE` mask — 2.03:1. It feeds the
24 and 32 px frames, which is what the taskbar draws at 100 % and 125 % scaling. The title bar
does not inherit this: `Resources/BrandIcons.xaml` sets `#16324A` directly.
