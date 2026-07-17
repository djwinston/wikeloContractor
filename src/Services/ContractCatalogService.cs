using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using WikeloContractor.Models;
using WikeloContractor.Models.Api;
using WikeloContractor.Services.Api;

namespace WikeloContractor.Services;

public sealed partial class ContractCatalogService : IContractCatalogService
{
    /// <summary>How often the current game version is re-checked against the API.</summary>
    private static readonly TimeSpan _versionCheckInterval = TimeSpan.FromHours(12);

    /// <summary>Delay between per-record API calls during enrichment (politeness).</summary>
    private static readonly TimeSpan _enrichmentDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>Fallback wait when a 429 response carries no Retry-After header.</summary>
    private static readonly TimeSpan _defaultRetryAfter = TimeSpan.FromSeconds(60);

    /// <summary>Most-significant first: the highest-priority category among a contract's rewards wins.</summary>
    private static readonly ContractCategory[] _categoryPriority =
    [
        ContractCategory.Ship,
        ContractCategory.GroundVehicle,
        ContractCategory.Paint,
        ContractCategory.Weapon,
        ContractCategory.Armor,
    ];

    /// <summary>Extra wait on top of Retry-After so the retry never lands a second too early.</summary>
    private static readonly TimeSpan _rateLimitSafetyMargin = TimeSpan.FromSeconds(5);

    private readonly IStarCitizenWikiClient _apiClient;
    private readonly string _cacheFilePath;

    private CacheEnvelope? _envelope;
    private int _enrichmentRunning;

    /// <summary>Until this moment all API calls are blocked (set after an HTTP 429).</summary>
    private DateTimeOffset _rateLimitedUntil;

    public ContractCatalogService(IStarCitizenWikiClient apiClient)
        : this(apiClient, AppStorage.GetDirectory("cache"))
    {
    }

    /// <summary>Test seam: lets unit tests redirect the cache to a temp directory.</summary>
    internal ContractCatalogService(IStarCitizenWikiClient apiClient, string cacheDirectory)
    {
        _apiClient = apiClient;

        _ = Directory.CreateDirectory(cacheDirectory);
        _cacheFilePath = Path.Combine(cacheDirectory, "contracts.json");
    }

    public CatalogLoadResult? Current { get; private set; }

    public DateTimeOffset? RateLimitedUntil =>
        _rateLimitedUntil > DateTimeOffset.UtcNow ? _rateLimitedUntil : null;

    /// <summary>Raised (on a background thread) when catalog data changes after enrichment.</summary>
    public event EventHandler? CatalogUpdated;

    /// <summary>Raised (on a background thread) when the rate-limit window opens after an HTTP 429.</summary>
    public event EventHandler? RateLimitChanged;

    public async Task<CatalogLoadResult> GetContractsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        _envelope ??= await TryReadCacheAsync(cancellationToken);

        var versionCheckDue = forceRefresh
            || _envelope is null
            || DateTimeOffset.UtcNow - _envelope.LastVersionCheckAt >= _versionCheckInterval;

        if (!versionCheckDue)
        {
            StartEnrichmentIfNeeded();
            return Publish(FromEnvelope(_envelope!, CatalogStatus.Online));
        }

        // Rate-limit window still open — no API calls until it elapses.
        if (DateTimeOffset.UtcNow < _rateLimitedUntil)
        {
            var remaining = _rateLimitedUntil - DateTimeOffset.UtcNow;
            RateLimitChanged?.Invoke(this, EventArgs.Empty);

            if (_envelope is not null)
            {
                return Publish(FromEnvelope(_envelope, CatalogStatus.RateLimited));
            }

            throw new ApiRateLimitedException(remaining);
        }

        try
        {
            var latestVersion = await _apiClient.GetCurrentGameVersionAsync(cancellationToken);

            if (_envelope is null || _envelope.GameVersion != latestVersion)
            {
                // New game version (or first start) — contracts may have changed, refetch.
                var missions = await _apiClient.GetWikeloMissionsAsync(cancellationToken);

                // Unreleased entries are placeholders from game files (e.g. "<= UNINITIALIZED =>").
                var contracts = missions
                    .Where(m => m.Released)
                    .Select(MapContract)
                    .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _envelope = new CacheEnvelope
                {
                    GameVersion = latestVersion,
                    FetchedAt = DateTimeOffset.UtcNow,
                    LastVersionCheckAt = DateTimeOffset.UtcNow,
                    Contracts = contracts,
                    Enriched = false,
                };
            }
            else
            {
                // Same version — cached contracts are still valid, just remember the check time.
                _envelope = _envelope with { LastVersionCheckAt = DateTimeOffset.UtcNow };
            }

            await WriteCacheAsync(_envelope, cancellationToken);

            var result = Publish(FromEnvelope(_envelope, CatalogStatus.Online));
            StartEnrichmentIfNeeded();
            return result;
        }
        catch (ApiRateLimitedException ex)
        {
            // Too many requests this minute — tell the user to wait; cache still works.
            _ = RegisterRateLimit(ex.RetryAfter);

            if (_envelope is not null)
            {
                return Publish(FromEnvelope(_envelope, CatalogStatus.RateLimited));
            }

            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // API unreachable or returned garbage — serve cached data when available.
            if (_envelope is not null)
            {
                return Publish(FromEnvelope(_envelope, CatalogStatus.Offline));
            }

            throw;
        }
    }

    /// <summary>
    /// Loads mission details (rewards) and item classifications in the background,
    /// then updates the cache and raises <see cref="CatalogUpdated"/>.
    /// Runs once per game version; results are persisted in the cache envelope.
    /// </summary>
    private void StartEnrichmentIfNeeded()
    {
        if (_envelope is null || _envelope.Enriched)
        {
            return;
        }

        if (Interlocked.Exchange(ref _enrichmentRunning, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await EnrichAsync();
            }
            catch (Exception)
            {
                // Best effort: enrichment is retried on the next load if it did not finish.
            }
            finally
            {
                _ = Interlocked.Exchange(ref _enrichmentRunning, 0);
            }
        });
    }

    private async Task EnrichAsync()
    {
        var envelope = _envelope;
        if (envelope is null)
        {
            return;
        }

        // 1. Mission details → reward items.
        var rewardsByMission = new Dictionary<string, List<ContractReward>>();
        foreach (var contract in envelope.Contracts)
        {
            var detail = await WithRateLimitRetryAsync(() => _apiClient.GetMissionDetailAsync(contract.Uuid));
            rewardsByMission[contract.Uuid] = detail?.RewardItems
                .Select(r => new ContractReward { Name = r.Name, ItemUuid = r.Uuid, Amount = r.Amount ?? 1 })
                .ToList() ?? [];

            await Task.Delay(_enrichmentDelay);
        }

        // 2. Distinct reward items → classification signals.
        var classifications = new Dictionary<string, ItemClassification?>();
        var distinctItemUuids = rewardsByMission.Values
            .SelectMany(r => r)
            .Select(r => r.ItemUuid)
            .Where(uuid => !string.IsNullOrEmpty(uuid))
            .Distinct();

        foreach (var uuid in distinctItemUuids)
        {
            try
            {
                classifications[uuid!] = await WithRateLimitRetryAsync(() => _apiClient.GetItemClassificationAsync(uuid!));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                classifications[uuid!] = null;
            }

            await Task.Delay(_enrichmentDelay);
        }

        // 3. Rebuild contracts with rewards and derived category.
        var enriched = envelope.Contracts
            .Select(c =>
            {
                var rewards = rewardsByMission.GetValueOrDefault(c.Uuid) ?? [];
                return c with
                {
                    Rewards = rewards,
                    Category = DeriveCategory(rewards, classifications),
                };
            })
            .ToList();

        _envelope = envelope with { Contracts = enriched, Enriched = true };
        await WriteCacheAsync(_envelope, CancellationToken.None);

        Current = FromEnvelope(_envelope, CatalogStatus.Online);
        CatalogUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Runs an API call behind the rate-limit gate; on HTTP 429 notifies the UI, waits the
    /// window out once and retries. A second 429 propagates and aborts enrichment
    /// (retried on the next load).
    /// </summary>
    private async Task<T> WithRateLimitRetryAsync<T>(Func<Task<T>> apiCall)
    {
        await WaitForRateLimitWindowAsync();

        try
        {
            return await apiCall();
        }
        catch (ApiRateLimitedException ex)
        {
            await Task.Delay(RegisterRateLimit(ex.RetryAfter));
            return await apiCall();
        }
    }

    /// <summary>Blocks all API calls for Retry-After + a safety margin and notifies the UI.</summary>
    private TimeSpan RegisterRateLimit(TimeSpan? retryAfter)
    {
        var wait = (retryAfter ?? _defaultRetryAfter) + _rateLimitSafetyMargin;
        _rateLimitedUntil = DateTimeOffset.UtcNow + wait;
        RateLimitChanged?.Invoke(this, EventArgs.Empty);
        return wait;
    }

    /// <summary>Waits until the rate-limit window (if any) has elapsed.</summary>
    private async Task WaitForRateLimitWindowAsync()
    {
        var remaining = _rateLimitedUntil - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }

    private static ContractCategory DeriveCategory(
        IReadOnlyList<ContractReward> rewards,
        IReadOnlyDictionary<string, ItemClassification?> classifications)
    {
        if (rewards.Count == 0)
        {
            return ContractCategory.Other;
        }

        var categories = rewards
            .Select(r => ClassifyReward(r, r.ItemUuid is null ? null : classifications.GetValueOrDefault(r.ItemUuid)))
            .ToList();

        // The most "significant" reward defines the contract category.
        return _categoryPriority.FirstOrDefault(categories.Contains, ContractCategory.Other);
    }

    private static ContractCategory ClassifyReward(ContractReward reward, ItemClassification? classification)
    {
        // Paint/color variants are more meaningful to the player than the underlying vehicle record.
        if (PaintNameRegex().IsMatch(reward.Name))
        {
            return ContractCategory.Paint;
        }

        if (classification is null)
        {
            return ContractCategory.Other;
        }

        if (classification.IsSpaceship)
        {
            return ContractCategory.Ship;
        }

        if (classification.IsVehicleRecord)
        {
            return ContractCategory.GroundVehicle;
        }

        return classification.TypeString switch
        {
            string t when t.StartsWith("Char_Armor", StringComparison.OrdinalIgnoreCase) => ContractCategory.Armor,
            string t when t.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase) => ContractCategory.Weapon,
            _ => ContractCategory.Other,
        };
    }

    [GeneratedRegex(@"\b(color|paint|livery)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PaintNameRegex();

    private CatalogLoadResult Publish(CatalogLoadResult result)
    {
        Current = result;
        return result;
    }

    private static CatalogLoadResult FromEnvelope(CacheEnvelope envelope, CatalogStatus status) => new()
    {
        Contracts = envelope.Contracts,
        GameVersion = envelope.GameVersion,
        FetchedAt = envelope.FetchedAt,
        Status = status,
    };

    private static WikeloContract MapContract(MissionDto mission) => new()
    {
        Uuid = mission.Uuid,
        Title = mission.Title,
        Description = mission.Description,
        OnceOnly = mission.OnceOnly,
        HasPrerequisites = mission.HasPrerequisites,
        Requirements = mission.HaulingSummary
            .Select(h => new ContractRequirement { Name = h.Name, MinAmount = h.MinAmount, MaxAmount = h.MaxAmount })
            .ToList(),
        ReputationAmount = mission.ReputationGained.FirstOrDefault(r => r.Scope == "Wikelo")?.Amount
            ?? mission.ReputationAmount
            ?? 0,
        GameVersion = mission.GameVersion,
        WebUrl = mission.WebUrl,
    };

    private async Task<CacheEnvelope?> TryReadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_cacheFilePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_cacheFilePath);
            return await JsonSerializer.DeserializeAsync<CacheEnvelope>(stream, AppStorage.JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupted or locked cache — treat as absent; it will be rewritten on the next successful fetch.
            return null;
        }
    }

    private async Task WriteCacheAsync(CacheEnvelope envelope, CancellationToken cancellationToken)
    {
        // Atomic write: serialize to a temp file, then swap it in.
        var tempPath = _cacheFilePath + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, envelope, AppStorage.JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _cacheFilePath, overwrite: true);
    }

    private sealed record CacheEnvelope
    {
        public string? GameVersion { get; init; }

        public DateTimeOffset FetchedAt { get; init; }

        public DateTimeOffset LastVersionCheckAt { get; init; }

        /// <summary>True when rewards and categories have been loaded for this version.</summary>
        public bool Enriched { get; init; }

        public List<WikeloContract> Contracts { get; init; } = [];
    }
}
