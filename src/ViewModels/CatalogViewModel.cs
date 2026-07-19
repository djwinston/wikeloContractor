using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WikeloContractor.Models;
using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

public partial class CatalogViewModel : ViewModel
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

    private readonly IContractCatalogService _catalogService;
    private readonly ICompletionService _completionService;

    /// <summary>Card wrappers (one per contract); the collection view is built and filtered over these.</summary>
    private List<ContractCardViewModel> _cards = [];

    /// <summary>Suppresses the filter re-run while <see cref="SetContracts"/> restores the resource selection.</summary>
    private bool _suppressFilter;

    /// <summary>Filtered view over <see cref="_cards"/>; refreshed in place as filters change.</summary>
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

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Load failed and there is no cached data to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSynced))]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    private bool _hasLoadError;

    /// <summary>Freshness of the shown data, decided once by the service.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSynced))]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    private CatalogStatus _status;

    /// <summary>Data is current for the live game version (drives the green sync badge).</summary>
    public bool IsSynced => !HasLoadError && Status == CatalogStatus.Online;

    /// <summary>API is unreachable, showing cached data (drives the offline InfoBar).</summary>
    public bool IsOffline => !HasLoadError && Status == CatalogStatus.Offline;

    /// <summary>All filters combined produced no results.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _gameVersion;

    /// <summary>Wikelo standing shown as a progress bar above the contract list.</summary>
    [ObservableProperty]
    private ReputationSummary? _reputation;

    /// <summary>Shared rate-limit countdown, bound by both the Catalog and Settings pages.</summary>
    public RateLimitWatcher RateLimit { get; }

    private readonly INavigationService _navigationService;
    private readonly ContractDetailViewModel _detailViewModel;

    public CatalogViewModel(
        IContractCatalogService catalogService,
        ICompletionService completionService,
        RateLimitWatcher rateLimit,
        INavigationService navigationService,
        ContractDetailViewModel detailViewModel)
    {
        _catalogService = catalogService;
        _completionService = completionService;
        RateLimit = rateLimit;
        _navigationService = navigationService;
        _detailViewModel = detailViewModel;
        _catalogService.CatalogUpdated += OnCatalogUpdated;

        // Both this VM and the service are app-lifetime singletons — the subscription needs no teardown.
        _completionService.Changed += OnCompletionChanged;
        RecomputeReputation();
    }

    public override async Task OnNavigatedToAsync()
    {
        // Cheap after the first call: served from memory unless a version check is due.
        await LoadAsync();
    }

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

    private void OnCatalogUpdated(object? sender, EventArgs e) =>
        // Enrichment finishes on a background thread.
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_catalogService.Current is { } result)
            {
                SetContracts(result.Contracts);
            }
        });

    private void OnCompletionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var card in _cards)
            {
                card.RefreshCompleted();
            }

            RecomputeReputation();
        });

    private void RecomputeReputation() =>
        Reputation = ReputationSummary.From(ReputationLevels.Compute(_completionService.TotalReputation));

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasLoadError = false;

        try
        {
            var result = await _catalogService.GetContractsAsync();

            SetContracts(result.Contracts);
            GameVersion = result.GameVersion;
            Status = result.Status;
        }
        catch (Exception)
        {
            // No network and no cache — nothing to show.
            HasLoadError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetContracts(IReadOnlyList<WikeloContract> contracts)
    {
        _cards = contracts.Select(c => new ContractCardViewModel(c, _completionService)).ToList();

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
        RecomputeReputation();
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

    private bool FilterContract(object item)
    {
        if (item is not ContractCardViewModel { Contract: var contract })
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText)
            && !contract.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            && contract.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) != true
            && !contract.Rewards.Any(r => r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (CategoryIndex > 0 && CategoryIndex <= _categoryOrder.Length)
        {
            var selected = _categoryOrder[CategoryIndex - 1];

            // Any reward category matches (a ship contract with bonus armor shows under both).
            if (!contract.EffectiveCategories.Contains(selected))
            {
                return false;
            }
        }

        if (ResourceIndex > 0 && ResourceIndex < ResourceOptions.Count
            && !contract.Requirements.Any(r =>
                string.Equals(r.Name, ResourceOptions[ResourceIndex], StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private void UpdateIsEmpty() => IsEmpty = _cards.Count > 0 && (Contracts?.IsEmpty ?? false);
}
