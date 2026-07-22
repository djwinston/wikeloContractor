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
    /// <summary>
    /// Shape version of the cache envelope. Bump when enrichment output changes so older
    /// caches are discarded and rebuilt (v2: reward images; v3: reward details;
    /// v4: requirements from hauling_orders with SCU amounts; v5: category rules —
    /// once-only contracts and vehicle-variant base lookup; v6: missions filtered by
    /// mission_giver instead of reputation_scope; v7: ship loadout — weapons,
    /// missiles, components; v8: gun kind labels and component grades; v9: nested
    /// port scan — guns/ordnance with signal types, jump drives; v10: multi-category
    /// set per contract for the filter; v11: blueprint names granted on completion).
    /// </summary>
    private const int _cacheSchemaVersion = 11;

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
    private static readonly TimeSpan _defaultRateLimitSafetyMargin = TimeSpan.FromSeconds(5);

    private readonly IStarCitizenWikiClient _apiClient;
    private readonly string _cacheFilePath;

    /// <summary>Extra wait on top of Retry-After; overridable so tests do not pay the real 5 s.</summary>
    private readonly TimeSpan _rateLimitSafetyMargin;

    private CacheEnvelope? _envelope;
    private int _enrichmentRunning;

    /// <summary>Until this moment all API calls are blocked (set after an HTTP 429).</summary>
    private DateTimeOffset _rateLimitedUntil;

    public ContractCatalogService(IStarCitizenWikiClient apiClient)
        : this(apiClient, AppStorage.GetDirectory("cache"))
    {
    }

    /// <summary>
    /// Test seam: lets unit tests redirect the cache to a temp directory and shrink the
    /// rate-limit safety margin, which otherwise costs every retry test 5 s of wall clock.
    /// </summary>
    internal ContractCatalogService(IStarCitizenWikiClient apiClient, string cacheDirectory, TimeSpan? rateLimitSafetyMargin = null)
    {
        _apiClient = apiClient;
        _rateLimitSafetyMargin = rateLimitSafetyMargin ?? _defaultRateLimitSafetyMargin;

        _ = Directory.CreateDirectory(cacheDirectory);
        _cacheFilePath = Path.Combine(cacheDirectory, "contracts.json");
    }

    public CatalogLoadResult? Current { get; private set; }

    public DateTimeOffset? RateLimitedUntil =>
        _rateLimitedUntil > DateTimeOffset.UtcNow ? _rateLimitedUntil : null;

    public CatalogSyncState SyncState { get; private set; } = CatalogSyncState.Idle;

    /// <summary>Raised (on a background thread) when catalog data changes after enrichment.</summary>
    public event EventHandler? CatalogUpdated;

    /// <summary>Raised (on a background thread) when the rate-limit window opens after an HTTP 429.</summary>
    public event EventHandler? RateLimitChanged;

    /// <summary>Raised (on a background thread) when enrichment starts, advances or ends.</summary>
    public event EventHandler? SyncStateChanged;

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
                    SchemaVersion = _cacheSchemaVersion,
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

        // Published synchronously, before the work is queued, so a caller that has just awaited
        // GetContractsAsync already sees the catalog as incomplete rather than briefly complete.
        SetSyncState(new CatalogSyncState(CatalogSyncPhase.Contracts, 0, _envelope.Contracts.Count));

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

                // Must be in the finally: this run swallows its exceptions, and an aborted run
                // that left the state syncing would block the catalog until the app restarts.
                SetSyncState(CatalogSyncState.Idle);
            }
        });
    }

    private void SetSyncState(CatalogSyncState state)
    {
        SyncState = state;
        SyncStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task EnrichAsync()
    {
        var envelope = _envelope;
        if (envelope is null)
        {
            return;
        }

        // 1. Mission details → reward items + full requirements (hauling orders).
        var rewardsByMission = new Dictionary<string, List<ContractReward>>();
        var requirementsByMission = new Dictionary<string, List<ContractRequirement>>();
        var blueprintsByMission = new Dictionary<string, List<string>>();
        var detailsDone = 0;
        foreach (var contract in envelope.Contracts)
        {
            var detail = await WithRateLimitRetryAsync(() => _apiClient.GetMissionDetailAsync(contract.Uuid));
            rewardsByMission[contract.Uuid] = detail?.RewardItems
                .Select(r => new ContractReward { Name = r.Name, ItemUuid = r.Uuid, Amount = r.Amount ?? 1 })
                .ToList() ?? [];

            // Flatten every pool's items to distinct blueprint names (a pool can list several).
            // `blueprints` and its `items` arrive as JSON null when absent, so both need guarding.
            blueprintsByMission[contract.Uuid] = detail?.Blueprints?
                .SelectMany(p => p.Items ?? [])
                .Select(i => i.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            // hauling_orders are richer than the list's hauling_summary: SCU amounts plus
            // entries the summary omits (Wikelo Favor, vehicles to hand over).
            requirementsByMission[contract.Uuid] = detail?.HaulingOrders
                .Where(o => !string.IsNullOrWhiteSpace(o.Name))
                .Select(o => new ContractRequirement
                {
                    Name = o.Name,
                    MinAmount = o.MinAmount,
                    MaxAmount = o.MaxAmount,
                    MinScu = o.MinScu,
                    MaxScu = o.MaxScu,
                })
                .ToList() ?? [];

            SetSyncState(new CatalogSyncState(CatalogSyncPhase.Contracts, ++detailsDone, envelope.Contracts.Count));
            await Task.Delay(_enrichmentDelay);
        }

        // 2. Distinct reward items → classification signals.
        var distinctItemUuids = rewardsByMission.Values
            .SelectMany(r => r)
            .Select(r => r.ItemUuid)
            .Where(uuid => !string.IsNullOrEmpty(uuid))
            .Select(uuid => uuid!)
            .Distinct();

        var classifications = await LookupEachAsync<string, ItemClassification>(
            distinctItemUuids, uuid => _apiClient.GetItemClassificationAsync(uuid), reportAs: CatalogSyncPhase.Rewards);

        // 2.5. Fixed guns are listed by name only in vehicle records — look up each distinct
        // gun once for its kind label ("Laser Repeater"), size and grade, then patch the
        // classifications so every reward sharing the gun benefits.
        var gunNames = classifications.Values
            .Where(c => c?.Details?.Weapons is { Count: > 0 })
            .SelectMany(c => c!.Details!.Weapons!)
            .Where(w => w.Type is null && w.TypeLabel is null)
            .Select(w => w.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var gunInfo = await LookupEachAsync<string, VehicleWeaponInfo>(gunNames, name => _apiClient.GetVehicleWeaponInfoAsync(name), StringComparer.OrdinalIgnoreCase);

        // Only classifications with an unresolved gun name need patching.
        var uuidsWithUnresolvedGuns = classifications
            .Where(kv => kv.Value?.Details?.Weapons?.Any(w => w.Type is null && w.TypeLabel is null) == true)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var uuid in uuidsWithUnresolvedGuns)
        {
            var classification = classifications[uuid]!;
            classifications[uuid] = classification with
            {
                Details = classification.Details! with
                {
                    Weapons = classification.Details!.Weapons!.Select(w =>
                        w.Type is null && w.TypeLabel is null && gunInfo.GetValueOrDefault(w.Name) is { } info
                            ? w with { TypeLabel = info.TypeLabel, Size = w.Size ?? info.Size, Grade = info.Grade }
                            : w).ToList(),
                },
            };
        }

        // 3. Rebuild contracts with rewards (incl. item images and details) and derived category.
        var enriched = envelope.Contracts
            .Select(c =>
            {
                var rewards = (rewardsByMission.GetValueOrDefault(c.Uuid) ?? [])
                    .Select(r =>
                    {
                        var classification = r.ItemUuid is null ? null : classifications.GetValueOrDefault(r.ItemUuid);
                        return r with
                        {
                            // All known images are kept in the cache for later selection.
                            Images = classification?.Images ?? [],
                            Details = classification?.Details,
                        };
                    })
                    .ToList();

                var requirements = requirementsByMission.GetValueOrDefault(c.Uuid);

                var rewardCategories = ClassifyRewards(rewards, classifications);
                // One-time unlock contracts (e.g. "Arrive to System") gate the rest of the
                // catalog; their grab-bag reward mix must not drive the primary category.
                var primary = c.OnceOnly ? ContractCategory.Other : DeriveCategory(rewardCategories);

                return c with
                {
                    Rewards = rewards,
                    Category = primary,
                    // The filter matches any of these, so a ship contract with bonus armor
                    // and weapons is discoverable under all three categories.
                    Categories = rewardCategories.Append(primary).Distinct().ToList(),
                    // Keep the summary-based requirements when the detail had no orders.
                    Requirements = requirements is { Count: > 0 } ? requirements : c.Requirements,
                    Blueprints = blueprintsByMission.GetValueOrDefault(c.Uuid) ?? [],
                };
            })
            .ToList();

        _envelope = envelope with { Contracts = enriched, Enriched = true };
        await WriteCacheAsync(_envelope, CancellationToken.None);

        Current = FromEnvelope(_envelope, CatalogStatus.Online);

        // Clear the sync state *before* announcing the data: CatalogUpdated means "complete data
        // is available", so a subscriber reacting to it must not still see the catalog as
        // syncing. The finally in StartEnrichmentIfNeeded repeats this idempotently for the
        // abort path, where this line is never reached.
        SetSyncState(CatalogSyncState.Idle);
        CatalogUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Looks up one value per key through the rate-limit gate, politeness delay between
    /// calls; a failed lookup maps to null rather than aborting the rest of the batch.
    /// </summary>
    /// <param name="reportAs">
    /// When set, publishes sync progress under this phase. Only the reward classification pass
    /// reports: the gun lookups that follow it are derived from its results, so their count is
    /// unknown up front and they run as a short unreported tail.
    /// </param>
    private async Task<Dictionary<TKey, TValue?>> LookupEachAsync<TKey, TValue>(
        IEnumerable<TKey> keys,
        Func<TKey, Task<TValue?>> fetch,
        IEqualityComparer<TKey>? comparer = null,
        CatalogSyncPhase? reportAs = null)
        where TKey : notnull
    {
        var pending = keys as IReadOnlyList<TKey> ?? keys.ToList();
        var result = new Dictionary<TKey, TValue?>(comparer);
        var done = 0;

        if (reportAs is { } startPhase)
        {
            SetSyncState(new CatalogSyncState(startPhase, 0, pending.Count));
        }

        foreach (var key in pending)
        {
            try
            {
                result[key] = await WithRateLimitRetryAsync(() => fetch(key));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                result[key] = default;
            }

            if (reportAs is { } phase)
            {
                SetSyncState(new CatalogSyncState(phase, ++done, pending.Count));
            }

            await Task.Delay(_enrichmentDelay);
        }

        return result;
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

    /// <summary>Distinct categories across the rewards, in reward order.</summary>
    private static List<ContractCategory> ClassifyRewards(
        IReadOnlyList<ContractReward> rewards,
        IReadOnlyDictionary<string, ItemClassification?> classifications) =>
        rewards
            .Select(r => ClassifyReward(r, r.ItemUuid is null ? null : classifications.GetValueOrDefault(r.ItemUuid)))
            .Distinct()
            .ToList();

    private static ContractCategory DeriveCategory(IReadOnlyList<ContractCategory> categories) =>
        // The most "significant" reward defines the primary category.
        _categoryPriority.FirstOrDefault(categories.Contains, ContractCategory.Other);

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
        ReputationAmount = mission.ReputationGained?.FirstOrDefault(r => r.Scope == "Wikelo")?.Amount
            ?? mission.ReputationAmount
            ?? 0,
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
            var envelope = await JsonSerializer.DeserializeAsync<CacheEnvelope>(stream, AppStorage.JsonOptions, cancellationToken);

            // Older schema — discard so the catalog is refetched and re-enriched with the new shape.
            return envelope?.SchemaVersion == _cacheSchemaVersion ? envelope : null;
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
        /// <summary>See <see cref="_cacheSchemaVersion"/>; missing in pre-v2 caches (reads as 0).</summary>
        public int SchemaVersion { get; init; }

        public string? GameVersion { get; init; }

        public DateTimeOffset FetchedAt { get; init; }

        public DateTimeOffset LastVersionCheckAt { get; init; }

        /// <summary>True when rewards and categories have been loaded for this version.</summary>
        public bool Enriched { get; init; }

        public List<WikeloContract> Contracts { get; init; } = [];
    }
}
