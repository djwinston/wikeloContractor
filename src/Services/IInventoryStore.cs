namespace WikeloContractor.Services;

/// <summary>
/// The personal inventory counter store, backed by <c>%AppData%\WikeloContractor\inventory.json</c>.
/// State is keyed by required-item name (list-endpoint requirements carry no UUID), mirroring
/// <see cref="ICompletionService"/>.
/// </summary>
public interface IInventoryStore
{
    /// <summary>Loads the persisted store; call once at startup.</summary>
    Task LoadAsync();

    /// <summary>Current held count for an item (zero when absent).</summary>
    int GetCount(string name);

    /// <summary>Sets the held count (clamped to zero and up), then persists.</summary>
    Task SetCountAsync(string name, int count);

    /// <summary>
    /// Applies several counts at once (each clamped to zero and up), persisting and raising
    /// <see cref="Changed"/> a single time. Used when one action touches many items (e.g. deducting a
    /// completed contract's requirements) to avoid a write and a full readiness rebuild per item.
    /// </summary>
    Task SetCountsAsync(IReadOnlyDictionary<string, int> counts);

    /// <summary>Raised after any count changes.</summary>
    event EventHandler? Changed;
}
