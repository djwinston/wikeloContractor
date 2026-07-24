using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WikeloContractor.Models;
using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// Everything a page showing a filterable list of contract cards needs: the cards themselves, the
/// search / category / resource filters over them, the empty state, and the navigation into the
/// detail page.
/// <para>
/// Both <see cref="CatalogViewModel"/> (every contract) and <see cref="FavoritesViewModel"/> (only
/// the flagged ones) derive from this — the pages differ solely in which contracts they feed in via
/// <see cref="RebuildFromCatalog"/>, never in how those contracts are filtered or rendered. A second
/// copy of <see cref="FilterContract"/> is a review finding; see PLAN.md Phase 2.5.
/// </para>
/// </summary>
public abstract partial class ContractListViewModel : ViewModel
{
    /// <summary>Category combo order; index 0 in the UI is "All categories".</summary>
    private static readonly ContractCategory[] _categoryOrder =
    [
        ContractCategory.Ship,
        ContractCategory.GroundVehicle,
        ContractCategory.Paint,
        ContractCategory.Weapon,
        ContractCategory.Armor,
        ContractCategory.Other,
    ];

    private readonly IInventoryStore _inventoryStore;
    private readonly ContractCompletionInteraction _completionInteraction;
    private readonly INavigationService _navigationService;
    private readonly ContractDetailViewModel _detailViewModel;

    /// <summary>Card wrappers (one per shown contract); the collection view is built and filtered over these.</summary>
    private List<ContractCardViewModel> _cards = [];

    /// <summary>Suppresses the filter re-run while <see cref="SetContracts"/> restores the resource selection.</summary>
    private bool _suppressFilter;

    /// <summary>Last observed sync state, so per-tick progress events collapse to the two real transitions.</summary>
    private bool _wasSyncing;

    protected ContractListViewModel(
        IContractCatalogService catalogService,
        ICompletionService completionService,
        IFavoritesService favoritesService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction,
        INavigationService navigationService,
        ContractDetailViewModel detailViewModel)
    {
        CatalogService = catalogService;
        CompletionService = completionService;
        FavoritesService = favoritesService;
        _inventoryStore = inventoryStore;
        _completionInteraction = completionInteraction;
        _navigationService = navigationService;
        _detailViewModel = detailViewModel;

        // Every subscriber here and every publisher is an app-lifetime singleton — no teardown.
        CatalogService.CatalogUpdated += OnCatalogUpdated;
        CatalogService.SyncStateChanged += OnSyncStateChanged;
        CompletionService.Changed += OnCompletionChanged;
        FavoritesService.Changed += OnFavoritesChanged;
        _inventoryStore.Changed += OnInventoryChanged;
    }

    protected IContractCatalogService CatalogService { get; }

    /// <summary>Exposed so <see cref="CatalogViewModel"/> can read the accumulated reputation.</summary>
    protected ICompletionService CompletionService { get; }

    /// <summary>Exposed so <see cref="FavoritesViewModel"/> can narrow the list to flagged contracts.</summary>
    protected IFavoritesService FavoritesService { get; }

    /// <summary>Filtered view over the cards; refreshed in place as filters change.</summary>
    [ObservableProperty]
    private ICollectionView? _contracts;

    /// <summary>Index 0 is always the localized "All resources" placeholder; 1.. are resource names.</summary>
    [ObservableProperty]
    private ObservableCollection<string> _resourceOptions = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>0 = all categories, 1.. = index into <see cref="_categoryOrder"/> + 1.</summary>
    [ObservableProperty]
    private int _categoryIndex;

    /// <summary>0 = all resources, 1.. = index into <see cref="ResourceOptions"/>.</summary>
    [ObservableProperty]
    private int _resourceIndex;

    /// <summary>All filters combined produced no results (there ARE cards, they are just filtered out).</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>The cards currently backing the view, for derived classes that fan events out to them.</summary>
    protected IReadOnlyList<ContractCardViewModel> Cards => _cards;

    /// <summary>Enrichment is running, so requirement lists are still the summary-based fallback.</summary>
    protected bool IsSyncingNow => CatalogService.SyncState.IsSyncing;

    /// <summary>
    /// Feeds the page's contracts in from <see cref="IContractCatalogService.Current"/>. Called on
    /// every catalog update; the catalog passes everything through, favorites narrows to the flagged set.
    /// </summary>
    protected abstract void RebuildFromCatalog();

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    partial void OnCategoryIndexChanged(int value) => RefreshFilter();

    partial void OnResourceIndexChanged(int value) => RefreshFilter();

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        CategoryIndex = 0;
        ResourceIndex = 0;
    }

    [RelayCommand]
    private void OpenDetails(WikeloContract? contract)
    {
        if (contract is null)
        {
            return;
        }

        _detailViewModel.Show(contract);
        // The detail page is not a nav menu item — navigate with back-stack support.
        _ = _navigationService.NavigateWithHierarchy(typeof(Views.Pages.ContractDetailPage));
    }

    /// <summary>Rebuilds the cards, the resource filter options and the collection view.</summary>
    protected void SetContracts(IReadOnlyList<WikeloContract> contracts)
    {
        _cards = contracts
            .Select(c => new ContractCardViewModel(
                c, CompletionService, FavoritesService, _inventoryStore, _completionInteraction, IsSyncingNow))
            .ToList();

        var resources = contracts
            .SelectMany(c => c.Requirements)
            .Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Preserve the selection by resource name, not by raw index, since the list can shrink/grow.
        var previouslySelected = ResourceIndex > 0 && ResourceIndex <= ResourceOptions.Count - 1
            ? ResourceOptions[ResourceIndex]
            : null;

        var allResourcesLabel = Localized.String("Catalog_Filter_AllResources") ?? "All resources";
        ResourceOptions = new ObservableCollection<string>([allResourcesLabel, .. resources]);

        var restoredIndex = previouslySelected is null
            ? -1
            : resources.FindIndex(r => string.Equals(r, previouslySelected, StringComparison.OrdinalIgnoreCase));

        // Restore the selection without triggering a filter pass — the fresh view below applies it.
        _suppressFilter = true;
        ResourceIndex = restoredIndex >= 0 ? restoredIndex + 1 : 0;
        _suppressFilter = false;

        Contracts = new ListCollectionView(_cards) { Filter = FilterContract };
        UpdateIsEmpty();
        OnContractsSet();
    }

    /// <summary>Re-evaluates the current view against the filters without reallocating the collection.</summary>
    private void RefreshFilter()
    {
        if (_suppressFilter)
        {
            return;
        }

        Contracts?.Refresh();
        UpdateIsEmpty();
    }

    /// <summary>
    /// The combo box selections translated into filter criteria. Index 0 means "all" on both
    /// combos, which maps to null — see <see cref="ContractFilter"/> for the matching itself.
    /// </summary>
    internal ContractFilter CurrentFilter => new(
        SearchText,
        CategoryIndex > 0 && CategoryIndex <= _categoryOrder.Length ? _categoryOrder[CategoryIndex - 1] : null,
        ResourceIndex > 0 && ResourceIndex < ResourceOptions.Count ? ResourceOptions[ResourceIndex] : null);

    private bool FilterContract(object item) =>
        item is ContractCardViewModel { Contract: var contract } && CurrentFilter.Matches(contract);

    private void UpdateIsEmpty() => IsEmpty = _cards.Count > 0 && (Contracts?.IsEmpty ?? false);

    /// <summary>Runs after every rebuild of the card list (the catalog recomputes reputation here).</summary>
    protected virtual void OnContractsSet() { }

    /// <summary>Runs after the completed set changed and the cards were refreshed.</summary>
    protected virtual void OnCompletionChangedCore() { }

    /// <summary>Runs after the favorite set changed and the cards were refreshed.</summary>
    protected virtual void OnFavoritesChangedCore() { }

    /// <summary>Runs when enrichment starts or finishes (the two real transitions only).</summary>
    protected virtual void OnSyncStateChangedCore() { }

    private void OnCatalogUpdated(object? sender, EventArgs e) =>
        // Enrichment finishes on a background thread.
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (CatalogService.Current is not null)
            {
                RebuildFromCatalog();
            }
        });

    private void OnSyncStateChanged(object? sender, EventArgs e) =>
        // Enrichment reports progress from a background thread, once per fetched detail — dozens of
        // ticks. Only the syncing *bool* gates anything on a list page, and it flips exactly twice,
        // so skip the intermediate ticks rather than re-raising and re-fanning them.
        Application.Current.Dispatcher.Invoke(() =>
        {
            var syncing = IsSyncingNow;
            if (syncing == _wasSyncing)
            {
                return;
            }

            _wasSyncing = syncing;
            OnSyncStateChangedCore();

            // Cards gate their completion toggle on this: completing against half-loaded
            // requirements deducts the wrong amounts from the inventory.
            foreach (var card in _cards)
            {
                card.SetSyncing(syncing);
            }
        });

    private void OnCompletionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var card in _cards)
            {
                card.RefreshCompleted();
            }

            OnCompletionChangedCore();
        });

    private void OnFavoritesChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var card in _cards)
            {
                card.RefreshFavorite();
            }

            OnFavoritesChangedCore();
        });

    private void OnInventoryChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var card in _cards)
            {
                card.RefreshReadiness();
            }
        });
}
