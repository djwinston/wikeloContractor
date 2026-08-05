using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One inventory row: a required item, its category, the count the player holds, and whether it is
/// pinned to the in-game overlay. The count is backed by <see cref="IInventoryStore"/> and keyed by
/// <see cref="Name"/>; every change to <see cref="Count"/> — a typed-in value or a spin step — is
/// persisted.
/// </summary>
public partial class InventoryItemViewModel : ObservableObject, IRequirementItem
{
    private readonly IInventoryStore _store;

    /// <summary>True while <see cref="RefreshCount"/> is pushing the store's value into the property.</summary>
    private bool _suppressWrite;

    public InventoryItemViewModel(string name, InventoryCategory category, IInventoryStore store, IPinnedItemsService pins)
    {
        _store = store;
        Name = name;
        Category = category;
        _count = store.GetCount(name);
        Pin = new PinToggle(name, pins);
    }

    public string Name { get; }

    /// <summary>The overlay-pin affordance, shared with the Favorites page's gathering plan.</summary>
    public PinToggle Pin { get; }

    public InventoryCategory Category { get; }

    /// <summary>Localized category name; also the grouping key for the page's section headers.</summary>
    public string CategoryLabel => Localized.String(InventoryCategoryDisplay.LabelKey(Category)) ?? Name;

    [ObservableProperty]
    private int _count;

    /// <summary>
    /// Re-reads the count after the store changed elsewhere (the overlay's hotkeys, a contract
    /// deduction).
    /// <para>
    /// The equality check is what makes this cheap: an overlay hotkey raises one event that reaches
    /// all ~95 rows, and 94 of them leave here. The flag then makes termination a local invariant
    /// rather than something inferred from the store's own de-duplication.
    /// </para>
    /// </summary>
    public void RefreshCount()
    {
        var stored = _store.GetCount(Name);
        if (stored == Count)
        {
            return;
        }

        _suppressWrite = true;
        try
        {
            Count = stored;
        }
        finally
        {
            _suppressWrite = false;
        }
    }

    /// <summary>
    /// Persist every count change, whether a direct edit (the NumberBox) or a spin step. The value
    /// can't go negative — the NumberBox has <c>Minimum="0"</c> and <see cref="RefreshCount"/> reads
    /// the store — and the store no-ops when nothing actually changed.
    /// </summary>
    partial void OnCountChanged(int value)
    {
        if (_suppressWrite)
        {
            return;
        }

        _ = _store.SetCountAsync(Name, value);
    }
}
