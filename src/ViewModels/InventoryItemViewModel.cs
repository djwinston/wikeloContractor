using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One inventory row: a required item, its category, and the count the player holds. The count is
/// backed by <see cref="IInventoryStore"/> and keyed by <see cref="Name"/>; every change to
/// <see cref="Count"/> — a typed-in value or a spin step — is persisted.
/// </summary>
public partial class InventoryItemViewModel : ObservableObject, IRequirementItem
{
    private readonly IInventoryStore _store;

    public InventoryItemViewModel(string name, InventoryCategory category, IInventoryStore store)
    {
        _store = store;
        Name = name;
        Category = category;
        _count = store.GetCount(name);
    }

    public string Name { get; }

    public InventoryCategory Category { get; }

    /// <summary>Localized category name; also the grouping key for the page's section headers.</summary>
    public string CategoryLabel => Localized.String(InventoryCategoryDisplay.LabelKey(Category)) ?? Name;

    [ObservableProperty]
    private int _count;

    /// <summary>Re-reads the count after the store changed elsewhere (e.g. a future overlay).</summary>
    public void RefreshCount() => Count = _store.GetCount(Name);

    /// <summary>
    /// Persist every count change, whether a direct edit (the NumberBox) or a spin step. The value
    /// can't go negative — the NumberBox has <c>Minimum="0"</c> and <see cref="RefreshCount"/> reads
    /// the store — and the store no-ops when nothing actually changed.
    /// </summary>
    partial void OnCountChanged(int value) => _ = _store.SetCountAsync(Name, value);
}
