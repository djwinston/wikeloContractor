using System.IO;
using WikeloContractor.Models.Api;
using WikeloContractor.Services;
using WikeloContractor.Services.Api;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class ContractCatalogServiceTests : IDisposable
{
    private sealed class FakeApiClient : IStarCitizenWikiClient
    {
        public Func<string> VersionResponder { get; set; } = () => "4.2.0-LIVE.111";

        public List<MissionDto> Missions { get; set; } = [];

        public int VersionCalls { get; private set; }

        public int MissionsCalls { get; private set; }

        public Task<string> GetCurrentGameVersionAsync(CancellationToken cancellationToken = default)
        {
            VersionCalls++;
            return Task.FromResult(VersionResponder());
        }

        public Task<IReadOnlyList<MissionDto>> GetWikeloMissionsAsync(CancellationToken cancellationToken = default)
        {
            MissionsCalls++;
            return Task.FromResult<IReadOnlyList<MissionDto>>(Missions);
        }

        public Task<MissionDetailDto?> GetMissionDetailAsync(string missionUuid, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionDetailDto?>(null);

        public Task<ItemClassification?> GetItemClassificationAsync(string itemUuid, CancellationToken cancellationToken = default) =>
            Task.FromResult<ItemClassification?>(null);
    }

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

    private static MissionDto Mission(string uuid, string title, bool released = true) => new()
    {
        Uuid = uuid,
        Title = title,
        Released = released,
        HaulingSummary = [new HaulingSummaryItemDto { Name = "Gold", MinAmount = 1, MaxAmount = 1 }],
        ReputationGained = [new ReputationGainedDto { Faction = "Wikelo Emporium", Scope = "Wikelo", Amount = 250 }],
        GameVersion = "4.2.0",
    };

    [Fact]
    public async Task First_load_fetches_missions_and_filters_unreleased_placeholders()
    {
        var client = new FakeApiClient
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
        var client = new FakeApiClient
        {
            VersionResponder = () => throw new ApiRateLimitedException(TimeSpan.FromSeconds(30)),
        };
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
        var client = new FakeApiClient
        {
            VersionResponder = () => throw new ApiRateLimitedException(TimeSpan.FromSeconds(30)),
        };
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
        var client = new FakeApiClient { Missions = [Mission("a", "Real contract")] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        var enriched = new TaskCompletionSource();
        service.CatalogUpdated += (_, _) => enriched.TrySetResult();

        _ = await service.GetContractsAsync();
        // Let background enrichment finish before the next assertions touch the cache.
        await enriched.Task.WaitAsync(TimeSpan.FromSeconds(10));

        client.VersionResponder = () => throw new ApiRateLimitedException(null);
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
        var client = new FakeApiClient { Missions = [Mission("a", "Real contract")] };
        var service = new ContractCatalogService(client, _cacheDirectory);

        _ = await service.GetContractsAsync();
        _ = await service.GetContractsAsync(forceRefresh: true);

        Assert.Equal(2, client.VersionCalls);
        Assert.Equal(1, client.MissionsCalls);
    }
}
