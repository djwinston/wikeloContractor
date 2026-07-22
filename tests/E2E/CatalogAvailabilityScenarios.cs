using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// How the app behaves when the API is unreachable or refusing requests. Unlike the sync
/// scenarios these are expected to hold today — they are the regression net around the
/// offline and rate-limit paths the sync work touches.
/// </summary>
[Collection("WpfApp")]
public sealed class CatalogAvailabilityScenarios
{
    private readonly WpfAppFixture _app;

    public CatalogAvailabilityScenarios(WpfAppFixture app) => _app = app;

    private static ScriptedWikiApi SmallCatalog() => new()
    {
        Missions = [ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special")],
    };

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

    // S5 — launching with no connectivity over an existing cache: keep working, say so, do not block.
    [Fact]
    public async Task Launching_offline_serves_the_cache_without_claiming_to_be_synced_or_syncing()
    {
        using var first = await CatalogHarness.CreateAsync(_app, SmallCatalog());
        await SettleAsync(first);
        first.AgeCache();

        // Restart over the same cache with a dead API: a fresh service holds no envelope, so it
        // really does attempt the version check and really does fall back to the cache.
        var api = SmallCatalog();
        api.GoOffline();
        using var restarted = await CatalogHarness.CreateAsync(_app, api, first.Root);

        await _app.OnUiAsync(() => restarted.Catalogue.OnNavigatedToAsync());

        await _app.OnUiAsync(() =>
        {
            Assert.True(restarted.Catalogue.IsOffline);
            Assert.False(restarted.Catalogue.IsSynced);
            Assert.False(restarted.Catalogue.HasLoadError);
            Assert.Single(restarted.Catalogue.Contracts!.Cast<object>());

            // The cached contracts are already enriched, so nothing is missing and nothing
            // may be blocked — an offline launch must stay fully usable.
            Assert.False(restarted.Catalogue.IsSyncing);
        });
    }

    // S6 — HTTP 429 on a catalog load: cached data stays, and the shared countdown explains why.
    [Fact]
    public async Task Rate_limited_load_keeps_the_cache_and_starts_the_shared_countdown()
    {
        using var first = await CatalogHarness.CreateAsync(_app, SmallCatalog());
        await SettleAsync(first);
        first.AgeCache();

        var api = SmallCatalog();
        api.ThrowRateLimitedOnCall(1, TimeSpan.FromSeconds(30), times: int.MaxValue);
        using var restarted = await CatalogHarness.CreateAsync(_app, api, first.Root);

        await _app.OnUiAsync(() => restarted.Catalogue.OnNavigatedToAsync());

        await _app.WaitUntilAsync(
            () => restarted.Catalogue.RateLimit.IsActive,
            "the shared rate-limit countdown to open");

        await _app.OnUiAsync(() =>
        {
            Assert.Equal(CatalogStatus.RateLimited, restarted.Catalogue.Status);
            Assert.Single(restarted.Catalogue.Contracts!.Cast<object>());
            Assert.False(restarted.Catalogue.IsSynced);
            Assert.NotNull(restarted.Catalogue.RateLimit.Message);
        });
    }

    // S8 — the countdown promises "loading resumes"; something has to actually resume it.
    // Before the fix nothing did: the bar sat on "Resuming loading..." forever and the data only
    // refreshed if the user happened to navigate away and back.
    [Fact]
    public async Task Elapsed_rate_limit_window_refetches_and_closes_the_countdown()
    {
        using var first = await CatalogHarness.CreateAsync(_app, SmallCatalog());
        await SettleAsync(first);
        first.AgeCache();

        var api = SmallCatalog();
        // Refuse the first call only; the automatic retry after the window must get through.
        api.ThrowRateLimitedOnCall(1, TimeSpan.FromMilliseconds(100));
        using var restarted = await CatalogHarness.CreateAsync(_app, api, first.Root);

        await _app.OnUiAsync(() => restarted.Catalogue.OnNavigatedToAsync());

        await _app.WaitUntilAsync(
            () => restarted.Catalogue.RateLimit.IsActive,
            "the countdown to open");

        var callsWhileWaiting = api.VersionCalls;

        // No polling while the gate is shut, then exactly one refetch once it opens.
        await _app.WaitUntilAsync(
            () => api.VersionCalls > callsWhileWaiting,
            "the elapsed window to trigger a refetch");

        await _app.WaitUntilAsync(
            () => !restarted.Catalogue.RateLimit.IsActive,
            "the countdown to close after a clean load");

        await _app.OnUiAsync(() =>
        {
            Assert.Equal(CatalogStatus.Online, restarted.Catalogue.Status);
            Assert.True(restarted.Catalogue.IsSynced);
        });
    }

    // S7 — a 429 mid-enrichment: the gate holds every call, one retry gets through, sync finishes.
    [Fact]
    public async Task Rate_limit_during_enrichment_is_waited_out_and_the_sync_completes()
    {
        var api = new ScriptedWikiApi
        {
            Missions =
            [
                ScriptedWikiApi.Mission("m1", "First"),
                ScriptedWikiApi.Mission("m2", "Second"),
            ],
        };

        var cacheDirectory = Path.Combine(Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));
        var service = new ContractCatalogService(api, cacheDirectory, TimeSpan.FromMilliseconds(50));

        try
        {
            // Call 3 is the first mission-detail request; a single 429 there must be retried.
            api.ThrowRateLimitedOnCall(3, TimeSpan.FromMilliseconds(50));

            var enriched = new TaskCompletionSource();
            service.CatalogUpdated += (_, _) => enriched.TrySetResult();

            _ = await service.GetContractsAsync();
            await enriched.Task.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(2, service.Current!.Contracts.Count);
            Assert.Equal(CatalogSyncState.Idle, service.SyncState);

            // The gate reopened once the window elapsed, so both details were fetched:
            // two successful calls plus the one that was refused.
            Assert.Equal(3, api.DetailCalls);
        }
        finally
        {
            try
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: a leftover temp directory is harmless.
            }
        }
    }
}
