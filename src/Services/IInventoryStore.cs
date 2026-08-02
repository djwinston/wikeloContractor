namespace WikeloContractor.Services;

/// <summary>
/// The personal inventory counter store, backed by <c>%AppData%\WikeloContractor\inventory.json</c>.
/// State is keyed by required-item name (list-endpoint requirements carry no UUID), mirroring
/// <see cref="ICompletionService"/>.
/// <para>
/// Writes are <b>debounced</b>. The overlay's hotkeys drive this store under key auto-repeat, so a
/// setter returns as soon as the change is in memory and <see cref="Changed"/> has been raised; the
/// file catches up a moment later. Await <see cref="FlushAsync"/> when the file itself has to be
/// current — a completed <c>SetCountAsync</c> does not mean "on disk".
/// </para>
/// </summary>
public interface IInventoryStore
{
    /// <summary>Loads the persisted store; call once at startup.</summary>
    Task LoadAsync();

    /// <summary>Current held count for an item (zero when absent).</summary>
    int GetCount(string name);

    /// <summary>Sets the held count (clamped to zero and up); the write is debounced.</summary>
    Task SetCountAsync(string name, int count);

    /// <summary>
    /// Applies several counts at once (each clamped to zero and up), raising <see cref="Changed"/> a
    /// single time. Used when one action touches many items (e.g. deducting a completed contract's
    /// requirements) to avoid a full readiness rebuild per item.
    /// </summary>
    Task SetCountsAsync(IReadOnlyDictionary<string, int> counts);

    /// <summary>Writes any pending change immediately.</summary>
    Task FlushAsync();

    /// <summary>
    /// Blocking <see cref="FlushAsync"/>, for shutdown. <c>App.OnExit</c> is <c>async void</c>, so a
    /// continuation posted back to a closing dispatcher may never run — and what would be lost is
    /// the player's last in-game edits.
    /// </summary>
    void Flush();

    /// <summary>Raised after any count changes, before the change reaches disk.</summary>
    event EventHandler? Changed;
}
