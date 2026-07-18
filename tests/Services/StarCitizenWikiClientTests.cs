using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using WikeloContractor.Services.Api;
using Xunit;

namespace WikeloContractor.Tests.Services;

public class StarCitizenWikiClientTests
{
    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
    };

    private static StarCitizenWikiClient CreateClient(StubHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task Missions_response_is_parsed_from_api_shape()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": [
                {
                  "uuid": "abc-123",
                  "title": "A Hauling Job",
                  "description": "Bring stuff to Wikelo.",
                  "once_only": true,
                  "has_prerequisites": false,
                  "released": true,
                  "hauling_summary": [
                    { "name": "Quantainium", "min_amount": 2, "max_amount": 2 },
                    { "name": "Gold", "min_amount": null, "max_amount": null }
                  ],
                  "reputation_gained": [
                    { "faction": "Wikelo Emporium", "scope": "Wikelo", "amount": 250 }
                  ],
                  "reputation_amount": null,
                  "game_version": "4.2.0",
                  "web_url": "https://example.test/mission"
                }
              ],
              "meta": { "current_page": 1, "per_page": 200, "total": 1 }
            }
            """));

        var missions = await CreateClient(handler).GetWikeloMissionsAsync();

        var mission = Assert.Single(missions);
        Assert.Equal("abc-123", mission.Uuid);
        Assert.True(mission.OnceOnly);
        Assert.True(mission.Released);
        Assert.Equal(2, mission.HaulingSummary.Count);
        Assert.Equal(2, mission.HaulingSummary[0].MinAmount);
        Assert.Null(mission.HaulingSummary[1].MinAmount);
        Assert.NotNull(mission.ReputationGained);
        Assert.Equal(250, mission.ReputationGained.Single(r => r.Scope == "Wikelo").Amount);
    }

    [Fact]
    public async Task Missions_request_targets_wikelo_giver_with_full_page_and_app_user_agent()
    {
        var handler = new StubHandler(_ => Json("""{ "data": [] }"""));

        _ = await CreateClient(handler).GetWikeloMissionsAsync();

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("filter[mission_giver]=Wikelo", uri);
        Assert.Contains("page[size]=200", uri);
        Assert.Contains("WikeloContractor", handler.LastRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Mission_with_null_reputation_gained_is_parsed()
    {
        // Top-rank trades (e.g. the Idris contract) grant no reputation: explicit JSON null.
        var handler = new StubHandler(_ => Json("""
            {
              "data": [
                {
                  "uuid": "idris-1",
                  "title": "Special Idris For Killing",
                  "released": true,
                  "hauling_summary": [{ "name": "Wikelo Favor", "min_amount": 50, "max_amount": 50 }],
                  "reputation_gained": null,
                  "reputation_amount": null
                }
              ]
            }
            """));

        var missions = await CreateClient(handler).GetWikeloMissionsAsync();

        var mission = Assert.Single(missions);
        Assert.Null(mission.ReputationGained);
    }

    [Fact]
    public async Task Rate_limited_response_throws_with_retry_after()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var ex = await Assert.ThrowsAsync<ApiRateLimitedException>(
            () => CreateClient(handler).GetWikeloMissionsAsync());

        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task Rate_limited_response_without_header_has_no_retry_after()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<ApiRateLimitedException>(
            () => CreateClient(handler).GetItemClassificationAsync("some-uuid"));

        Assert.Null(ex.RetryAfter);
    }

    [Fact]
    public async Task Current_game_version_is_the_default_entry()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": [
                { "code": "4.1.1-LIVE.111", "channel": "LIVE", "is_default": false },
                { "code": "4.2.0-LIVE.222", "channel": "LIVE", "is_default": true },
                { "code": "4.3.0-PTU.333", "channel": "PTU", "is_default": false }
              ]
            }
            """));

        var version = await CreateClient(handler).GetCurrentGameVersionAsync();

        Assert.Equal("4.2.0-LIVE.222", version);
    }

    [Fact]
    public async Task Item_with_string_type_is_classified_by_type()
    {
        var handler = new StubHandler(_ => Json("""
            { "data": { "name": "Arrowhead", "type": "WeaponPersonal" } }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        Assert.NotNull(classification);
        Assert.Equal("WeaponPersonal", classification.TypeString);
        Assert.False(classification.IsSpaceship);
        Assert.False(classification.IsVehicleRecord);
    }

    [Fact]
    public async Task Item_images_are_parsed_and_entries_without_urls_are_skipped()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Testudo Helmet Clanguard",
                "type": "Char_Armor_Helmet",
                "images": [
                  {
                    "source": "starcitizen.tools",
                    "thumbnail_url": "https://media.starcitizen.tools/thumb/t.webp",
                    "original_url": "https://media.starcitizen.tools/t.png",
                    "thumbnail_width": 600,
                    "thumbnail_height": 600
                  },
                  { "source": "broken", "thumbnail_url": null, "original_url": null }
                ]
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        Assert.NotNull(classification);
        var image = Assert.Single(classification.Images);
        Assert.Equal("starcitizen.tools", image.Source);
        Assert.Equal("https://media.starcitizen.tools/thumb/t.webp", image.ThumbnailUrl);
        Assert.Equal("https://media.starcitizen.tools/t.png", image.OriginalUrl);
    }

    [Fact]
    public async Task Item_details_are_parsed_from_api_shape()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Ana Arms Endro",
                "type": "Char_Armor_Arms",
                "type_label": "Arms (Armor)",
                "sub_type_label": "Heavy",
                "rarity": "Common",
                "description": { "en_EN": "Modified Antium Armor.", "de_DE": "..." },
                "manufacturer": { "name": "Quirinus Tech", "code": "QRT" },
                "clothing": {
                  "slot": "Arms",
                  "damage_resistance_map": {
                    "physical": 0.6, "physical_change": -0.4,
                    "stun": 0.4, "stun_change": -0.6
                  }
                },
                "temperature_resistance": { "min": -95, "max": 120 },
                "radiation_resistance": { "maximum_radiation_capacity": 26800, "radiation_dissipation_rate": 145.8 }
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        var details = classification!.Details!;
        Assert.Equal("Modified Antium Armor.", details.Description);
        Assert.Equal("Quirinus Tech", details.Manufacturer);
        Assert.Equal("Arms (Armor)", details.TypeLabel);
        Assert.Equal("Heavy", details.SubTypeLabel);
        Assert.Equal("Common", details.Rarity);
        Assert.Equal(new Dictionary<string, double> { ["physical"] = 0.6, ["stun"] = 0.4 }, details.DamageResistances);
        Assert.Equal(-95, details.TemperatureMin);
        Assert.Equal(120, details.TemperatureMax);
        Assert.Equal(26800, details.RadiationCapacity);
        Assert.Equal(145.8, details.RadiationDissipationRate);
        Assert.Null(details.CargoCapacityScu);
    }

    [Fact]
    public async Task Vehicle_details_are_parsed_from_api_shape()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Syulen",
                "is_spaceship": true,
                "description": { "en_EN": "Artfully crafted by House Gatac." },
                "manufacturer": { "name": "Gatac Manufacture", "code": "GAMA" },
                "career": "Multi-Role",
                "role": "Starter / Pathfinder",
                "cargo_capacity": 6,
                "crew": { "min": 1, "max": 1 },
                "health": 11740,
                "shield_hp": 4320,
                "speed": { "scm": 225, "max": 1175 },
                "msrp": 70,
                "pledge_url": "https://robertsspaceindustries.com/pledge/ships/syulen/Syulen",
                "weaponry": {
                  "fixed_weapons": {
                    "weapons": [
                      { "name": "CF-337 Panther Repeater", "dps": 545.6 },
                      { "name": "CF-337 Panther Repeater", "dps": 545.6 },
                      { "name": "Attrition-3 Repeater", "dps": 501.7 }
                    ]
                  },
                  "missiles": { "count": 16, "damage": { "total": 51200 } }
                },
                "ports": [
                  { "name": "hardpoint_shield_generator_left", "equipped_item": { "name": "CoverAll", "type": "Shield", "size": 2, "grade": "B", "class": "Military" } },
                  { "name": "hardpoint_shield_generator_right", "equipped_item": { "name": "CoverAll", "type": "Shield", "size": 2, "grade": "B", "class": "Military" } },
                  { "name": "hardpoint_quantum_drive", "equipped_item": { "name": "Yeager", "type": "QuantumDrive", "size": 2 } },
                  {
                    "name": "hardpoint_weapon_left",
                    "equipped_item": { "name": "VariPuck S3 Gimbal Mount", "type": "Turret", "size": 3, "grade": "A" },
                    "ports": [
                      { "name": "hardpoint_class_2", "equipped_item": { "name": "CF-337 Panther Repeater", "type": "WeaponGun", "size": 3, "grade": "A", "vehicle_weapon": { "type": "Laser Repeater" } } }
                    ]
                  },
                  {
                    "name": "hardpoint_turret_top",
                    "equipped_item": { "name": "MSD-683 Missile Rack", "type": "MissileLauncher", "size": 7 },
                    "ports": [
                      { "name": "missile_01_attach", "equipped_item": { "name": "Arrester III Missile", "type": "Missile", "size": 3, "grade": "A", "missile": { "signal_type": "CrossSection" } } },
                      { "name": "missile_02_attach", "equipped_item": { "name": "Arrester III Missile", "type": "Missile", "size": 3, "grade": "A", "missile": { "signal_type": "CrossSection" } } }
                    ]
                  },
                  { "name": "hardpoint_fuel_intake", "equipped_item": { "name": "<= PLACEHOLDER =>", "type": "FuelIntake", "size": 1 } },
                  { "name": "hardpoint_paint", "equipped_item": { "name": "", "type": "" } },
                  { "name": "hardpoint_controller_flight", "equipped_item": { "name": "Flight Blade", "type": "FlightController", "size": 1 } }
                ]
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        var details = classification!.Details!;
        Assert.Equal("Artfully crafted by House Gatac.", details.Description);
        Assert.Equal("Gatac Manufacture", details.Manufacturer);
        Assert.Equal("Multi-Role", details.Career);
        Assert.Equal("Starter / Pathfinder", details.Role);
        Assert.Equal(6, details.CargoCapacityScu);
        Assert.Equal(1, details.CrewMin);
        Assert.Equal(1, details.CrewMax);
        Assert.Equal(11740, details.Health);
        Assert.Equal(4320, details.ShieldHp);
        Assert.Equal(225, details.SpeedScm);
        Assert.Equal(1175, details.SpeedMax);
        Assert.Equal(70, details.Msrp);
        Assert.Equal("https://robertsspaceindustries.com/pledge/ships/syulen/Syulen", details.PledgeUrl);
        Assert.Null(details.Rarity);

        // Weapons: guns from nested ports (weaponry fallback unused), then loaded ordnance
        // with signal type, then top-level mounts/racks; placeholders skipped.
        Assert.NotNull(details.Weapons);
        Assert.Collection(
            details.Weapons,
            w => { Assert.Equal("CF-337 Panther Repeater", w.Name); Assert.Equal(1, w.Count); Assert.Equal("Laser Repeater", w.TypeLabel); Assert.Equal("A", w.Grade); },
            w => { Assert.Equal("Arrester III Missile", w.Name); Assert.Equal(2, w.Count); Assert.Equal("Missile", w.Type); Assert.Equal("CrossSection", w.TypeLabel); },
            w => { Assert.Equal("VariPuck S3 Gimbal Mount", w.Name); Assert.Equal("Turret", w.Type); Assert.Equal(3, w.Size); },
            w => { Assert.Equal("MSD-683 Missile Rack", w.Name); Assert.Equal(7, w.Size); });
        Assert.Equal(16, details.MissileCount);

        // Components: whitelisted types only (no fuel intakes / controllers), grouped with size.
        Assert.NotNull(details.Components);
        Assert.Collection(
            details.Components,
            c => { Assert.Equal("CoverAll", c.Name); Assert.Equal("Shield", c.Type); Assert.Equal(2, c.Count); Assert.Equal(2, c.Size); Assert.Equal("B", c.Grade); Assert.Equal("Military", c.Class); },
            c => { Assert.Equal("Yeager", c.Name); Assert.Equal("QuantumDrive", c.Type); Assert.Equal(1, c.Count); Assert.Null(c.Grade); });
    }

    [Fact]
    public async Task Vehicle_without_nested_gun_ports_falls_back_to_weaponry_names()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Old Record",
                "is_spaceship": true,
                "weaponry": {
                  "fixed_weapons": {
                    "weapons": [
                      { "name": "CF-337 Panther Repeater" },
                      { "name": "CF-337 Panther Repeater" }
                    ]
                  }
                }
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        var gun = Assert.Single(classification!.Details!.Weapons!);
        Assert.Equal("CF-337 Panther Repeater", gun.Name);
        Assert.Equal(2, gun.Count);
        // Name-only entry — enrichment looks the kind label up separately.
        Assert.Null(gun.TypeLabel);
        Assert.Null(gun.Type);
    }

    [Fact]
    public async Task Vehicle_weapon_info_is_looked_up_by_slug()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/api/items/cf-337-panther-repeater")
            ? Json("""
                { "data": { "name": "CF-337 Panther Repeater", "size": 3, "grade": "A", "vehicle_weapon": { "type": "Laser Repeater" } } }
                """)
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        var info = await CreateClient(handler).GetVehicleWeaponInfoAsync("CF-337 Panther Repeater");

        Assert.NotNull(info);
        Assert.Equal("Laser Repeater", info.TypeLabel);
        Assert.Equal(3, info.Size);
        Assert.Equal("A", info.Grade);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task Vehicle_weapon_slug_miss_falls_back_to_name_filter()
    {
        var handler = new StubHandler(request =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("filter[name]="))
            {
                return Json("""{ "data": [ { "uuid": "gun-uuid", "name": "Odd/Name Gun" } ] }""");
            }

            return uri.EndsWith("/api/items/gun-uuid")
                ? Json("""{ "data": { "name": "Odd/Name Gun", "size": 2, "grade": "C", "vehicle_weapon": { "type": "Ballistic Cannon" } } }""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var info = await CreateClient(handler).GetVehicleWeaponInfoAsync("Odd/Name Gun");

        Assert.NotNull(info);
        Assert.Equal("Ballistic Cannon", info.TypeLabel);
        Assert.Equal("C", info.Grade);
        Assert.Equal(3, handler.Requests);
    }

    [Fact]
    public async Task Vehicle_weapon_not_found_anywhere_returns_null()
    {
        var handler = new StubHandler(request => request.RequestUri!.ToString().Contains("filter[name]=")
            ? Json("""{ "data": [] }""")
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        var info = await CreateClient(handler).GetVehicleWeaponInfoAsync("Ghost Gun");

        Assert.Null(info);
    }

    [Fact]
    public async Task Item_without_images_field_yields_empty_image_list()
    {
        var handler = new StubHandler(_ => Json("""
            { "data": { "name": "Arrowhead", "type": "WeaponPersonal" } }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        Assert.NotNull(classification);
        Assert.Empty(classification.Images);
    }

    [Fact]
    public async Task Vehicle_variant_item_is_classified_via_its_base_variant()
    {
        // A shop-exclusive vehicle variant is a plain item record without vehicle flags;
        // the base variant UUID resolves to a full vehicle record.
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.Contains("base-uuid")
            ? Json("""
                {
                  "data": {
                    "name": "Pulse",
                    "type": { "en_EN": "ground" },
                    "is_spaceship": false,
                    "is_gravlev": true,
                    "manufacturer": { "name": "Mirai" },
                    "speed": { "scm": 48, "max": 130 },
                    "images": [{ "source": "base", "original_url": "https://example.test/base.png" }]
                  }
                }
                """)
            : Json("""
                {
                  "data": {
                    "name": "Mirai Pulse Wikelo Special",
                    "type": "Vehicle",
                    "sub_type": "Vehicle_Spaceship",
                    "description": "A Wikelo-exclusive Pulse.",
                    "base_variant": { "uuid": "base-uuid", "name": "Mirai Pulse" },
                    "images": [{ "source": "variant", "original_url": "https://example.test/variant.png" }]
                  }
                }
                """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("variant-uuid");

        Assert.NotNull(classification);
        Assert.False(classification.IsSpaceship);
        Assert.True(classification.IsVehicleRecord);
        // The variant keeps its own identity and images; stats come from the base record.
        Assert.Equal("Mirai Pulse Wikelo Special", classification.Name);
        Assert.Equal("variant", Assert.Single(classification.Images).Source);
        Assert.Equal("A Wikelo-exclusive Pulse.", classification.Details?.Description);
        Assert.Equal("Mirai", classification.Details?.Manufacturer);
        Assert.Equal(48, classification.Details?.SpeedScm);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task Vehicle_variant_without_vehicle_base_record_keeps_item_classification()
    {
        // Both the variant and its base come back item-shaped — no vehicle record to borrow from.
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Some Vehicle Item",
                "type": "Vehicle",
                "base_variant": { "uuid": "base-uuid" }
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("variant-uuid");

        Assert.NotNull(classification);
        Assert.False(classification.IsVehicleRecord);
        Assert.Equal("Vehicle", classification.TypeString);
    }

    [Fact]
    public async Task Vehicle_record_with_localized_type_object_is_flagged()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Golem",
                "type": { "en_EN": "Mining Vehicle" },
                "is_spaceship": false,
                "is_vehicle": true
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        Assert.NotNull(classification);
        Assert.Null(classification.TypeString);
        Assert.False(classification.IsSpaceship);
        Assert.True(classification.IsVehicleRecord);
    }
}
