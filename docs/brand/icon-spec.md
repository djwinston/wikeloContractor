# App icon — technical specification

Artwork-agnostic production spec. It defines *how* the mark is authored and exported, not what
it depicts. Update in place; do not fork a file per revision.

WPF consumes a standard Windows `.ico` plus ordinary PNGs — there is no ".NET icon format".

## Authoring

- **Vector-first.** Every raster is exported from a vector master, never resampled from a larger
  raster. All coordinates live in a **200 × 200 viewBox**.
- **Two colour variants, one geometry.** Ship a variant for light surfaces and one for dark
  surfaces. They differ in colour values only — identical coordinates, proportions and stroke
  widths. A geometry difference between the two variants is a bug. The current mark is a full-bleed
  rounded "W" tile: `ondark` is the cyan tile with a dark stroke, `onlight` the dark tile with a
  cyan-gradient stroke.
- **File names describe the surface the mark is drawn *for*** — `master-ondark-*` sits on a dark
  surface, `master-onlight-*` on a light one. (This is the opposite of the older
  `icon-vector-dark`/`-light` convention, which named the artwork colour; the surface-named form is
  less ambiguous, so it replaced it.)
- **Small sizes drop detail; they never redraw the mark.** Maintain a small set of masters with
  identical core geometry and shed elements as the frame shrinks. Compensating the *stroke weight*
  as detail is removed is allowed and expected (mid thickens the "W" from 17 → 19 once the border
  and dot are gone, min to 23) — that is an optical-size adjustment, not a redraw. Changing the
  path itself between masters is the bug this rule guards against.

| Master role | Elements | Feeds `app.ico` frames |
|---|---|---|
| `*-full` | tile + faint inner border + "W" (stroke 17) + dot | 40, 48, 64, 128, 256 |
| `*-mid` | tile + "W" (stroke 19) | 24, 32 |
| `*-min` | tile + "W" (stroke 23) | 16, 20 |

## Rendering constraints that have bitten us

- **Strokes below ~5 % of the canvas die on downscale.** A 0.8 % hairline renders at ~0.19 px in
  a 24 px frame and disappears. Keep *load-bearing* strokes ≥ 5 % of canvas width (the "W" is 8.5–
  11.5 %). The one allowed exception is purely decorative detail that the mark does not depend on:
  the `*-full` master's inner border is a 2 px (1 %) stroke at 18 % opacity, and it is meant to
  fade out on the smaller frames rather than read as a line — so it lives only on `full`, never on
  the frames small enough for it to turn to mud.
- **Nothing may extend past the viewBox.** SVG clips at the viewBox, leaving stub artifacts in the
  square raster assets. Keep the whole composition — glow, shadow, bleed included — inside
  200 × 200.
- **A WPF `DrawingImage` has no viewBox.** It sizes to its drawing bounds, so any stray element
  outside the intended box silently shrinks the mark wherever the vector is used in-app.

## Contrast

- Every variant must clear **WCAG 4.5:1** against the surface it is drawn for, measured at the
  smallest frame that surface uses (taskbar draws 24/32 px at 100 %/125 % scaling).
- Interior detail (eyes, accents) may need a *different colour per variant* to hold contrast —
  do not assume one accent colour works on both. Verify each variant independently; do not
  darken a bright silhouette to "fix" interior contrast, as that trades away the contrast the
  silhouette needs against a dark taskbar.

## Deliverables

All exported from the vector masters, never resampled.

| Asset | Spec | Path |
|---|---|---|
| Icon (all frames) | 16/20/24/32/40/48/64/128/256, 32-bit BGRA, PNG-compressed frames | `src/Assets/app.ico` |
| App PNG (light surfaces) | 1024 × 1024, opaque rounded tile on transparent corners | `src/Assets/icon.png` |
| App PNG (dark surfaces) | 1024 × 1024, opaque rounded tile on transparent corners | `src/Assets/icon-light.png` |
| GitHub banner | 1920 × 960 | `docs/banner.png` |
| Vector masters | 6: `{ondark,onlight}-{full,mid,min}` | `docs/brand/master-*.svg` |

The About page hero is **not** a raster — it is composed in XAML from the theme-swapped
`AboutHeroBackgroundBrush` plus the vector mark, so it is theme-correct without a bitmap. There is
no `about-hero.png`.

## Out of scope

The in-app title bar icon renders at the WPF-UI default of 16 × 16; that slot is fixed by the
`ui:TitleBar` template, and a larger `ImageIcon.Width`/`Height` clips the artwork rather than
scaling it. The icon grows only by the artwork filling more of its 16 px box.

`Resources/BrandIcons.xaml` is the in-app vector, a hand-transcribed `DrawingImage` of the **`mid`**
master (tile gradient + "W" stroke, no border/dot). It mirrors the master's colours rather than
referencing the SVG, since WPF cannot load `.svg` — so a colour change in the mid master must be
copied across. See `docs/ui-notes.md`.
