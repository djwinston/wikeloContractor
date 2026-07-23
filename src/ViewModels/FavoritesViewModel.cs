using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The catalog narrowed to the contracts the user starred. Everything visible — the cards, the
/// filters, the detail navigation — comes from <see cref="ContractListViewModel"/>; the only thing
/// this adds is the narrower source and its own "nothing starred yet" empty state.
/// </summary>
public partial class FavoritesViewModel : ContractListViewModel
{
    public FavoritesViewModel(
        IContractCatalogService catalogService,
        ICompletionService completionService,
        IFavoritesService favoritesService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction,
        INavigationService navigationService,
        ContractDetailViewModel detailViewModel)
        : base(catalogService, completionService, favoritesService, inventoryStore,
               completionInteraction, navigationService, detailViewModel)
    {
    }

    /// <summary>
    /// Nothing is starred at all — a different message from the catalog's "filters matched nothing"
    /// (<see cref="ContractListViewModel.IsEmpty"/>), which is why it is a separate flag and a
    /// separate localization key. The two are mutually exclusive by construction.
    /// </summary>
    [ObservableProperty]
    private bool _hasNoFavorites = true;

    public override void OnNavigatedTo() =>
        // This VM is created on the first navigation here, which can be long after the catalog
        // finished loading — so its CatalogUpdated never reached us. Pull the current list in.
        RebuildFromCatalog();

    /// <summary>Only the flagged contracts, in the catalog's own order.</summary>
    protected override void RebuildFromCatalog()
    {
        var favorites = CatalogService.Current?.Contracts
            .Where(c => FavoritesService.IsFavorite(c.Uuid))
            .ToList() ?? [];

        SetContracts(favorites);
    }

    /// <summary>Un-starring a contract here removes its row, so the list is rebuilt, not just refreshed.</summary>
    protected override void OnFavoritesChangedCore() => RebuildFromCatalog();

    protected override void OnContractsSet() => HasNoFavorites = Cards.Count == 0;
}
