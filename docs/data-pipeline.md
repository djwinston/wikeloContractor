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

The missions list has no rewards; those need per-mission detail calls (~55) plus item
classification calls. This runs fire-and-forget after the list is served:

- Guarded by `Interlocked.Exchange` (single run), 150 ms politeness delay between calls.
- Derives `ContractCategory` per contract: paint by name regex (`color|paint|livery`),
  then vehicle-record flags (`is_spaceship` → Ship, other vehicle flags → GroundVehicle),
  then item type string (`Char_Armor_*` → Armor, `Weapon*` → Weapon), else Other.
  Priority when a contract has several rewards: Ship > GroundVehicle > Paint > Weapon > Armor.
- On success: envelope is rewritten with `Enriched = true` and **`CatalogUpdated` is raised
  on a background thread** — subscribers must marshal to the dispatcher.
- On failure: silently aborted; retried on the next catalog load (envelope stays `Enriched = false`).

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
