using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
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

        // Built once and never replaced: SyncGathering reconciles the source in place, so the view
        // follows it through INotifyCollectionChanged and the page keeps one ItemsSource for the
        // lifetime of the app.
        GatheringView = new ListCollectionView(Gathering) { Filter = FilterGatheringRow };

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

    /// <summary>
    /// Everything the starred contracts ask for between them — gathered and not — each with its
    /// overlay pin. The page binds <see cref="GatheringView"/>; this is the unfiltered source, and
    /// what the tests read.
    /// </summary>
    public ObservableCollection<GatheringRowViewModel> Gathering { get; } = [];

    /// <summary>
    /// <see cref="Gathering"/> through the tab's own All / Gathered / Not gathered filter. Separate
    /// from the contract list's filters on purpose: those act on the Contracts tab, and the plan is
    /// still summed from every open starred contract regardless of what is selected here.
    /// </summary>
    public ICollectionView GatheringView { get; }

    /// <summary>
    /// 0 = every item, 1 = only what is fully gathered, 2 = only what is still short. Same
    /// "index 0 means all" convention the contract filters use.
    /// </summary>
    [ObservableProperty]
    private int _gatheringFilterIndex;

    /// <summary>
    /// Items still short — the tab's badge. Not <see cref="Gathering"/>'s count since the plan
    /// started listing gathered items too: the badge answers "how much is left", and a number that
    /// never moves as the player fills their hold is not that answer.
    /// </summary>
    [ObservableProperty]
    private int _outstandingCount;

    /// <summary>
    /// There are rows, and this tab's filter excludes all of them — the "no match" state, distinct
    /// from having nothing starred and from having gathered everything.
    /// </summary>
    [ObservableProperty]
    private bool _isGatheringEmpty;

    /// <summary>
    /// The shared "Overlay 3/10" counter and its reset — the same object the inventory grid shows,
    /// because there is one set of pins and a second counter would only be a second thing to keep
    /// in step.
    /// </summary>
    public OverlayPinsViewModel OverlayPins { get; }

    /// <summary>
    /// There are starred contracts left to do and the inventory already covers all of them. Still
    /// worth stating now that the covered rows stay on screen: a grid of green cards is the same
    /// picture whether the plan is finished or the filter is hiding the rest.
    /// </summary>
    [ObservableProperty]
    private bool _hasNothingToGather;

    /// <summary>There is a shortfall left — what the tab's badge is shown for.</summary>
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
    /// Recomputes the combined plan.
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
        var plan = GatheringPlan.Build(open.Select(card => card.Contract), InventoryStore.GetCount);

        SyncGathering(plan);

        OutstandingCount = plan.Count(item => !item.IsCovered);
        HasOutstanding = OutstandingCount > 0;

        // The panel exists as long as there is an open starred contract; whether it shows cards or
        // the "you have it all" line is the other two flags. With nothing starred the page's own
        // empty state already speaks, and a second reassurance under it would just be noise.
        HasGatheringPlan = open.Count > 0;
        HasNothingToGather = HasGatheringPlan && !HasOutstanding;

        UpdateGatheringEmpty();
    }

    /// <summary>The tab's own filter, over the row's coverage state; index 0 lets everything through.</summary>
    private bool FilterGatheringRow(object item) =>
        item is GatheringRowViewModel row &&
        GatheringFilterIndex switch { 1 => row.IsCovered, 2 => !row.IsCovered, _ => true };

    partial void OnGatheringFilterIndexChanged(int value)
    {
        GatheringView.Refresh();
        UpdateGatheringEmpty();
    }

    private void UpdateGatheringEmpty() => IsGatheringEmpty = Gathering.Count > 0 && GatheringView.IsEmpty;

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
    private void SyncGathering(IReadOnlyList<GatheringItem> plan)
    {
        // Whether any row changed sides of the covered line. Insertions and removals reach the view
        // on their own through the collection; a row that merely flipped state does not, and with a
        // filter selected it would sit in the wrong half until something else refreshed. Refreshing
        // unconditionally is what this method exists to avoid — it raises a Reset, and one of the
        // callers is the ~30x/s held-hotkey path.
        var coverageMoved = false;

        for (var i = 0; i < plan.Count; i++)
        {
            var item = plan[i];

            // Anything sorting before the next wanted item has dropped out of the plan.
            while (i < Gathering.Count &&
                   StringComparer.OrdinalIgnoreCase.Compare(Gathering[i].Name, item.Name) < 0)
            {
                Gathering.RemoveAt(i);
            }

            if (i < Gathering.Count &&
                StringComparer.OrdinalIgnoreCase.Equals(Gathering[i].Name, item.Name))
            {
                coverageMoved |= Gathering[i].Update(item);
            }
            else
            {
                Gathering.Insert(i, new GatheringRowViewModel(item, _pins));
            }
        }

        while (Gathering.Count > plan.Count)
        {
            Gathering.RemoveAt(Gathering.Count - 1);
        }

        if (coverageMoved && GatheringFilterIndex != 0)
        {
            GatheringView.Refresh();
        }
    }
}
