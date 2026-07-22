using WikeloContractor.Models.Api;
using WikeloContractor.Services;
using WikeloContractor.ViewModels;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// What the user sees while the catalog is syncing after a new game version.
/// <para>
/// These reproduce a real incident: a new API build shipped, the refresh started, and the app gave
/// no sign of it — the sync badge stayed green, no progress was shown, every action stayed live,
/// and the category filters matched nothing because the rebuilt contracts had not been enriched.
/// </para>
/// <para>
/// The journey each scenario replays is the one that caused it: Settings → "Check for updates"
/// (<c>forceRefresh: true</c>) discovers the new version, then the user opens the Catalog page,
/// which serves the freshly refetched but still un-enriched contracts.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class CatalogSyncScenarios
{
    private readonly WpfAppFixture _app;

    public CatalogSyncScenarios(WpfAppFixture app) => _app = app;

    /// <summary>Three contracts whose categories and real requirements only exist after enrichment.</summary>
    private static ScriptedWikiApi ShipCatalog()
    {
        var api = new ScriptedWikiApi
        {
            Missions =
            [
                ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special"),
                ScriptedWikiApi.Mission("m2", "Testudo Armor Trade"),
                ScriptedWikiApi.Mission("m3", "Paint Job"),
            ],
        };

        api.MissionDetails["m1"] = new MissionDetailDto
        {
            Uuid = "m1",
            RewardItems = [new RewardItemDto { Name = "Asgard", Uuid = "ship-1", Amount = 1 }],
            // Richer than the summary: an SCU amount, plus an entry the summary omits entirely.
            HaulingOrders =
            [
                new HaulingOrderDto { Name = "Gold", MinScu = 36, MaxScu = 36 },
                new HaulingOrderDto { Name = "Wikelo Favor", MaxAmount = 2 },
            ],
        };
        api.MissionDetails["m2"] = new MissionDetailDto
        {
            Uuid = "m2",
            RewardItems = [new RewardItemDto { Name = "Testudo Helmet", Uuid = "item-1", Amount = 1 }],
        };
        api.MissionDetails["m3"] = new MissionDetailDto { Uuid = "m3" };

        api.Classifications["ship-1"] = new("Asgard", null, IsSpaceship: true, IsVehicleRecord: true, Images: []);
        api.Classifications["item-1"] = new(
            "Testudo Helmet", "Char_Armor_Helmet", IsSpaceship: false, IsVehicleRecord: false, Images: []);

        return api;
    }

    /// <summary>First run: load and let enrichment settle, leaving a complete cache behind.</summary>
    private static async Task SettleAsync(CatalogHarness harness)
    {
        var enriched = new TaskCompletionSource();
        void OnUpdated(object? s, EventArgs e) => enriched.TrySetResult();

        harness.Catalog.CatalogUpdated += OnUpdated;
        try
        {
            _ = await harness.Catalog.GetContractsAsync();
            await enriched.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally
        {
            harness.Catalog.CatalogUpdated -= OnUpdated;
        }
    }

    /// <summary>
    /// Replays the incident up to the point the user is looking at an un-enriched catalog:
    /// a new build is published, Settings checks for updates, the Catalog page is opened.
    /// The API stays held, so enrichment is genuinely in flight when the test asserts.
    /// </summary>
    private async Task ArriveMidSyncAsync(CatalogHarness harness)
    {
        harness.Api.BumpVersion("4.3.0-LIVE.222");
        harness.Api.HoldEnrichment();

        _ = harness.Catalog.GetContractsAsync(forceRefresh: true);

        await _app.WaitUntilAsync(
            () => harness.Catalog.SyncState.IsSyncing,
            "enrichment to start after the version bump");

        await _app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());
    }

    private static ContractCardViewModel Card(CatalogViewModel vm, string title) =>
        vm.Contracts!.Cast<ContractCardViewModel>().Single(c => c.Contract.Title == title);

    // S1 — the reported symptom: the green cloud never changed and no progress was shown.
    [Fact]
    public async Task Sync_after_a_version_bump_is_visible_and_clears_when_it_finishes()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, ShipCatalog());
        await SettleAsync(harness);
        await ArriveMidSyncAsync(harness);

        await _app.OnUiAsync(() =>
        {
            Assert.True(harness.Catalogue.IsSyncing);
            Assert.False(harness.Catalogue.IsSynced);
            Assert.Equal(CatalogSyncPhase.Contracts, harness.Catalogue.SyncState.Phase);
            Assert.Equal(3, harness.Catalogue.SyncState.Total);

            // The shell shows the progress and holds the app-wide lock.
            Assert.True(harness.Shell.IsSyncing);
            Assert.False(harness.Shell.IsNavigationEnabled);
            Assert.NotNull(harness.Shell.SyncProgressText);
        });

        harness.Api.ReleaseEnrichment();
        await _app.WaitUntilAsync(() => !harness.Catalogue.IsSyncing, "the sync to finish");

        await _app.OnUiAsync(() =>
        {
            Assert.True(harness.Catalogue.IsSynced);
            Assert.Equal(CatalogSyncState.Idle, harness.Catalogue.SyncState);

            // The lock must lift with the sync, or the app is stuck.
            Assert.True(harness.Shell.IsNavigationEnabled);
        });
    }

    // S2 — "during the rebuild the categories showed the wrong contracts".
    [Fact]
    public async Task Category_filter_is_blocked_while_contracts_are_unenriched()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, ShipCatalog());
        await SettleAsync(harness);
        await ArriveMidSyncAsync(harness);

        await _app.OnUiAsync(() =>
        {
            // Un-enriched contracts carry ContractCategory.Unknown, which no filter entry matches,
            // so category filtering silently returns nothing. The UI must block rather than lie.
            Assert.True(
                harness.Catalogue.IsSyncing,
                "the catalog must be blocked while enrichment has not produced the data filters read");
        });

        harness.Api.ReleaseEnrichment();
        await _app.WaitUntilAsync(() => !harness.Catalogue.IsSyncing, "the sync to finish");

        await _app.OnUiAsync(() =>
        {
            harness.Catalogue.CategoryIndex = 1; // Ships
            var shown = harness.Catalogue.Contracts!.Cast<ContractCardViewModel>().ToList();
            Assert.Single(shown);
            Assert.Equal("Asgard Wikelo War Special", shown[0].Contract.Title);
        });
    }

    // S3 — the unreported, worst defect: completing mid-sync deducts the wrong amounts, for good.
    [Fact]
    public async Task Completing_a_contract_is_refused_while_its_requirements_are_incomplete()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, ShipCatalog());
        await SettleAsync(harness);

        // Covers the summary requirement ("Gold × 1") exactly, so the card looks ready — but the
        // real order enrichment will reveal is 36 SCU of Gold plus 2 Wikelo Favor.
        await harness.Inventory.SetCountAsync("Gold", 1);
        await ArriveMidSyncAsync(harness);

        await _app.OnUiAsync(() =>
        {
            var card = Card(harness.Catalogue, "Asgard Wikelo War Special");

            Assert.True(card.IsReady, "the summary requirements alone do look satisfied — that is the trap");
            Assert.False(
                card.ToggleCompletedCommand.CanExecute(null),
                "completing mid-sync would deduct the summary requirements, not the real hauling orders");
        });

        harness.Api.ReleaseEnrichment();
        await _app.WaitUntilAsync(() => !harness.Catalogue.IsSyncing, "the sync to finish");

        await _app.OnUiAsync(() =>
        {
            var card = Card(harness.Catalogue, "Asgard Wikelo War Special");

            // Now judged against the complete requirement list: two entries, not one, and the
            // inventory covers neither — refused for the honest reason.
            Assert.Equal(2, card.Contract.Requirements.Count);
            Assert.False(card.IsReady);
            Assert.False(card.ToggleCompletedCommand.CanExecute(null));
        });
    }

    // S4 — enrichment swallows its exceptions. If the sync state is not reset in a finally, an
    // aborted run leaves the catalog blocked forever and the app is unusable until restart.
    [Fact]
    public async Task Aborted_enrichment_releases_the_catalog_instead_of_blocking_it_forever()
    {
        var api = ShipCatalog();
        using var harness = await CatalogHarness.CreateAsync(_app, api);
        await SettleAsync(harness);

        api.BumpVersion("4.3.0-LIVE.222");

        // Two consecutive 429s once enrichment is under way: the service retries a 429 once,
        // and the second one aborts the whole run.
        api.ThrowRateLimitedOnCall(api.TotalCalls + 3, TimeSpan.FromMilliseconds(50), times: 2);

        _ = harness.Catalog.GetContractsAsync(forceRefresh: true);
        await _app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());

        // Assert the block goes up before asserting it comes down, so this cannot pass by
        // never having blocked at all.
        await _app.WaitUntilAsync(() => harness.Catalogue.IsSyncing, "the sync to start");

        await _app.WaitUntilAsync(
            () => !harness.Catalogue.IsSyncing,
            "the aborted sync to release the catalog");

        await _app.OnUiAsync(() =>
        {
            Assert.Equal(CatalogSyncState.Idle, harness.Catalogue.SyncState);

            // The whole app is locked during a sync, so an abort that forgot to release it
            // would leave the user with no navigation at all until a restart.
            Assert.True(harness.Shell.IsNavigationEnabled);
        });
    }
}
