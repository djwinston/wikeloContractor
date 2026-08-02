namespace WikeloContractor.Models;

/// <summary>
/// Persisted list of the required items the user pinned to the overlay, saved to
/// <c>%AppData%\WikeloContractor\pinned.json</c>. Keyed by item name — the same key
/// <see cref="InventoryStore"/> uses, because both address the catalog's required items rather than
/// contracts.
/// <para>
/// A list, not a set: the order <em>is</em> the slot assignment, and the slot number is what the
/// hotkey digit selects.
/// </para>
/// </summary>
public sealed class PinnedItemsStore
{
    /// <summary>Item names in slot order; the first entry is slot 1.</summary>
    public List<string> Pinned { get; set; } = [];
}
