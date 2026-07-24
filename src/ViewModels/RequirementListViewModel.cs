using System.ComponentModel;
using System.Windows.Data;
using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>One row of an item-grid page: the shape both the shared thumb template and the base filter need.</summary>
public interface IRequirementItem
{
    string Name { get; }

    InventoryCategory Category { get; }

    /// <summary>Localized category name; also the grouping key for the page's section headers.</summary>
    string CategoryLabel { get; }
}

/// <summary>
/// The base for **any page showing the catalog's required items as a grouped grid**: the distinct-item
/// projection, the category-grouped <see cref="ICollectionView"/>, the search + category filters, the
/// empty state, and the full-window image-preview overlay. Mirrors <see cref="ContractListViewModel"/>
/// on the item-grid axis.
/// <para>
/// <see cref="InventoryViewModel"/> (per-item counters) and <see cref="SourcingViewModel"/> (per-item
/// sourcing notes) differ only in the row VM they project (<see cref="CreateItem"/>) and, for Sourcing,
/// the extra note clause in the search (<see cref="MatchesSearch"/>). A second copy of this machinery
/// is a review finding.
/// </para>
/// </summary>
public abstract partial class RequirementListViewModel : ViewModel
{
    /// <summary>
    /// Category combo order; index 0 in the UI is "All categories", 1.. index into this + 1. Kept in
    /// enum declaration order so the fixed ComboBoxItem lists in the pages line up with it.
    /// </summary>
    private static readonly InventoryCategory[] _categoryOrder = Enum.GetValues<InventoryCategory>();

    private List<IRequirementItem> _itemVms = [];

    protected RequirementListViewModel(IContractCatalogService catalogService)
    {
        CatalogService = catalogService;

        // Both this VM and the service are app-lifetime singletons — the subscription needs no teardown.
        CatalogService.CatalogUpdated += OnCatalogUpdated;
    }

    protected IContractCatalogService CatalogService { get; }

    /// <summary>Grouped, filtered view over the items; refreshed in place as the filters change.</summary>
    [ObservableProperty]
    private ICollectionView? _items;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>0 = all categories, 1.. = index into <see cref="_categoryOrder"/> + 1.</summary>
    [ObservableProperty]
    private int _categoryIndex;

    /// <summary>No items at all (before the catalog has loaded, or the filters matched nothing).</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Item name whose image fills the full-window preview overlay; null when closed.</summary>
    [ObservableProperty]
    private string? _previewItemName;

    /// <summary>Whether the full-window image preview overlay is showing.</summary>
    [ObservableProperty]
    private bool _isPreviewOpen;

    /// <summary>Projects one distinct required item into the page's concrete row VM.</summary>
    protected abstract IRequirementItem CreateItem(string name, InventoryCategory category);

    /// <summary>
    /// Whether an item matches the (non-blank) search text. Base matches the name; Sourcing widens it
    /// to the note. Category filtering is shared and not part of this.
    /// </summary>
    protected virtual bool MatchesSearch(IRequirementItem item, string search) =>
        item.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

    public override async Task OnNavigatedToAsync()
    {
        // Cheap after the first call: served from memory unless a version check is due.
        try
        {
            var result = await CatalogService.GetContractsAsync();
            BuildItems(result.Contracts);
        }
        catch (Exception)
        {
            // No network and no cache — fall back to whatever is already loaded (possibly nothing).
            if (CatalogService.Current is { } current)
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
            if (CatalogService.Current is { } result)
            {
                BuildItems(result.Contracts);
            }
        });

    private void BuildItems(IReadOnlyList<WikeloContract> contracts)
    {
        // A rebuild may drop the previewed item; close the overlay so it can't linger.
        IsPreviewOpen = false;

        _itemVms = contracts
            .SelectMany(c => c.Requirements)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => CreateItem(
                g.Key,
                InventoryCategoryClassifier.Classify(g.Key, g.Any(r => r.MinScu is not null || r.MaxScu is not null))))
            .ToList();

        var view = new ListCollectionView(_itemVms) { Filter = FilterItem };
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(IRequirementItem.CategoryLabel)));
        Items = view;
        UpdateIsEmpty();
    }

    private bool FilterItem(object item)
    {
        if (item is not IRequirementItem vm)
        {
            return false;
        }

        if (CategoryIndex > 0 && CategoryIndex <= _categoryOrder.Length
            && vm.Category != _categoryOrder[CategoryIndex - 1])
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SearchText) || MatchesSearch(vm, SearchText);
    }

    private void UpdateIsEmpty() => IsEmpty = Items is null || Items.IsEmpty;

    /// <summary>Opens the full-window preview for an item's image; imageless names simply no-op.</summary>
    [RelayCommand]
    private void OpenPreview(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        PreviewItemName = name;
        IsPreviewOpen = true;
    }

    [RelayCommand]
    private void ClosePreview() => IsPreviewOpen = false;
}
