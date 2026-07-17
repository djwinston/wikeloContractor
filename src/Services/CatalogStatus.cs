namespace WikeloContractor.Services;

/// <summary>
/// Mutually-exclusive freshness of the catalog data currently being served.
/// The service decides this once so every page maps the same state to UI and
/// the offline / rate-limited conditions can never be reported together.
/// </summary>
public enum CatalogStatus
{
    /// <summary>Data is current for the live game version.</summary>
    Online,

    /// <summary>API unreachable — showing previously cached contracts.</summary>
    Offline,

    /// <summary>API rejected the request with HTTP 429 — showing cache until the window elapses.</summary>
    RateLimited,
}
