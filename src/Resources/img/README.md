# Bundled item images

Pictures for items the Star Citizen Wiki API and community CDNs have **no image** for
(hand-made screenshots, crops, or a picture sourced elsewhere). These ship as loose content
next to the exe and are the *bundled defaults* layer for the two image override files.

## How to add one

1. **Encode it as lossy WebP** — these ship inside the release download, and a raw screenshot or
   lossless WebP is 10–20× bigger than it needs to be for a 46×46 thumb and a full-window preview.
   Every file in these folders went through exactly this line, and only `.webp` is shipped
   (the build glob ignores anything else, so a leftover source PNG next to it is harmless):
   ```powershell
   magick in.png -strip -quality 88 -define webp:method=6 -define webp:sharp-yuv=true out.webp
   ```
   Keep the native pixel size — the preview overlay decodes unscaled. Then put it in the folder for
   its side: `catalog/` for reward images, `inventory/` for required items (no spaces in the name).
2. Add a key to the matching override file:
   - Reward images → `Resources/img-catalog-overrides.json` (key: item **UUID** or name)
   - Inventory / required items → `Resources/img-inventory-overrides.json` (key: item **name**)
   ```json
   "overrides": {
     "Sadaryx": "Resources/img/inventory/sadaryx.webp"
   }
   ```
   The value is a path **relative to the install dir** — `ImageCacheService` resolves it against
   `AppContext.BaseDirectory`, so the shared override file stays machine-independent.

## Notes

- These files are **replaced on every app update** (like the override JSON). Personal images that
  should survive updates belong in `%AppData%\WikeloContractor\img-*-overrides.json` instead, where
  a value may also be an absolute path to a file on your own disk.
- An image that *does* exist on a community wiki needs no bundling — just put its URL in the
  override file, as the existing entries do.
- Several keys may point at one file — a whole armor set, or both grades of the same material,
  share a single picture.
- After adding one, flip its row in `docs/reward-images.md` / `docs/inventory-images.md` to 🖼 —
  those two files are the coverage overview.
