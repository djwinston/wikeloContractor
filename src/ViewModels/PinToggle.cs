using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One item's overlay-pin state and the button that changes it: pinned or not, which digit it
/// answers to, whether there is room left, and the already-resolved tooltip.
/// <para>
/// Its own type because two different rows now offer the same affordance — the inventory grid and the
/// Favorites page's gathering plan — and a second copy of these five members is exactly the drift
/// this repo treats as a review finding: the tooltip keys, the "full" rule and the digit label would
/// have to agree by hand forever.
/// </para>
/// <para>
/// Holds no list and no page: the owner rebuilds or refreshes it, and the cap itself lives in
/// <see cref="IPinnedItemsService"/>, so a full overlay refuses on its own no matter who asks.
/// </para>
/// </summary>
public sealed partial class PinToggle : ObservableObject
{
    private readonly IPinnedItemsService _pins;

    public PinToggle(string name, IPinnedItemsService pins)
    {
        Name = name;
        _pins = pins;
        Refresh();
    }

    public string Name { get; }

    [ObservableProperty]
    private bool _isPinned;

    /// <summary>The overlay digit this item answers to, or empty when it is not pinned.</summary>
    [ObservableProperty]
    private string _slotLabel = string.Empty;

    /// <summary>False only when the overlay is full and this item is not one of the ten.</summary>
    [ObservableProperty]
    private bool _canPin = true;

    /// <summary>Pin / unpin / "the overlay is full", as one already-resolved string.</summary>
    [ObservableProperty]
    private string _tooltip = string.Empty;

    /// <summary>Re-reads the state after the pinned set changed — this item's, or another's.</summary>
    public void Refresh()
    {
        var slot = _pins.SlotOf(Name);

        IsPinned = slot > 0;
        SlotLabel = IsPinned ? OverlaySlots.DigitLabel(slot) : string.Empty;
        CanPin = IsPinned || _pins.HasRoom;

        Tooltip = Localized.String(
            IsPinned ? "Inventory_Unpin" : CanPin ? "Inventory_Pin" : "Inventory_PinFull") ?? string.Empty;

        ToggleCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Pins or unpins the item. The cap lives in the service, so a full overlay simply refuses —
    /// <see cref="CanPin"/> only has to keep the button from inviting the click.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPin))]
    private async Task ToggleAsync()
    {
        if (IsPinned)
        {
            await _pins.UnpinAsync(Name);
            return;
        }

        _ = await _pins.PinAsync(Name);
    }
}
