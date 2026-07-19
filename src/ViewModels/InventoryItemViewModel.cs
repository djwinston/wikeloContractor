using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One inventory row: a required item, its category, and the count the player holds. The count is
/// backed by <see cref="IInventoryStore"/> and keyed by <see cref="Name"/>; the +/- commands persist it.
/// </summary>
public partial class InventoryItemViewModel : ObservableObject
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

    [RelayCommand]
    private Task Increment() => SetCountAsync(Count + 1);

    [RelayCommand]
    private Task Decrement() => SetCountAsync(Count - 1);

    private Task SetCountAsync(int value)
    {
        Count = Math.Max(0, value);
        return _store.SetCountAsync(Name, Count);
    }
}
