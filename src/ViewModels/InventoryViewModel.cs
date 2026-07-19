using System.ComponentModel;
using System.Windows.Data;
using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The personal inventory: every distinct required item across the catalog, grouped by category,
/// each with a persisted count. Mirrors <see cref="CatalogViewModel"/>'s collection-view idiom
/// (a <see cref="ListCollectionView"/> with a filter, refreshed in place as the search text changes).
/// </summary>
public partial class InventoryViewModel : ViewModel
{
    /// <summary>
    /// Category combo order; index 0 in the UI is "All categories", 1.. index into this + 1. Kept in
    /// enum declaration order so the fixed ComboBoxItem list in InventoryPage.xaml lines up with it.
    /// </summary>
    private static readonly InventoryCategory[] _categoryOrder = Enum.GetValues<InventoryCategory>();

    private readonly IContractCatalogService _catalogService;
    private readonly IInventoryStore _store;

    /// <summary>Item wrappers (one per distinct requirement); the grouped view is built over these.</summary>
    private List<InventoryItemViewModel> _itemVms = [];

    /// <summary>Grouped, filtered view over <see cref="_items"/>; refreshed in place as the search changes.</summary>
    [ObservableProperty]
    private ICollectionView? _items;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>0 = all categories, 1.. = index into <see cref="_categoryOrder"/> + 1.</summary>
    [ObservableProperty]
    private int _categoryIndex;

    /// <summary>No items at all (before the catalog has loaded, or an empty catalog).</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    public InventoryViewModel(IContractCatalogService catalogService, IInventoryStore store)
    {
        _catalogService = catalogService;
        _store = store;

        // Both this VM and the service are app-lifetime singletons — the subscription needs no teardown.
        _catalogService.CatalogUpdated += OnCatalogUpdated;
    }

    public override async Task OnNavigatedToAsync()
    {
        // Cheap after the first call: served from memory unless a version check is due.
        try
        {
            var result = await _catalogService.GetContractsAsync();
            BuildItems(result.Contracts);
        }
        catch (Exception)
        {
            // No network and no cache — fall back to whatever is already loaded (possibly nothing).
            if (_catalogService.Current is { } current)
            {
                BuildItems(current.Contracts);
            }
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    partial void OnCategoryIndexChanged(int value) => RefreshFilter();

    private void RefreshFilter()
    {
        Items?.Refresh();
        UpdateIsEmpty();
    }

    private void OnCatalogUpdated(object? sender, EventArgs e) =>
        // Enrichment finishes on a background thread; requirements may gain entries (hauling orders).
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_catalogService.Current is { } result)
            {
                BuildItems(result.Contracts);
            }
        });

    private void BuildItems(IReadOnlyList<WikeloContract> contracts)
    {
        _itemVms = contracts
            .SelectMany(c => c.Requirements)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new InventoryItemViewModel(
                g.Key,
                InventoryCategoryClassifier.Classify(g.Key, g.Any(r => r.MinScu is not null || r.MaxScu is not null)),
                _store))
            .ToList();

        var view = new ListCollectionView(_itemVms) { Filter = FilterItem };
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InventoryItemViewModel.CategoryLabel)));
        Items = view;
        UpdateIsEmpty();
    }

    private bool FilterItem(object item)
    {
        if (item is not InventoryItemViewModel vm)
        {
            return false;
        }

        if (CategoryIndex > 0 && CategoryIndex <= _categoryOrder.Length
            && vm.Category != _categoryOrder[CategoryIndex - 1])
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SearchText)
            || vm.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateIsEmpty() => IsEmpty = Items is null || Items.IsEmpty;
}
