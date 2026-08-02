namespace WikeloContractor.Services;

/// <summary>
/// Tracks which required items the user pinned to the in-game overlay, in slot order. Backed by
/// <c>%AppData%\WikeloContractor\pinned.json</c>.
/// <para>
/// The <see cref="Models.OverlaySlots.MaxSlots"/> cap is enforced here, not in the view model: the
/// hotkey plan, the overlay and the inventory page all depend on it holding, and a rule enforced in a
/// service is testable without a window.
/// </para>
/// </summary>
public interface IPinnedItemsService
{
    /// <summary>Loads the persisted list; call once at startup, beside the other stores.</summary>
    Task LoadAsync();

    /// <summary>Pinned item names in slot order — index 0 is slot 1.</summary>
    IReadOnlyList<string> Pinned { get; }

    /// <summary>How many slots are taken.</summary>
    int Count { get; }

    /// <summary>Whether another item still fits.</summary>
    bool HasRoom { get; }

    bool IsPinned(string name);

    /// <summary>The 1-based slot holding <paramref name="name"/>, or 0 when it is not pinned.</summary>
    int SlotOf(string name);

    /// <summary>The item in a 1-based slot, or null when the slot is empty or out of range.</summary>
    string? ItemAt(int slot);

    /// <summary>
    /// Appends an item to the first free slot. Returns false when the grid is full or the item is
    /// already pinned — the caller shows the refusal, this does not throw.
    /// </summary>
    Task<bool> PinAsync(string name);

    /// <summary>
    /// Removes an item and compacts the slots below it, so pinning stays contiguous. A no-op when the
    /// item was not pinned.
    /// </summary>
    Task UnpinAsync(string name);

    /// <summary>
    /// Empties the grid. Unpinning ten items one at a time to re-plan a session is tedious enough
    /// that people stop re-planning; this is the way back to a blank slate.
    /// </summary>
    Task ClearAsync();

    /// <summary>Raised after the pinned list changes.</summary>
    event EventHandler? Changed;
}
