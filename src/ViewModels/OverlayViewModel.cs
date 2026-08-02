using System.Collections.ObjectModel;
using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// What the in-game HUD shows: the pinned items in slot order, their counts, and the two pieces of
/// chrome state (<see cref="IsShown"/>, <see cref="IsInteractive"/>).
/// <para>
/// Holds no reference to a <c>Window</c>. That is what lets the whole overlay — hotkey to store to
/// readiness chip — be exercised in the E2E tier by raising <c>IHotkeyService.Pressed</c>, with
/// <see cref="OverlayService"/> as the only piece that knows a window exists.
/// </para>
/// </summary>
public sealed partial class OverlayViewModel : ObservableObject
{
    private readonly IPinnedItemsService _pins;
    private readonly IInventoryStore _inventory;
    private readonly IContractCatalogService _catalog;

    public OverlayViewModel(IPinnedItemsService pins, IInventoryStore inventory, IContractCatalogService catalog)
    {
        _pins = pins;
        _inventory = inventory;
        _catalog = catalog;

        // All three are app-lifetime singletons, as is this VM — no teardown needed.
        _pins.Changed += (_, _) => UiThread.Invoke(Rebuild);
        _inventory.Changed += (_, _) => UiThread.Invoke(RefreshCounts);
        _catalog.CatalogUpdated += (_, _) => UiThread.Invoke(Rebuild);

        Rebuild();
    }

    /// <summary>The rows, in slot order. Never longer than <see cref="OverlaySlots.MaxSlots"/>.</summary>
    public ObservableCollection<OverlaySlotViewModel> Slots { get; } = [];

    /// <summary>Whether the HUD is currently on screen. Driven by <see cref="OverlayService"/>.</summary>
    [ObservableProperty]
    private bool _isShown;

    /// <summary>
    /// Interactive mode: the drag header and the +/− buttons appear and the window stops being
    /// click-through. Off is the normal in-game state.
    /// </summary>
    [ObservableProperty]
    private bool _isInteractive;

    /// <summary>Nothing pinned — the HUD shows a "pin items from the Inventory page" hint instead.</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>
    /// The row in a 1-based slot, or null when the slot is empty. Indexed rather than searched:
    /// <see cref="Rebuild"/> fills the slots contiguously from 1, and this runs on every hotkey press
    /// under key auto-repeat.
    /// </summary>
    public OverlaySlotViewModel? SlotAt(int slot) =>
        slot >= 1 && slot <= Slots.Count ? Slots[slot - 1] : null;

    /// <summary>Applies a hotkey's relative change to a slot; an empty slot is silently ignored.</summary>
    public void Adjust(int slot, int delta) => SlotAt(slot)?.Adjust(delta);

    private void Rebuild()
    {
        var categories = CategoryLookup();

        Slots.Clear();

        for (var slot = 1; slot <= _pins.Count; slot++)
        {
            if (_pins.ItemAt(slot) is not { } name)
            {
                continue;
            }

            var category = categories.TryGetValue(name, out var known)
                ? known
                // The catalog may not be loaded yet (offline first run); the name alone still
                // classifies most items, and a later CatalogUpdated rebuilds this.
                : InventoryCategoryClassifier.Classify(name, hasScu: false);

            Slots.Add(new OverlaySlotViewModel(slot, name, category, _inventory));
        }

        IsEmpty = Slots.Count == 0;
    }

    private void RefreshCounts()
    {
        foreach (var row in Slots)
        {
            row.Refresh();
        }
    }

    /// <summary>
    /// Item name → category, derived exactly as the inventory grid derives it, so an item's glyph is
    /// the same in both places.
    /// </summary>
    private Dictionary<string, InventoryCategory> CategoryLookup()
    {
        var lookup = new Dictionary<string, InventoryCategory>(StringComparer.OrdinalIgnoreCase);

        if (_catalog.Current is not { } result)
        {
            return lookup;
        }

        foreach (var group in result.Contracts
            .SelectMany(contract => contract.Requirements)
            .GroupBy(requirement => requirement.Name, StringComparer.OrdinalIgnoreCase))
        {
            lookup[group.Key] = InventoryCategoryClassifier.Classify(
                group.Key,
                group.Any(requirement => requirement.MinScu is not null || requirement.MaxScu is not null));
        }

        return lookup;
    }
}
