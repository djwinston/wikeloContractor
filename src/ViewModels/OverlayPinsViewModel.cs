using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The overlay's pin budget as a page shows it: "Overlay 3/10" and the button that clears it.
/// <para>
/// A singleton shared by every page offering pins — the inventory grid and the Favorites gathering
/// plan — rather than a copy per page. There is one set of pins, so there is one counter; two would
/// only be two things to keep in step.
/// </para>
/// </summary>
public sealed partial class OverlayPinsViewModel : ObservableObject
{
    private readonly IPinnedItemsService _pins;

    public OverlayPinsViewModel(IPinnedItemsService pins)
    {
        _pins = pins;

        // Both sides are app-lifetime singletons — no teardown. The service raises from whichever
        // thread did the work (an overlay hotkey runs on the UI thread, a load does not).
        _pins.Changed += (_, _) => UiThread.Invoke(Refresh);

        Refresh();
    }

    /// <summary>"Overlay 3/10" — how much of the budget is spent.</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _hasPins;

    private void Refresh()
    {
        Summary = Localized.Format("Inventory_PinCount", _pins.Count, OverlaySlots.MaxSlots);
        HasPins = _pins.Count > 0;
        ClearCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasPins))]
    private Task ClearAsync() => _pins.ClearAsync();
}
