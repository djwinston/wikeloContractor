# Inventory image inventory

Which required items have a bundled image in `src/Resources/img-inventory-overrides.json`
and which still need one — the inventory counterpart of `reward-images.md`.

Inventory requirements carry **no UUID**: the API returns them as bare names, and `InventoryStore`
keys by name too. So unlike reward images there is nothing to look an item up by, and every entry
here is a manual name → URL mapping. The bundled file lists all 95 names; an empty value
is a placeholder waiting for a URL, ignored at runtime.

Personal entries go in `%AppData%\WikeloContractor\img-inventory-overrides.json`, which wins per
key and survives updates. See docs/ui-notes.md, "Inventory images".

Categories are what `InventoryCategoryClassifier` derives from the name — heuristic grouping for
the inventory page, not API data.

The **Image** column reflects the bundled file: ✅ = a URL is set, ⬜ = the value is still empty
(needs a URL). Current URLs were resolved from starcitizen.tools page images and spot-checked for a
200 response. Rows marked (generic) point at a shared category image rather than the item itself —
replace them when a better one exists. Items that resolved only to the wiki's own placeholder are
left empty.

Snapshot from the enriched catalog cache; regenerate by asking Claude.
Game version: 4.9.0-LIVE.12232306. Items: 95, with an image: 56.


## Ore / mineral

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | Carinite | Do Lava Suit, Fun Kopion Skull Gun, Fun Military Skull Gun, Hot Shot, Now make Polaris. Short Time Deal., Prospects Look Good, Special Idris For Killing, Turn Things to Favor, Zeus Cargo Special |
| ✅ | Carinite (Pure) | ATLS Cool Metal Color, Armor with horn and string, F8 War Mod, Fortune ship for you, More than a Max, New Move Big Starlancer Ship, Now make Polaris. Short Time Deal., Prospects Look Good, Prowler More Utility, Sneaky Stabber, Special Idris For Killing, Starlifter A2 War Mod, Want Taurus ship, Zeus Cargo Special |
| ✅ (generic) | Copper | ATLS Orange Line |
| ✅ (generic) | Corundum | ATLS Orange Line |
| ⬜ | Jaclium (Ore) | Fun Kopion Skull Gun, Fun Military Skull Gun, Make a Ursa Mod, Prospects Look Good |
| ✅ (generic) | Quantainium | ATLS Orange Line, Want Polaris? Need something special. |
| ⬜ | Sadaryx | Red Fight Armor, Red Fight Shotgun |
| ⬜ | Saldynium (Ore) | Fun Kopion Skull Gun, Fun Military Skull Gun, Make a Ursa Mod, Prospects Look Good |
| ✅ (generic) | Savrilium | Red Fight Apollo |
| ✅ (generic) | Tungsten | ATLS Orange Line |

## Armor

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | Ace Interceptor Helmet | Firebird Mod, Guardian Fight Mod, Guardian WiK-X, More than a Max, New Move Big Starlancer Ship, Now make Polaris. Short Time Deal., RSI Meteor Mod, Sneaky Starfighter Ion, Special Idris For Killing, Starfighter Inferno Special, Wikelo Navy F7, Zeus Cargo Special |
| ✅ | Antium Arms | Armor with horn and string, Make glowy armor |
| ✅ | Antium Core | Armor with horn and string, Make glowy armor |
| ✅ | Antium Helmet | Armor with horn and string, Make glowy armor |
| ✅ | Antium Legs | Armor with horn and string, Make glowy armor |
| ✅ | Corbel Arms Mire | Shiny Builder Suit |
| ✅ | Corbel Core Mire | Shiny Builder Suit |
| ✅ | Corbel Helmet Mire | Shiny Builder Suit |
| ✅ | Corbel Legs Mire | Shiny Builder Suit |
| ✅ | Ermer Family Farms Fat Free Ice Cream | Very Hungry |
| ⬜ | Geist Armor Arms ASD Edition | Hide Snow Suit |
| ⬜ | Geist Armor Core ASD Edition | Hide Snow Suit |
| ⬜ | Geist Armor Helmet ASD Edition | Hide Snow Suit |
| ⬜ | Geist Armor Legs ASD Edition | Hide Snow Suit |
| ⬜ | Geist Backpack ASD Edition | Hide Snow Suit |
| ⬜ | Monde Arms | Red Fight Armor |
| ⬜ | Monde Core | Red Fight Armor |
| ⬜ | Monde Helmet | Red Fight Armor |
| ⬜ | Monde Legs | Red Fight Armor |
| ⬜ | Novikov Backpack Mire | Shiny Builder Suit |
| ✅ | Palatino Arms | Test Armor |
| ✅ | Palatino Backpack | Test Armor |
| ✅ | Palatino Core | Test Armor |
| ✅ | Palatino Helmet | Test Armor |
| ✅ | Palatino Legs | Test Armor |
| ⬜ | Strata Arms | Do Lava Suit |
| ⬜ | Strata Backpack | Do Lava Suit |
| ⬜ | Strata Core | Do Lava Suit |
| ⬜ | Strata Helmet | Do Lava Suit |
| ⬜ | Strata Legs | Do Lava Suit |
| ✅ | Tailwind Flight Helmet | Suit Up Take Off |
| ✅ | Tailwind Flight Suit | Suit Up Take Off |
| ✅ | Testudo Arms Turfwar | Armor with Vanduul |
| ✅ | Testudo Backpack Turfwar | Armor with Vanduul |
| ✅ | Testudo Core Turfwar | Armor with Vanduul |
| ✅ | Testudo Helmet Turfwar | Armor with Vanduul |
| ✅ | Testudo Legs Turfwar | Armor with Vanduul |
| ⬜ | Warden Backpack Monde | Red Fight Armor |

## Weapon

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | Boomtube Rocket Launcher | Curious Weapon |
| ✅ | Fresnel Energy LMG | Yormandi Gun |
| ✅ | Killshot Rifle | Your Best Shot |
| ✅ | NN-13 Cannon | Make ATLS shoot, Make jumpy ATLS shoot |
| ✅ | Parallax Energy Assault Rifle | Fun Kopion Skull Gun, Fun Military Skull Gun |
| ✅ | Prism Laser Shotgun | Make VOLT shotgun angrier |
| ✅ | R97 Shotgun | Red Fight Shotgun |
| ✅ | Tripledown Pistol | Hot Shot |
| ✅ | Zenith Laser Sniper Rifle | Snow Snipe |

## Vehicle

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | Argo ATLS | ATLS Cool Metal Color, ATLS Orange Line, ATLS Snowland Color, Make ATLS shoot |
| ✅ | Argo ATLS GEO | Make jumpy ATLS shoot |
| ⬜ | Argo ATLS IKTI | Make jumpy ATLS shoot |

## Component

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | ASD Secure Drive | Asgard Fight Mod, Golem Rocks, Hide Snow Suit, New Move Big Starlancer Ship, Now make Polaris. Short Time Deal., Shiny Builder Suit, Snow Snipe, Special Idris For Killing, Starlifter A2 War Mod, What is Terrapin? |
| ✅ | DCHS-05 Orbital Positioning Comp-Board | Build a Mod Scorpius, Firebird Mod, Guardian take down ship, New Move Big Starlancer Ship, Now make Polaris. Short Time Deal., Peregrine Wikelo Mod, Sneaky Stabber, Special Idris For Killing, Starlifter A2 War Mod, Wikelo Navy F7 |
| ⬜ | Metamaterial Test #146 | Clipper Fight Now, Extra Special Wolf |
| ⬜ | RCMBNT-PWL-1 | Asgard Fight Mod, Do Lava Suit, Now make Polaris. Short Time Deal., Special Idris For Killing |
| ⬜ | RCMBNT-PWL-2 | Asgard Fight Mod, Do Lava Suit, Now make Polaris. Short Time Deal., Special Idris For Killing |
| ⬜ | RCMBNT-PWL-3 | Asgard Fight Mod, Do Lava Suit, Now make Polaris. Short Time Deal., Special Idris For Killing |
| ⬜ | RCMBNT-RGL-1 | Asgard Fight Mod, Now make Polaris. Short Time Deal., Shiny Builder Suit, Special Idris For Killing |
| ⬜ | RCMBNT-RGL-2 | Asgard Fight Mod, Now make Polaris. Short Time Deal., Shiny Builder Suit, Special Idris For Killing |
| ⬜ | RCMBNT-RGL-3 | Asgard Fight Mod, Now make Polaris. Short Time Deal., Shiny Builder Suit, Special Idris For Killing |
| ⬜ | RCMBNT-XTL-1 | Asgard Fight Mod, Now make Polaris. Short Time Deal., Special Idris For Killing |
| ⬜ | RCMBNT-XTL-2 | Asgard Fight Mod, Now make Polaris. Short Time Deal., Special Idris For Killing |
| ⬜ | RCMBNT-XTL-3 | Asgard Fight Mod, Now make Polaris. Short Time Deal., Special Idris For Killing |

## Creature material

| Image | Required item | Contract(s) |
|:---:|---|---|
| ⬜ | Bluemoon Fungus | Hot Shot |
| ⬜ | Irradiated Kopion Horn | Ready for RAFT? |
| ⬜ | Irradiated Valakkar Fang (Adult) | Make VOLT shotgun angrier, Ready for RAFT? |
| ⬜ | Irradiated Valakkar Fang (Apex) | Make ATLS shoot, Now make Polaris. Short Time Deal., Special Idris For Killing |
| ⬜ | Irradiated Valakkar Fang (Juvenile) | Fun Military Skull Gun, Make VOLT shotgun angrier, Ready for RAFT? |
| ⬜ | Irradiated Valakkar Pearl (Grade AA) | Guardian Fight Mod, Guardian take down ship, Trade Worm Parts for Favors? |
| ⬜ | Irradiated Valakkar Pearl (Grade AAA) | ATLS Snowland Color, F8 War Mod, Make glowy armor, More than a Max, New Move Big Starlancer Ship, Now make Polaris. Short Time Deal., Prowler More Utility, Ready for RAFT?, Sneaky Stabber, Special Idris For Killing, Starlifter A2 War Mod, Want Taurus ship |
| ✅ | Large Artifact Fragment (Pristine) | Guardian WiK-X, RSI Meteor Mod, Where Wolf? Here Wolf |
| ✅ | Tundra Kopion Horn | Fun Kopion Skull Gun, Wikelo Arrive to System |
| ✅ | Vanduul Metal | Armor with Vanduul, Curious Weapon, Guardian WiK-X, RSI Meteor Mod, Where Wolf? Here Wolf |
| ✅ | Vanduul Plating | Armor with Vanduul, Curious Weapon, Guardian WiK-X, RSI Meteor Mod, Where Wolf? Here Wolf |
| ✅ | Yormandi Eye | Prowler More Utility, Sneaky Starfighter Ion, Test Armor, Yormandi Gun |
| ✅ | Yormandi Tongue | Prowler More Utility, Starfighter Inferno Special, Test Armor, Yormandi Gun |

## Collectible

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | Government Cartography Agency Medal (Pristine) | Firebird Mod, Upgrade Intrepid, Want Taurus ship, Wikelo Navy F7 |
| ✅ | Tevarin War Service Marker (Pristine) | Build a Mod Scorpius, F8 War Mod, Guardian Fight Mod, New Move Big Starlancer Ship, Sneaky Starfighter Ion, Spirit Cargo mod, Starlifter A2 War Mod, What is Terrapin? |
| ✅ | UEE 6th Platoon Medal (Pristine) | Guardian take down ship, Now make Polaris. Short Time Deal., Special Idris For Killing, Starfighter Inferno Special, Zeus Special |

## Consumable

| Image | Required item | Contract(s) |
|:---:|---|---|
| ⬜ | Berry Blend Smoothie | Very Hungry |
| ✅ | Expired Quantanium Fuel Canister | Suit Up Take Off, Your Best Shot |
| ✅ | Vestal Water | Wikelo Arrive to System |

## Favor

| Image | Required item | Contract(s) |
|:---:|---|---|
| ✅ | Council Scrip | Trade Council Scrip for Favors? |
| ✅ | MG Scrip | Now make Polaris. Short Time Deal., Special Idris For Killing, Starlifter A2 War Mod, Trade Merc Scrip for Favors? |
| ⬜ | Polaris Bit | Now make Polaris. Short Time Deal., Special Idris For Killing, Starlifter A2 War Mod |
| ✅ | Wikelo Favor | ATLS Cool Metal Color, ATLS Orange Line, ATLS Snowland Color, Asgard Fight Mod, Build a Mod Scorpius, F8 War Mod, Firebird Mod, Fortune ship for you, Golem Rocks, Guardian Fight Mod, Guardian WiK-X, Guardian take down ship, Make ATLS shoot, Make a Ursa Mod, Make jumpy ATLS shoot, More than a Max, Most Special Wolf, New Move Big Starlancer Ship, Now make Polaris. Short Time Deal., Noxy Mod, Peregrine Wikelo Mod, Prospects Look Good, Prowler More Utility, Pulse Plus, RSI Meteor Mod, Ready for RAFT?, Red Fight Apollo, Red Fight Armor, Red Fight Shotgun, Sneaky Stabber, Sneaky Starfighter Ion, Special Idris For Killing, Spirit Cargo mod, Starfighter Inferno Special, Starlifter A2 War Mod, Suit Up Take Off, Upgrade Intrepid, Want Taurus ship, What is Terrapin?, Where Wolf? Here Wolf, Wikelo Navy F7, Your Best Shot, Zeus Cargo Special, Zeus Special |
