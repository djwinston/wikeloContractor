using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The personal inventory: every distinct required item across the catalog, grouped by category, each
/// with a persisted count and an overlay pin. The grid, filters and image preview come from
/// <see cref="RequirementListViewModel"/>; this adds the count store the row VM writes through, the
/// pin store, and the fan-out of both services' <c>Changed</c> events onto the rows.
/// </summary>
public sealed partial class InventoryViewModel : RequirementListViewModel
{
    private readonly IInventoryStore _store;
    private readonly IPinnedItemsService _pins;

    public InventoryViewModel(IContractCatalogService catalogService, IInventoryStore store, IPinnedItemsService pins)
        : base(catalogService)
    {
        _store = store;
        _pins = pins;

        // One subscription for the whole page, fanned onto the rows — the same shape
        // ContractListViewModel uses. Without this the overlay's hotkeys would move counts that the
        // inventory page then shows stale.
        _store.Changed += (_, _) => UiThread.Invoke(() => ForEachRow(row => row.RefreshCount()));
        _pins.Changed += (_, _) => UiThread.Invoke(() =>
        {
            ForEachRow(row => row.RefreshPin());
            UpdatePinSummary();
        });

        UpdatePinSummary();
    }

    /// <summary>"Overlay 3/10" beside the search box — the cap has to be visible before it bites.</summary>
    [ObservableProperty]
    private string _pinSummary = string.Empty;

    /// <summary>Whether there is anything to clear; drives the reset button.</summary>
    [ObservableProperty]
    private bool _hasPins;

    /// <summary>Empties the grid so a new session can be planned without ten separate unpins.</summary>
    [RelayCommand(CanExecute = nameof(HasPins))]
    private Task ClearPinsAsync() => _pins.ClearAsync();

    protected override IRequirementItem CreateItem(string name, InventoryCategory category) =>
        new InventoryItemViewModel(name, category, _store, _pins);

    protected override void OnItemsRebuilt()
    {
        // Fresh row objects read the stores in their constructor, so only the summary needs redoing.
        UpdatePinSummary();
    }

    /// <summary>
    /// Fans a service event onto every row. Indexed rather than <c>OfType</c>: one inventory edit
    /// reaches all ~95 rows and a held hotkey repeats that ~30 times a second, so the boxed enumerator
    /// and iterator LINQ allocates there are pure waste — <see cref="CreateItem"/> only ever produces
    /// this row type anyway.
    /// </summary>
    private void ForEachRow(Action<InventoryItemViewModel> apply)
    {
        for (var i = 0; i < ItemVms.Count; i++)
        {
            if (ItemVms[i] is InventoryItemViewModel row)
            {
                apply(row);
            }
        }
    }

    private void UpdatePinSummary()
    {
        PinSummary = Localized.Format("Inventory_PinCount", _pins.Count, OverlaySlots.MaxSlots);
        HasPins = _pins.Count > 0;
        ClearPinsCommand.NotifyCanExecuteChanged();
    }
}
