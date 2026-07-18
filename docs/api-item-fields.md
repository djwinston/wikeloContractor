# Item / vehicle detail — field inventory

What `GET /api/items/{uuid}` returns for reward items. The endpoint transparently serves a
**vehicle record** for vehicle UUIDs (ships, ground vehicles, paint variant records), so the
two shapes are listed separately. Everything below arrives in the responses enrichment
already fetches — capturing more fields costs **zero extra API calls**: extend
`StarCitizenWikiClient.ParseDetails` + `Models/RewardDetails`, and bump
`ContractCatalogService._cacheSchemaVersion` so caches rebuild.

Live fixtures these tables were derived from: `item-detail.json` (Ana Arms Endro),
`vehicle-detail.json` (Syulen) — re-sample via the api-explore skill when the API changes.

**Stored** = captured into `ContractReward.Details` / `.Images` today.

## Regular item (armor, weapons, ...)

| Field | Type | Meaning | Stored |
|---|---|---|---|
| `uuid`, `name`, `slug`, `class_name` | string | identity (name/uuid already on the reward) | ✅ |
| `description` | localized object (`en_EN`, ...) | lore description | ✅ en |
| `manufacturer` | object `{name, code, uuid, link}` | manufacturer | ✅ name |
| `type`, `type_label` | string | machine type (`Char_Armor_Arms`) / human label ("Arms (Armor)") | ✅ label¹ |
| `sub_type`, `sub_type_label` | string | subtype, e.g. "Heavy" | ✅ label |
| `rarity` | string | "Common", "Rare", ... | ✅ |
| `classification`, `classification_label` | string | tree path `FPS.Armor.Arms` / leaf label | — |
| `size`, `mass` | number | item size class, mass (kg) | — |
| `dimension` | object | width/height/length + inventory volume | — |
| `clothing` / `suit_armor` | object | slot, armor type, **`damage_resistance_map`** (multiplier per damage type + `*_change` deltas), `temp_resistance_min/max`, `radiation_resistance`, `gforce_resistance`, protected body parts, EM signature | ✅ resist map |
| `temperature_resistance` | object `{min, max}` | survivable temperature range, °C | ✅ |
| `radiation_resistance` | object | `maximum_radiation_capacity`, `radiation_dissipation_rate` | ✅ |
| `gforce_resistance` | number | G-force modifier | — |
| `is_base_variant`, `variants` | bool / array | variant group info | — |
| `is_craftable`, `is_lootable` | bool | acquisition flags | — |
| `shops` | array | shop availability (empty for Wikelo exclusives) | — |
| `uex_prices` | object | UEX Corp price data | — |
| `tags`, `required_tags`, `entity_tags`, `interactions` | arrays | game-file tags | — |
| `images` | array | external CDN images (see data-pipeline.md) | ✅ all |
| `web_url`, `link` | string | wiki page / API self-link | — |
| `version`, `updated_at` | string | game version of the record | — |

¹ machine `type` is kept transiently for category derivation, not persisted.

## Vehicle record (ships, ground vehicles, paints)

| Field | Type | Meaning | Stored |
|---|---|---|---|
| `name`, `game_name`, `slug`, `class_name` | string | identity ("Syulen" / "Gatac Syulen") | ✅ name |
| `description` | localized object | lore description | ✅ en |
| `manufacturer` | object `{name, code, ...}` | manufacturer | ✅ name |
| `career`, `role`, `foci` | string / array | "Multi-Role", "Starter / Pathfinder" | ✅ career, role |
| `production_status`, `production_note` | localized object | development status | — |
| `size`, `size_class` | localized object / number | size category | — |
| `sizes`, `dimension`, `cross_section` | object | length/beam/height (m) | — |
| `mass`, `mass_hull`, `mass_loadout`, `mass_total` | number | masses (kg) | — |
| `cargo_capacity` | number | cargo, SCU | ✅ |
| `cargo_grids`, `cargo_limits`, `ore_capacity` | array / object | grid breakdown, box size limits | — |
| `vehicle_inventory`, `inventory_containers`, `weapon_storage` | number / arrays | personal inventory (µSCU), weapon lockers | — |
| `crew` | object `{min, max, weapon, operation}` | crew seats | ✅ min/max |
| `seating` | object | beds, ejection seats, escape pods, medical beds | — |
| `health` | number | hull HP | ✅ |
| `shield_hp`, `shield`, `shield_face_type` | number / object | shield pool, regen, resist/absorption maps | ✅ hp |
| `armor` | object | physical/IR/EM damage modifiers | — |
| `weaponry`, `weapon_snapshot`, `turrets`, `ports`, `parts`, `components` | objects/arrays | DPS/alpha, hardpoints, installed components | — |
| `speed` | object | `scm`, `max`, boost, acceleration times | ✅ scm, max |
| `agility`, `afterburner`, `propulsion`, `fuel`, `quantum` | object | maneuvering, fuel, quantum range/speed | — |
| `emission`, `signature`, `cooling`, `power`, `power_pools` | object | IR/EM signatures, power/cooling budget | — |
| `insurance` | object | claim/expedite times, expedite cost | — |
| `msrp`, `pledge_url`, `skus` | number / string / array | pledge store price (USD) and page | ✅ msrp, url |
| `uex_prices` | object | UEX Corp prices (purchase/rental) | — |
| `loaner` | array | loaner ships | — |
| `is_spaceship`, `is_vehicle`, `is_gravlev`, `is_power_suit` | bool | record kind flags (drive category) | ✅² |
| `images` | array | external CDN images | ✅ all |
| `web_url`, `link`, `id`, `chassis_id`, `shipmatrix_name` | misc | wiki/API references | — |
| `version`, `updated_at` | string | game version of the record | — |

² used for category derivation (Ship / GroundVehicle), not persisted as-is.

## Notes

- Wikelo-exclusive variants (e.g. "Asgard Wikelo War Special") still return the full stat
  block but usually have an **empty `images` array** and no shop/price data.
- `description` for items and vehicles is a localized object; the app stores `en_EN` only
  (API data stays English by policy).
- `damage_resistance_map` values are damage **multipliers** (lower = better); the paired
  `*_change` keys are deltas vs. baseline and are skipped when parsing.
