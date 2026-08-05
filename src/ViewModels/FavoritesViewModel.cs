using System.Collections.ObjectModel;
using WikeloContractor.Models;
using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The catalog narrowed to the contracts the user starred. The cards, the filters and the detail
/// navigation all come from <see cref="ContractListViewModel"/>; what this adds is the narrower
/// source, its own "nothing starred yet" empty state, and the gathering plan.
/// </summary>
public partial class FavoritesViewModel : ContractListViewModel
{
    private readonly IPinnedItemsService _pins;

    public FavoritesViewModel(
        IContractCatalogService catalogService,
        ICompletionService completionService,
        IFavoritesService favoritesService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction,
        INavigationService navigationService,
        ContractDetailViewModel detailViewModel,
        IPinnedItemsService pins,
        OverlayPinsViewModel overlayPins)
        : base(catalogService, completionService, favoritesService, inventoryStore,
               completionInteraction, navigationService, detailViewModel)
    {
        _pins = pins;
        OverlayPins = overlayPins;

        // Pinning from the inventory grid must show up here too, and the tenth pin has to grey out
        // every remaining button. Refresh in place rather than rebuild: the plan itself did not move.
        _pins.Changed += (_, _) => UiThread.Invoke(() =>
        {
            foreach (var row in Gathering)
            {
                row.Pin.Refresh();
            }
        });
    }

    /// <summary>
    /// Nothing is starred at all — a different message from the catalog's "filters matched nothing"
    /// (<see cref="ContractListViewModel.IsEmpty"/>), which is why it is a separate flag and a
    /// separate localization key. The two are mutually exclusive by construction.
    /// </summary>
    [ObservableProperty]
    private bool _hasNoFavorites = true;

    /// <summary>Everything still missing across the starred contracts, each with its overlay pin.</summary>
    public ObservableCollection<GatheringRowViewModel> Gathering { get; } = [];

    /// <summary>
    /// The shared "Overlay 3/10" counter and its reset — the same object the inventory grid shows,
    /// because there is one set of pins and a second counter would only be a second thing to keep
    /// in step.
    /// </summary>
    public OverlayPinsViewModel OverlayPins { get; }

    /// <summary>
    /// There are starred contracts left to do and the inventory already covers all of them. A state
    /// worth stating: an empty gathering list otherwise looks like a feature that failed to load.
    /// </summary>
    [ObservableProperty]
    private bool _hasNothingToGather;

    /// <summary>There is a shortfall to show — so there is something to pin, and a budget to show.</summary>
    [ObservableProperty]
    private bool _hasOutstanding;

    /// <summary>
    /// There is a starred contract still open, so the gathering tab has something to say — a
    /// shortfall, or the "you have it all" line — and the explanation of how the numbers were
    /// reached is worth showing. A positive flag rather than an inverted binding on
    /// <see cref="HasNoFavorites"/>: those two answer different questions, and they part ways the
    /// moment every starred contract is completed.
    /// </summary>
    [ObservableProperty]
    private bool _hasGatheringPlan;

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

    protected override void OnContractsSet()
    {
        HasNoFavorites = Cards.Count == 0;
        RebuildGatheringPlan();
    }

    /// <summary>Completing a contract removes it from the plan and takes its items out of stock.</summary>
    protected override void OnCompletionChangedCore() => RebuildGatheringPlan();

    /// <summary>Every counter edit moves the shortfall — that is the number the player is watching.</summary>
    protected override void OnInventoryChangedCore() => RebuildGatheringPlan();

    /// <summary>Enrichment replaces the requirement lists the plan is summed from.</summary>
    protected override void OnSyncStateChangedCore() => RebuildGatheringPlan();

    /// <summary>
    /// Recomputes the combined shortfall.
    /// <para>
    /// <b>Completed contracts are excluded</b>, and that is the whole correctness of the feature:
    /// completing already deducted their items from the inventory, so counting them again would send
    /// the player back out for things they have handed over.
    /// </para>
    /// <para>
    /// Deliberately independent of the page's filters. They are a way to find a row; the plan answers
    /// "what does my whole starred set still need", and a shopping list that changes because a search
    /// box has text in it is not one.
    /// </para>
    /// </summary>
    private void RebuildGatheringPlan()
    {
        var open = Cards.Where(card => !card.IsCompleted).ToList();
        var outstanding = GatheringPlan.Build(open.Select(card => card.Contract), InventoryStore.GetCount);

        SyncGathering(outstanding);

        HasOutstanding = outstanding.Count > 0;

        // The panel exists as long as there is an open starred contract; whether it shows chips or
        // the "you have it all" line is the other two flags. With nothing starred the page's own
        // empty state already speaks, and a second reassurance under it would just be noise.
        HasGatheringPlan = open.Count > 0;
        HasNothingToGather = HasGatheringPlan && !HasOutstanding;
    }

    /// <summary>
    /// Reconciles the displayed rows with a freshly computed plan, in place.
    /// <para>
    /// Clearing and refilling would be shorter, and wrong here: this runs off
    /// <see cref="IInventoryStore.Changed"/>, which a held overlay hotkey raises about thirty times a
    /// second. Each pass would discard every row and every <see cref="PinToggle"/> on it — a resource
    /// lookup apiece — and raise a collection Reset that re-materializes every chip, to move one
    /// number. Rows are keyed by name, so an edit touches the one row it actually changed.
    /// </para>
    /// <para>
    /// Both sequences are ordered by name, ordinal ignoring case (<see cref="GatheringPlan.Build"/>
    /// guarantees it), so one walk reconciles them.
    /// </para>
    /// </summary>
    private void SyncGathering(IReadOnlyList<GatheringItem> outstanding)
    {
        for (var i = 0; i < outstanding.Count; i++)
        {
            var item = outstanding[i];

            // Anything sorting before the next wanted item has dropped out of the plan.
            while (i < Gathering.Count &&
                   StringComparer.OrdinalIgnoreCase.Compare(Gathering[i].Name, item.Name) < 0)
            {
                Gathering.RemoveAt(i);
            }

            if (i < Gathering.Count &&
                StringComparer.OrdinalIgnoreCase.Equals(Gathering[i].Name, item.Name))
            {
                Gathering[i].Update(item);
            }
            else
            {
                Gathering.Insert(i, new GatheringRowViewModel(item, _pins));
            }
        }

        while (Gathering.Count > outstanding.Count)
        {
            Gathering.RemoveAt(Gathering.Count - 1);
        }
    }
}
