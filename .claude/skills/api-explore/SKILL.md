---
name: api-explore
description: Workflow for exploring the Star Citizen Wiki API (or any OpenAPI service) before writing client code — spec download, endpoint discovery, live sampling, DTO design. Use whenever new endpoints, parameters, or response shapes need to be understood.
---

# API exploration workflow

Follow these steps **in order** before writing any client code for a new endpoint.
Never design DTOs from the spec alone — always sample live responses.

## 1. Get the spec (once per session)

```powershell
Invoke-WebRequest 'https://api.star-citizen.wiki/api/openapi' -OutFile "$scratchpad\openapi.yaml"
```

- The spec is **YAML**, not JSON, ~700 KB. Do NOT read it whole — grep it.
- Base URL: `https://api.star-citizen.wiki/`, interactive docs: https://docs.star-citizen.wiki

## 2. Find endpoints and parameters

- Paths sit at 2-space indentation: grep `^  /api/` to list, then grep the resource name.
- Once the path's line number is known, Read ~100–200 lines from there: parameters,
  filter names, and the response schema `$ref` are all inside the path block.
- Filter syntax is `filter[field]=value`; pagination is `page[number]` / `page[size]` (**max 200**);
  sorting is `sort=field` / `sort=-field`.

## 3. Check valid filter values via the facets endpoint

Most list resources have a `/filters` twin (e.g. `GET /api/missions/filters`) returning
`{ "filters": { field: [ { value, label, count } ] } }` — use it to verify a value exists
and see how many records match before fetching.

## 4. Sample live data — this is the source of truth

```powershell
Invoke-WebRequest '<url>' -OutFile "$scratchpad\sample.json"   # keep as fixture
$d = Get-Content "$scratchpad\sample.json" -Raw | ConvertFrom-Json
$d.data[0].PSObject.Properties.Name                            # field inventory
```

- Inspect several records, not one: fields are frequently `null` in some records only.
- List vs detail responses differ: detail endpoints add fields (e.g. missions detail adds
  `mission_type`, `reward_items`, `hauling_orders`, `description_html`).
- Watch for placeholder/junk records from game files (`<= UNINITIALIZED =>`, `released: false`,
  `not_for_release`, `work_in_progress`) — decide explicitly whether to filter them.
- Group-by is a fast facet scanner: `$d.data | Group-Object field | Sort-Object Count -Descending`.

## 5. Bulk sampling etiquette

- Search endpoints are rate-limited to 60 req/min/IP; other endpoints: stay polite anyway.
- For per-record detail sweeps add `Start-Sleep -Milliseconds 300` between calls and run
  the loop in the background (`run_in_background`), writing results to a scratchpad JSON.
- Save fixtures to the scratchpad; they double as future test data.

## 6. Only then write code

- DTOs: `[JsonPropertyName]` per field (API is snake_case), value types nullable unless
  proven otherwise, lists default to `[]`.
- Every response is an envelope: `{ data, links, meta }` — model `data` + what you need.
- Record the endpoint + quirks discovered in PLAN.md notes for the relevant phase.

## Known project facts (update as they are discovered)

- Wikelo contracts: `GET /api/missions?filter[mission_giver]=Wikelo&page[size]=200`
  (~88 records; default page size is only 30, so `page[size]` is required).
  `filter[reputation_scope]=Wikelo` returns only ~60 — it misses top-rank trades that grant
  no reputation (`reputation_gained: null`), e.g. the Idris contract ("Special Idris For Killing").
- Requirements are in list field `hauling_summary`; rewards only in detail (`reward_items`).
- Category lives in detail field `mission_type`: "Wikelo - Vehicles" / "Wikelo - Other Items" / "Collection".
- Current LIVE version: `GET /api/game-versions` → entry with `is_default: true`; doubles as a health check.
- SCU-based requirements have `min_amount/max_amount = null` and use `min_scu/max_scu` (detail `hauling_orders`).
- Detail `hauling_orders` also list entries the list-level `hauling_summary` omits:
  "Wikelo Favor" (41 of 55 contracts) and vehicles to hand over — treat orders as the
  authoritative requirements source.
- `GET /api/items/{uuid}` transparently returns **vehicle records** for vehicle UUIDs:
  `type` becomes a localized object (or is absent) and flags `is_spaceship` / `is_vehicle` /
  `is_gravlev` / `is_power_suit` appear. For regular items `type` is a string
  (`Char_Armor_*`, `WeaponPersonal`, ...). Parse with JsonDocument, not a fixed DTO.
- Paint/color rewards are whole vehicle variant records; detect them by name (`color|paint|livery`).
- Item/vehicle detail responses **always include an `images` array** (no `include` param needed):
  `[{ source, thumbnail_url, original_url, thumbnail_width/height, original_width/height }]`.
  Sources seen: `cstone.space` (regular items; `thumbnail_url == original_url`, no real thumb)
  and `starcitizen.tools` (vehicles/armor; real 600px `.webp` thumbs on `media.starcitizen.tools`).
- The `images` array can be **empty** — Wikelo-exclusive vehicle variants
  (e.g. "Asgard Wikelo War Special") have no wiki coverage; plan a placeholder fallback.
- Image files are hosted on external Cloudflare-backed CDNs (`cstone.space`,
  `media.starcitizen.tools`) — **separate from the API's rate limit**, long `Cache-Control`
  (7–31 days), no auth, no rate-limit headers observed.
