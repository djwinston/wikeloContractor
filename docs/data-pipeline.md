# Catalog data pipeline

Read this before touching anything under `src/Services/` or `src/Models/Api/`.
Companion doc for API exploration: `.claude/skills/api-explore/SKILL.md` (endpoint facts live there).

## Flow overview

```
Star Citizen Wiki API ──► StarCitizenWikiClient ──► ContractCatalogService ──► ViewModels
                                                        │
                                                        ▼
                                     %AppData%\WikeloContractor\cache\contracts.json
```

- `StarCitizenWikiClient` (`Services/Api/`) — typed HttpClient, all raw HTTP + DTO parsing.
- `ContractCatalogService` (`Services/`) — caching, version-based invalidation, background
  enrichment, rate limiting. The only service ViewModels talk to.
- DTOs (`Models/Api/MissionDto.cs`) mirror the API; `WikeloContract` (`Models/`) is the
  UI-facing model (a **record** — enrichment rebuilds instances via `with`).

## Caching & invalidation (version-based, not TTL)

Wikelo contracts change only when a new game version ships, so:

- Cache envelope (contracts + `GameVersion` + `FetchedAt` + `LastVersionCheckAt` + `Enriched`)
  is persisted to `cache/contracts.json` with an atomic write (`.tmp` + `File.Move`).
- The `%AppData%\WikeloContractor` root and the JSON options (indented, enums as strings) are
  shared through `Services/AppStorage`; both this service and `SettingsService` go through it,
  so the folder name and serialization conventions live in one place.
- On load: if the last version check is younger than **12 h** → serve the cache, no network.
- Otherwise `GET /api/game-versions` (entry with `is_default: true` = current LIVE; doubles
  as a health check). Same version → keep cache; new version → refetch missions.
- `forceRefresh: true` (Settings → "Check for updates") skips the 12 h timer, never the rate-limit gate.
- API unreachable → serve stale cache with `Status = CatalogStatus.Offline` (offline badge); with
  no cache at all the exception propagates and the UI shows the load-error InfoBar.
- Unreleased missions (`released: false`) are placeholder junk from game files — always filtered out.

Every `CatalogLoadResult` carries a single mutually-exclusive `CatalogStatus`
(`Online` / `Offline` / `RateLimited`), decided once by the service. The UI maps that one
value to badges/InfoBars, so offline and rate-limited can never be reported together.

## Background enrichment

The missions list has no rewards; those need per-mission detail calls (~88) plus item
classification calls, plus one lookup per distinct ship gun name (kind label/grade —
see docs/api-item-fields.md note ³). This runs fire-and-forget after the list is served:

- Guarded by `Interlocked.Exchange` (single run), 150 ms politeness delay between calls.
- Derives categories per contract from per-reward classification: paint by name regex
  (`color|paint|livery`), then vehicle-record flags (`is_spaceship` → Ship, other vehicle
  flags → GroundVehicle), then item type string (`Char_Armor_*` → Armor, `Weapon*` → Weapon),
  else Other. Two fields result: the primary `Category` (card icon / detail badge — the
  highest-priority reward, Ship > GroundVehicle > Paint > Weapon > Armor; always **Other**
  for `once_only` unlocks like "Arrive to System", whose grab-bag mix must not drive it)
  and the full `Categories` set (all reward categories + primary), which the catalog
  category filter matches against — a ship contract with bonus armor and weapons is
  discoverable under Ships, Armor and Weapons alike.
- Shop-exclusive vehicle variants (e.g. "Mirai Pulse Wikelo Special") come back from
  `api/items/{uuid}` as plain **item** records (`type: "Vehicle"`, no vehicle flags, and a
  misleading `sub_type: Vehicle_Spaceship` even for gravlevs). The client follows
  `base_variant.uuid` (one extra call) and, when that resolves to a vehicle record, borrows
  its flags and stats — the variant keeps its own name, images, and description.
- On success: envelope is rewritten with `Enriched = true` and **`CatalogUpdated` is raised
  on a background thread** — subscribers must marshal to the dispatcher.
- On failure: silently aborted; retried on the next catalog load (envelope stays `Enriched = false`).

## Requirements from hauling orders

The mission detail's `hauling_orders` are richer than the list's `hauling_summary`:
they carry SCU-based amounts (`min_scu`/`max_scu` — the summary shows those as `null`)
and entries the summary omits entirely ("Wikelo Favor", vehicles to hand over).
Enrichment **replaces** a contract's `Requirements` with the orders when the detail has
any; the summary-based list stays as a fallback. `ContractRequirement.AmountLabel`
renders SCU ranges ("36 SCU") when the SCU fields are set.

## Reward details

Enrichment also captures per-reward display details (`ContractReward.Details`,
`Models/RewardDetails`) from the same item/vehicle detail responses: description (en),
manufacturer, plus item stats (type/subtype labels, rarity, damage/temperature/radiation
resistances) or vehicle stats (career/role, cargo SCU, crew, HP/shields, SCM/max speed,
MSRP + pledge URL). The full field inventory of those responses — including everything we
do **not** store yet — is documented in `docs/api-item-fields.md`; extending it is
parse-only (no extra API calls), just bump the cache schema version.

## Reward preview images

Item/vehicle detail responses always carry an `images` array (no `include` param), so image
URLs ride along with the classification calls enrichment already makes — **zero extra API
requests**. Enrichment stores **all** entries per reward (`ContractReward.Images`); the UI
uses the first loadable one, keeping the rest available for manual selection later.

- The envelope has a `SchemaVersion` (`_cacheSchemaVersion`); a mismatch on read
  discards the cache so the catalog is refetched and re-enriched with the new shape.
- Image **files** are hosted on external Cloudflare CDNs (`cstone.space`,
  `media.starcitizen.tools`) — outside the API rate limit, so downloads bypass the 429 gate
  entirely. `ImageCacheService` (own HttpClient, singleton) downloads each URL once into
  `cache/images/<sha256(url)><ext>` (atomic write) with a politeness cap of 4 parallel
  downloads and in-flight deduplication; failures are not cached and retry on next use.
- Wikelo-exclusive variants (e.g. "Asgard Wikelo War Special") have **no** wiki images —
  the UI falls back to a category placeholder icon.
- `ImageOverrideService` merges **two** override files: the bundled
  `src/Resources/img-catalog-overrides.json` (maintained in the repo, copied to the build output —
  shared defaults for every user; pre-seeded with placeholder entries for items without API
  images, inventory in `docs/reward-images.md`) and the user's
  `%AppData%\WikeloContractor\img-catalog-overrides.json` (template auto-created), which wins per
  key. Keys are item UUID **or** item name (case-insensitive); values are an image URL (or,
  in the user file, an absolute local path); empty values are ignored placeholders.
  Overrides win over API images and both files are re-read when they change on disk.

## Rate limiting (HTTP 429)

- The client throws `ApiRateLimitedException` (with `Retry-After` when the server sends it)
  for any 429 response.
- The service opens a global gate `_rateLimitedUntil` = now + (`Retry-After` ?? 60 s) + **5 s
  safety margin**. Until it elapses **no API call leaves the app**: catalog loads serve the
  cache (`Status = CatalogStatus.RateLimited`), enrichment calls wait the window out.
- The absolute deadline is exposed as `RateLimitedUntil` (the single source of truth); a
  payload-free `RateLimitChanged` event signals it opened. The shared `RateLimitWatcher`
  (`ViewModels/`, singleton) reads that deadline and drives one per-second countdown InfoBar
  bound by BOTH the Catalog and Settings pages ("loading resumes in N s"). The displayed
  countdown equals the real gate — we wait longer rather than lie about the remaining time.
- Enrichment retries a 429'd call once after the window; a second 429 aborts the run
  (picked up again on the next load).

## Events contract

Both service events may fire on background threads — always `Dispatcher.Invoke` in handlers:

| Event | Payload | Raised when |
|---|---|---|
| `CatalogUpdated` | — | enrichment finished, `Current` has fresh data |
| `RateLimitChanged` | — | rate-limit window opened (or a call was attempted while the gate is closed); read `RateLimitedUntil` for the deadline |

## Politeness rules (non-negotiable)

Cache aggressively, never poll, keep the identifying User-Agent, keep the politeness delays.
The API is community-run and free; each user hits it from their own IP, so per-user load
must stay minimal.
