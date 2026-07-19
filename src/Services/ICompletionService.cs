using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <summary>
/// Tracks which contracts the user has completed and the Wikelo reputation earned. Backed by
/// <c>%AppData%\WikeloContractor\completed.json</c>; state is keyed by contract UUID so it survives
/// the enrichment cycle that rebuilds contract records.
/// </summary>
public interface ICompletionService
{
    /// <summary>Loads the persisted store; call once at startup before the catalog is shown.</summary>
    Task LoadAsync();

    bool IsCompleted(string uuid);

    /// <summary>Marks a contract completed (storing its reputation) or clears it, then persists.</summary>
    Task SetCompletedAsync(WikeloContract contract, bool completed);

    /// <summary>Sum of reputation earned across all completed contracts.</summary>
    int TotalReputation { get; }

    /// <summary>Raised after the set of completed contracts changes.</summary>
    event EventHandler? Changed;
}
