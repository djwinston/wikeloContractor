using WikeloContractor.Models;

namespace WikeloContractor.Services;

public interface IContractCatalogService
{
    /// <summary>Last loaded catalog state, or null before the first successful load.</summary>
    CatalogLoadResult? Current { get; }

    /// <summary>Raised (possibly on a background thread) when catalog data changes after background enrichment.</summary>
    event EventHandler? CatalogUpdated;

    /// <summary>
    /// Raised (possibly on a background thread) when the rate-limit window opens or shifts.
    /// Consumers read <see cref="RateLimitedUntil"/> for the authoritative deadline rather
    /// than tracking their own; no API call is made until it elapses, then loading resumes.
    /// </summary>
    event EventHandler? RateLimitChanged;

    /// <summary>
    /// When set, all API calls are blocked until this moment (Retry-After + a safety margin);
    /// null when no rate-limit window is currently open. This is the single source of truth
    /// both pages drive their countdown from.
    /// </summary>
    DateTimeOffset? RateLimitedUntil { get; }

    /// <summary>
    /// Returns Wikelo contracts. The disk cache is invalidated only when a new LIVE game
    /// version is published; the version itself is re-checked at most every 12 hours
    /// (or immediately with <paramref name="forceRefresh"/>). Falls back to cached data
    /// when the API is unreachable so the app stays usable offline.
    /// </summary>
    Task<CatalogLoadResult> GetContractsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public sealed class CatalogLoadResult
{
    public required IReadOnlyList<WikeloContract> Contracts { get; init; }

    /// <summary>LIVE game version the contracts belong to (e.g. "4.9.0-LIVE.12232306").</summary>
    public string? GameVersion { get; init; }

    /// <summary>When the data was originally fetched from the API.</summary>
    public DateTimeOffset FetchedAt { get; init; }

    /// <summary>Freshness of these contracts: online, offline (cache), or rate-limited (cache).</summary>
    public required CatalogStatus Status { get; init; }
}
