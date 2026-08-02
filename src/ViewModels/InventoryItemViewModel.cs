using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One inventory row: a required item, its category, the count the player holds, and whether it is
/// pinned to the in-game overlay. The count is backed by <see cref="IInventoryStore"/> and keyed by
/// <see cref="Name"/>; every change to <see cref="Count"/> — a typed-in value or a spin step — is
/// persisted.
/// </summary>
public partial class InventoryItemViewModel : ObservableObject, IRequirementItem
{
    private readonly IInventoryStore _store;
    private readonly IPinnedItemsService _pins;

    /// <summary>True while <see cref="RefreshCount"/> is pushing the store's value into the property.</summary>
    private bool _suppressWrite;

    public InventoryItemViewModel(string name, InventoryCategory category, IInventoryStore store, IPinnedItemsService pins)
    {
        _store = store;
        _pins = pins;
        Name = name;
        Category = category;
        _count = store.GetCount(name);
        RefreshPin();
    }

    public string Name { get; }

    public InventoryCategory Category { get; }

    /// <summary>Localized category name; also the grouping key for the page's section headers.</summary>
    public string CategoryLabel => Localized.String(InventoryCategoryDisplay.LabelKey(Category)) ?? Name;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isPinned;

    /// <summary>The overlay digit this item answers to, or empty when it is not pinned.</summary>
    [ObservableProperty]
    private string _slotLabel = string.Empty;

    /// <summary>False only when the overlay is full and this item is not one of the ten.</summary>
    [ObservableProperty]
    private bool _canPin = true;

    /// <summary>Pin / unpin / "the overlay is full", as one already-resolved string for the tooltip.</summary>
    [ObservableProperty]
    private string _pinTooltip = string.Empty;

    /// <summary>
    /// Re-reads the count after the store changed elsewhere (the overlay's hotkeys, a contract
    /// deduction).
    /// <para>
    /// The equality check is what makes this cheap: an overlay hotkey raises one event that reaches
    /// all ~95 rows, and 94 of them leave here. The flag then makes termination a local invariant
    /// rather than something inferred from the store's own de-duplication.
    /// </para>
    /// </summary>
    public void RefreshCount()
    {
        var stored = _store.GetCount(Name);
        if (stored == Count)
        {
            return;
        }

        _suppressWrite = true;
        try
        {
            Count = stored;
        }
        finally
        {
            _suppressWrite = false;
        }
    }

    /// <summary>Re-reads the pin state after the pinned set changed (this row's, or another's).</summary>
    public void RefreshPin()
    {
        var slot = _pins.SlotOf(Name);

        IsPinned = slot > 0;
        SlotLabel = IsPinned ? OverlaySlots.DigitLabel(slot) : string.Empty;
        CanPin = IsPinned || _pins.HasRoom;

        PinTooltip = Localized.String(
            IsPinned ? "Inventory_Unpin" : CanPin ? "Inventory_Pin" : "Inventory_PinFull") ?? string.Empty;

        TogglePinCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Persist every count change, whether a direct edit (the NumberBox) or a spin step. The value
    /// can't go negative — the NumberBox has <c>Minimum="0"</c> and <see cref="RefreshCount"/> reads
    /// the store — and the store no-ops when nothing actually changed.
    /// </summary>
    partial void OnCountChanged(int value)
    {
        if (_suppressWrite)
        {
            return;
        }

        _ = _store.SetCountAsync(Name, value);
    }

    /// <summary>
    /// Pins or unpins this item. The cap lives in the service, so a full grid simply refuses — this
    /// only has to keep the button from inviting the click.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPin))]
    private async Task TogglePinAsync()
    {
        if (IsPinned)
        {
            await _pins.UnpinAsync(Name);
            return;
        }

        _ = await _pins.PinAsync(Name);
    }
}
