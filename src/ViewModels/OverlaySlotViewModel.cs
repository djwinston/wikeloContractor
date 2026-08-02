using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One overlay row: a pinned item, the digit that drives it, and the count the player holds.
/// <para>
/// <see cref="Count"/> is deliberately read-only from the outside and there is no write-back hook on
/// it — every change goes through <see cref="Adjust"/>. The inventory page's row VM has to solve the
/// "the store changed, don't write it back" re-entrancy problem; the overlay simply never creates it.
/// </para>
/// </summary>
public sealed partial class OverlaySlotViewModel : ObservableObject
{
    private readonly IInventoryStore _store;

    public OverlaySlotViewModel(int slot, string name, InventoryCategory category, IInventoryStore store)
    {
        _store = store;
        Slot = slot;
        Name = name;
        Category = category;
        _count = store.GetCount(name);
    }

    /// <summary>1-based slot number.</summary>
    public int Slot { get; }

    /// <summary>The digit shown on the badge and pressed with the modifier pattern — slot 10 is "0".</summary>
    public string DigitLabel => OverlaySlots.DigitLabel(Slot);

    public string Name { get; }

    public InventoryCategory Category { get; }

    /// <summary>Localized category name; the overlay row shows a category glyph, not artwork.</summary>
    public string CategoryLabel => Localized.String(InventoryCategoryDisplay.LabelKey(Category)) ?? Name;

    [ObservableProperty]
    private int _count;

    /// <summary>Re-reads the count after the store changed elsewhere (the inventory page, a deduction).</summary>
    public void Refresh() => Count = _store.GetCount(Name);

    /// <summary>
    /// Applies a relative change, clamped at zero. Relative rather than absolute on purpose: the
    /// inventory page may have moved the count since this row was built, and a hotkey means "one more
    /// than whatever is there now", not "the number I last saw plus one".
    /// </summary>
    public void Adjust(int delta)
    {
        var next = Math.Max(0, _store.GetCount(Name) + delta);
        _ = _store.SetCountAsync(Name, next);

        // The store raises Changed synchronously, so this is usually already done; setting it here
        // keeps the row correct even if nobody is listening.
        Count = next;
    }

    [RelayCommand]
    private void Increment() => Adjust(1);

    [RelayCommand]
    private void Decrement() => Adjust(-1);
}
