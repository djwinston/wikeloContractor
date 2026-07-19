namespace WikeloContractor.Models;

/// <summary>Persisted personal inventory: required-item name → how many the player currently holds.</summary>
public sealed class InventoryStore
{
    /// <summary>Item name → count. Zero counts are omitted (a missing key means zero).</summary>
    public Dictionary<string, int> Counts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
