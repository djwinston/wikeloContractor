namespace WikeloContractor.Services;

/// <summary>Which stage of background enrichment is running.</summary>
public enum CatalogSyncPhase
{
    /// <summary>Nothing is running — the catalog data on show is complete.</summary>
    Idle,

    /// <summary>Fetching per-mission details (rewards, hauling orders, blueprints).</summary>
    Contracts,

    /// <summary>Classifying the reward items the mission details referenced.</summary>
    Rewards,
}

/// <summary>
/// Progress of background enrichment — a different axis from <see cref="CatalogStatus"/>.
/// <para>
/// <see cref="CatalogStatus"/> answers "how fresh is this data" (online / offline / rate-limited)
/// and is mutually exclusive by design. Completeness is orthogonal: the catalog can be perfectly
/// <see cref="CatalogStatus.Online"/> and still be mid-sync, showing contracts whose rewards,
/// categories and full requirement lists have not arrived yet. Until enrichment finishes those
/// contracts are unsafe to filter by category or to complete against, so the UI blocks on this.
/// </para>
/// <para>
/// <see cref="Total"/> is per-phase, not global: the reward count is unknown until the mission
/// details have all been read, so one global total would have to be invented. A phase plus its
/// own total is the honest shape.
/// </para>
/// </summary>
public sealed record CatalogSyncState(CatalogSyncPhase Phase, int Completed, int Total)
{
    /// <summary>No enrichment running; the shown catalog is complete.</summary>
    public static readonly CatalogSyncState Idle = new(CatalogSyncPhase.Idle, 0, 0);

    /// <summary>Enrichment is in flight — catalog data is incomplete.</summary>
    public bool IsSyncing => Phase != CatalogSyncPhase.Idle;

    /// <summary>Progress within the current phase, in [0, 1]; 0 when the total is not known yet.</summary>
    public double Fraction => Total > 0 ? (double)Completed / Total : 0;
}
