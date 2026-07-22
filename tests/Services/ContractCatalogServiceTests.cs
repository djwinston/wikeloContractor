using System.IO;
using WikeloContractor.Models.Api;
using WikeloContractor.Services;
using WikeloContractor.Services.Api;
using WikeloContractor.Tests.E2E;
using Xunit;
using static WikeloContractor.Tests.E2E.ScriptedWikiApi;

namespace WikeloContractor.Tests.Services;

public sealed class ContractCatalogServiceTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    [Fact]
    public async Task First_load_fetches_missions_and_filters_unreleased_placeholders()
    {
        var client = new ScriptedWikiApi
        {
            Missions = [Mission("a", "Real contract"), Mission("b", "<= UNINITIALIZED =>", released: false)],
        };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var result = await service.GetContractsAsync();

        var contract = Assert.Single(result.Contracts);
        Assert.Equal("Real contract", contract.Title);
        Assert.Equal(250, contract.ReputationAmount);
        Assert.Equal("4.2.0-LIVE.111", result.GameVersion);
        Assert.Equal(CatalogStatus.Online, result.Status);
    }

    [Fact]
    public async Task Rate_limit_without_cache_propagates_and_reports_padded_wait()
    {
        var client = new ScriptedWikiApi();
        client.AlwaysRateLimited(TimeSpan.FromSeconds(30));
        var service = new ContractCatalogService(client, _cacheDirectory);

        var raised = false;
        service.RateLimitChanged += (_, _) => raised = true;

        _ = await Assert.ThrowsAsync<ApiRateLimitedException>(() => service.GetContractsAsync());

        Assert.True(raised);

        // Retry-After (30 s) + safety margin (5 s), minus the little time elapsed since.
        Assert.NotNull(service.RateLimitedUntil);
        var remaining = service.RateLimitedUntil.Value - DateTimeOffset.UtcNow;
        Assert.InRange(remaining, TimeSpan.FromSeconds(33), TimeSpan.FromSeconds(35));
    }

    [Fact]
    public async Task While_rate_limit_window_is_open_no_api_call_is_made()
    {
        var client = new ScriptedWikiApi();
        client.AlwaysRateLimited(TimeSpan.FromSeconds(30));
        var service = new ContractCatalogService(client, _cacheDirectory);

        _ = await Assert.ThrowsAsync<ApiRateLimitedException>(() => service.GetContractsAsync());
        Assert.Equal(1, client.VersionCalls);

        // Still inside the window — the gate must block before reaching the client.
        _ = await Assert.ThrowsAsync<ApiRateLimitedException>(() => service.GetContractsAsync());
        Assert.Equal(1, client.VersionCalls);
    }

    [Fact]
    public async Task Rate_limited_refresh_serves_cache_and_flags_result()
    {
        var client = new ScriptedWikiApi { Missions = [Mission("a", "Real contract")] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        // Let background enrichment finish before the next assertions touch the cache.
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        client.AlwaysRateLimited();
        var limited = await service.GetContractsAsync(forceRefresh: true);

        Assert.Equal(CatalogStatus.RateLimited, limited.Status);
        Assert.Single(limited.Contracts);

        // Window is open — a second forced refresh serves the cache without an API call.
        var callsBefore = client.VersionCalls;
        var gated = await service.GetContractsAsync(forceRefresh: true);

        Assert.Equal(CatalogStatus.RateLimited, gated.Status);
        Assert.Single(gated.Contracts);
        Assert.Equal(callsBefore, client.VersionCalls);
    }

    [Fact]
    public async Task Same_game_version_does_not_refetch_missions()
    {
        var client = new ScriptedWikiApi { Missions = [Mission("a", "Real contract")] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        _ = await service.GetContractsAsync();
        _ = await service.GetContractsAsync(forceRefresh: true);

        Assert.Equal(2, client.VersionCalls);
        Assert.Equal(1, client.MissionsCalls);
    }

    [Fact]
    public async Task Enrichment_attaches_item_images_to_rewards()
    {
        var client = new ScriptedWikiApi
        {
            Missions = [Mission("m1", "Armor exchange")],
            MissionDetails = new()
            {
                ["m1"] = new MissionDetailDto
                {
                    Uuid = "m1",
                    RewardItems = [new RewardItemDto { Name = "Testudo Helmet", Uuid = "item-1", Amount = 1 }],
                    HaulingOrders =
                    [
                        new HaulingOrderDto { Name = "Quantainium", MinScu = 36, MaxScu = 36 },
                        new HaulingOrderDto { Name = "Wikelo Favor", MaxAmount = 1 },
                    ],
                },
            },
            Classifications = new()
            {
                ["item-1"] = new ItemClassification(
                    "Testudo Helmet",
                    "Char_Armor_Helmet",
                    IsSpaceship: false,
                    IsVehicleRecord: false,
                    Images:
                    [
                        new WikeloContractor.Models.RewardImage
                        {
                            Source = "starcitizen.tools",
                            ThumbnailUrl = "https://media.starcitizen.tools/thumb/t.webp",
                            OriginalUrl = "https://media.starcitizen.tools/t.png",
                        },
                    ],
                    Details: new WikeloContractor.Models.RewardDetails { Manufacturer = "CC's Conversions", Rarity = "Rare" }),
            },
        };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var contract = Assert.Single(service.Current!.Contracts);
        var reward = Assert.Single(contract.Rewards);
        var image = Assert.Single(reward.Images);
        Assert.Equal("starcitizen.tools", image.Source);
        Assert.Equal("https://media.starcitizen.tools/thumb/t.webp", image.ThumbnailUrl);
        Assert.Equal("https://media.starcitizen.tools/t.png", image.OriginalUrl);
        Assert.Equal("CC's Conversions", reward.Details?.Manufacturer);
        Assert.Equal("Rare", reward.Details?.Rarity);

        // hauling_orders replace the summary-based requirements (SCU amounts, extra entries).
        Assert.Equal(2, contract.Requirements.Count);
        Assert.Equal("36 SCU", contract.Requirements[0].AmountLabel);
        Assert.Equal("Wikelo Favor", contract.Requirements[1].Name);
        Assert.Equal("1", contract.Requirements[1].AmountLabel);
    }

    [Fact]
    public async Task Null_blueprints_do_not_abort_enrichment()
    {
        // The live API sends "blueprints": null for contracts without any (most of them);
        // enrichment must still complete and leave the blueprint list empty.
        var client = new ScriptedWikiApi
        {
            Missions = [Mission("m1", "Armor exchange")],
            MissionDetails = new()
            {
                ["m1"] = new MissionDetailDto
                {
                    Uuid = "m1",
                    RewardItems = [new RewardItemDto { Name = "Testudo Helmet", Uuid = "item-1", Amount = 1 }],
                    Blueprints = null,
                },
            },
        };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var contract = Assert.Single(service.Current!.Contracts);
        Assert.Single(contract.Rewards);
        Assert.Empty(contract.Blueprints);
    }

    [Fact]
    public async Task Enrichment_captures_distinct_blueprint_names_across_pools()
    {
        var client = new ScriptedWikiApi
        {
            Missions = [Mission("m1", "Suit Up Take Off")],
            MissionDetails = new()
            {
                ["m1"] = new MissionDetailDto
                {
                    Uuid = "m1",
                    Blueprints =
                    [
                        new BlueprintPoolDto { Items = [new BlueprintItemDto { Name = "Tailwind Flight Suit Dominion Camo" }] },
                        new BlueprintPoolDto
                        {
                            Items =
                            [
                                new BlueprintItemDto { Name = "Tailwind Flight Helmet Dominion Camo" },
                                // Duplicate across pools collapses to one entry.
                                new BlueprintItemDto { Name = "Tailwind Flight Suit Dominion Camo" },
                            ],
                        },
                    ],
                },
            },
        };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var contract = Assert.Single(service.Current!.Contracts);
        Assert.Equal(
            ["Tailwind Flight Suit Dominion Camo", "Tailwind Flight Helmet Dominion Camo"],
            contract.Blueprints);
    }

    [Fact]
    public async Task Mission_without_reputation_maps_to_zero_amount()
    {
        var mission = Mission("idris", "Special Idris For Killing");
        mission.ReputationGained = null;
        mission.ReputationAmount = null;
        var client = new ScriptedWikiApi { Missions = [mission] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var result = await service.GetContractsAsync();

        var contract = Assert.Single(result.Contracts);
        Assert.Equal(0, contract.ReputationAmount);
    }

    [Fact]
    public async Task Once_only_contract_is_categorized_as_other_regardless_of_rewards()
    {
        var client = new ScriptedWikiApi
        {
            Missions = [Mission("m1", "Wikelo Arrive to System", onceOnly: true)],
            MissionDetails = new()
            {
                ["m1"] = new MissionDetailDto
                {
                    Uuid = "m1",
                    RewardItems = [new RewardItemDto { Name = "Coda Pistol", Uuid = "item-1", Amount = 1 }],
                },
            },
            Classifications = new()
            {
                // A weapon reward would normally drive the category to Weapon.
                ["item-1"] = new ItemClassification(
                    "Coda Pistol", "WeaponPersonal", IsSpaceship: false, IsVehicleRecord: false, Images: []),
            },
        };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var contract = Assert.Single(service.Current!.Contracts);
        Assert.True(contract.OnceOnly);
        Assert.Equal(WikeloContractor.Models.ContractCategory.Other, contract.Category);
        // The full set still lists every reward category, so the filter can surface the
        // contract under Weapons too.
        Assert.Contains(WikeloContractor.Models.ContractCategory.Other, contract.Categories);
        Assert.Contains(WikeloContractor.Models.ContractCategory.Weapon, contract.Categories);
    }

    [Fact]
    public async Task Enrichment_patches_gun_entries_with_looked_up_kind_and_grade()
    {
        var client = new ScriptedWikiApi
        {
            Missions = [Mission("m1", "Ship trade")],
            MissionDetails = new()
            {
                ["m1"] = new MissionDetailDto
                {
                    Uuid = "m1",
                    RewardItems = [new RewardItemDto { Name = "Some Ship", Uuid = "ship-1", Amount = 1 }],
                },
            },
            Classifications = new()
            {
                ["ship-1"] = new ItemClassification(
                    "Some Ship", null, IsSpaceship: true, IsVehicleRecord: true, Images: [],
                    Details: new WikeloContractor.Models.RewardDetails
                    {
                        Weapons =
                        [
                            // A fixed gun (no Type) and a mount (has Type) — only the gun is looked up.
                            new WikeloContractor.Models.ShipLoadoutEntry { Name = "CF-337 Panther Repeater", Count = 2 },
                            new WikeloContractor.Models.ShipLoadoutEntry { Name = "MSD-683 Missile Rack", Type = "MissileLauncher", Size = 7 },
                        ],
                    }),
            },
            WeaponInfo = new(StringComparer.OrdinalIgnoreCase)
            {
                ["CF-337 Panther Repeater"] = new VehicleWeaponInfo("Laser Repeater", 3, "A"),
            },
        };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var contract = Assert.Single(service.Current!.Contracts);
        var weapons = Assert.Single(contract.Rewards).Details!.Weapons!;
        Assert.Collection(
            weapons,
            gun =>
            {
                Assert.Equal("Laser Repeater", gun.TypeLabel);
                Assert.Equal(3, gun.Size);
                Assert.Equal("A", gun.Grade);
                Assert.Equal(2, gun.Count);
            },
            mount => Assert.Null(mount.TypeLabel));
    }

    [Fact]
    public async Task Cache_with_older_schema_version_is_discarded_and_refetched()
    {
        // Pre-v2 cache: no SchemaVersion field (reads as 0), same game version as the API.
        _ = Directory.CreateDirectory(_cacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(_cacheDirectory, "contracts.json"), """
            {
              "GameVersion": "4.2.0-LIVE.111",
              "FetchedAt": "2026-07-01T00:00:00+00:00",
              "LastVersionCheckAt": "2099-01-01T00:00:00+00:00",
              "Enriched": true,
              "Contracts": []
            }
            """);

        var client = new ScriptedWikiApi { Missions = [Mission("a", "Real contract")] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var result = await service.GetContractsAsync();

        // The stale-schema cache must not be trusted even with a fresh version check timestamp.
        Assert.Equal(1, client.MissionsCalls);
        Assert.Single(result.Contracts);
    }

    [Fact]
    public async Task Current_schema_cache_is_reused_by_a_new_service_instance()
    {
        var client = new ScriptedWikiApi { Missions = [Mission("a", "Real contract")] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();
        _ = await service.GetContractsAsync();
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Fresh instance over the same cache directory (new app run).
        var secondClient = new ScriptedWikiApi { Missions = [Mission("a", "Real contract")] };
        var secondService = new ContractCatalogService(secondClient, _cacheDirectory);

        var result = await secondService.GetContractsAsync();

        Assert.Single(result.Contracts);
        Assert.Equal(0, secondClient.MissionsCalls);
    }
}
