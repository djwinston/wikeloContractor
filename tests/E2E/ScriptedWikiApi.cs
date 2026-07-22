using System.Net.Http;
using WikeloContractor.Models.Api;
using WikeloContractor.Services.Api;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The scriptable <see cref="IStarCitizenWikiClient"/> every test drives the app through —
/// the single home for faking the API, used by both the service-level tests and the E2E
/// scenarios. A second fake for this interface would be exactly the duplication the project
/// rules call a review finding.
/// <para>
/// Beyond returning canned data it can be scripted to reproduce the states that are hard to
/// hit by hand: a version bump mid-session, an enrichment held open so assertions can run
/// while the catalog is incomplete, HTTP 429 on the Nth call, and a dead API.
/// </para>
/// </summary>
public sealed class ScriptedWikiApi : IStarCitizenWikiClient
{
    private readonly object _gate = new();

    private TaskCompletionSource? _hold;
    private int _rateLimitOnCall = -1;
    private TimeSpan? _rateLimitRetryAfter;
    private int _rateLimitTimes;
    private bool _offline;

    public string Version { get; private set; } = "4.2.0-LIVE.111";

    public List<MissionDto> Missions { get; set; } = [];

    public Dictionary<string, MissionDetailDto?> MissionDetails { get; set; } = [];

    public Dictionary<string, ItemClassification?> Classifications { get; set; } = [];

    public Dictionary<string, VehicleWeaponInfo?> WeaponInfo { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int VersionCalls { get; private set; }

    public int MissionsCalls { get; private set; }

    public int DetailCalls { get; private set; }

    /// <summary>Every API call routed through the fake, in order — the counter the gate scripts against.</summary>
    public int TotalCalls { get; private set; }

    /// <summary>Publishes a new LIVE version, which is what invalidates the cache.</summary>
    public void BumpVersion(string version) => Version = version;

    /// <summary>
    /// Blocks every enrichment call — mission details, item classifications, gun lookups — until
    /// <see cref="ReleaseEnrichment"/>, so a test can assert on the app while enrichment is
    /// genuinely in flight instead of racing it. The version check and the mission list are
    /// deliberately exempt: they run before enrichment starts, and holding them would stop the
    /// catalog load from ever reaching it.
    /// </summary>
    public void HoldEnrichment()
    {
        lock (_gate)
        {
            _hold ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void ReleaseEnrichment()
    {
        TaskCompletionSource? hold;
        lock (_gate)
        {
            hold = _hold;
            _hold = null;
        }

        _ = hold?.TrySetResult();
    }

    /// <summary>
    /// Throws <see cref="ApiRateLimitedException"/> on the Nth call overall (1-based) and, when
    /// <paramref name="times"/> &gt; 1, on the calls right after it — that is how an enrichment
    /// abort is reproduced (the service retries a 429 once, a second one aborts the run).
    /// </summary>
    public void ThrowRateLimitedOnCall(int call, TimeSpan? retryAfter = null, int times = 1)
    {
        _rateLimitOnCall = call;
        _rateLimitRetryAfter = retryAfter;
        _rateLimitTimes = times;
    }

    /// <summary>Every call from now on is refused with HTTP 429.</summary>
    public void AlwaysRateLimited(TimeSpan? retryAfter = null) =>
        ThrowRateLimitedOnCall(TotalCalls + 1, retryAfter, times: int.MaxValue);

    /// <summary>Every call from now on fails as if the API were unreachable.</summary>
    public void GoOffline() => _offline = true;

    public void GoOnline() => _offline = false;

    public async Task<string> GetCurrentGameVersionAsync(CancellationToken cancellationToken = default)
    {
        VersionCalls++;
        await EnterAsync(honorHold: false);
        return Version;
    }

    public async Task<IReadOnlyList<MissionDto>> GetWikeloMissionsAsync(CancellationToken cancellationToken = default)
    {
        MissionsCalls++;
        await EnterAsync(honorHold: false);
        return Missions;
    }

    public async Task<MissionDetailDto?> GetMissionDetailAsync(string missionUuid, CancellationToken cancellationToken = default)
    {
        DetailCalls++;
        await EnterAsync();
        return MissionDetails.GetValueOrDefault(missionUuid);
    }

    public async Task<ItemClassification?> GetItemClassificationAsync(string itemUuid, CancellationToken cancellationToken = default)
    {
        await EnterAsync();
        return Classifications.GetValueOrDefault(itemUuid);
    }

    public async Task<VehicleWeaponInfo?> GetVehicleWeaponInfoAsync(string weaponName, CancellationToken cancellationToken = default)
    {
        await EnterAsync();
        return WeaponInfo.GetValueOrDefault(weaponName);
    }

    /// <summary>The scripted behaviour every call passes through: count, fail, or wait on the gate.</summary>
    private async Task EnterAsync(bool honorHold = true)
    {
        Task? hold;
        int call;

        lock (_gate)
        {
            call = ++TotalCalls;
            hold = honorHold ? _hold?.Task : null;
        }

        if (_offline)
        {
            throw new HttpRequestException("Scripted: API unreachable.");
        }

        // long arithmetic: `times: int.MaxValue` ("from now on") would otherwise overflow the
        // upper bound to a negative number and silently never throw.
        if (_rateLimitOnCall > 0 && call >= _rateLimitOnCall && call < (long)_rateLimitOnCall + _rateLimitTimes)
        {
            throw new ApiRateLimitedException(_rateLimitRetryAfter);
        }

        if (hold is not null)
        {
            await hold;
        }
    }

    /// <summary>A minimal released mission with one summary requirement and Wikelo reputation.</summary>
    public static MissionDto Mission(string uuid, string title, bool released = true, bool onceOnly = false) => new()
    {
        Uuid = uuid,
        Title = title,
        Released = released,
        OnceOnly = onceOnly,
        HaulingSummary = [new HaulingSummaryItemDto { Name = "Gold", MinAmount = 1, MaxAmount = 1 }],
        ReputationGained = [new ReputationGainedDto { Faction = "Wikelo Emporium", Scope = "Wikelo", Amount = 250 }],
    };
}
