using System.Globalization;
using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One card of the gathering plan as the page renders it: the item, how much of it is gathered
/// against what the starred contracts ask for, and its overlay pin.
/// <para>
/// The pin is here because this is where the decision is actually made: the player reads what they
/// still need and picks what to count in game from that same list. Sending them to the Inventory page
/// to find each name again is the manual step this removes.
/// </para>
/// <para>
/// <see cref="Name"/> identifies the row and never changes; the two numbers do, so they notify. The
/// owner reconciles rather than refills the list — see <see cref="FavoritesViewModel"/> — which is
/// what keeps a held overlay hotkey from discarding and re-creating every row (and every
/// <see cref="PinToggle"/> with it) thirty times a second.
/// </para>
/// </summary>
public sealed partial class GatheringRowViewModel : ObservableObject
{
    public GatheringRowViewModel(GatheringItem item, IPinnedItemsService pins)
    {
        Name = item.Name;
        Pin = new PinToggle(item.Name, pins);
        Update(item);
    }

    /// <summary>Last numbers rendered, so a plan that recomputed without moving this row costs nothing.</summary>
    private GatheringItem? _item;

    public string Name { get; }

    /// <summary>
    /// "12 / 36" — what the inventory holds against what the starred contracts ask for, and the only
    /// quantity the card states. The shortfall is the gap between the two, so spelling it out as
    /// well was one more number to read and the one that wanted a status colour.
    /// </summary>
    [ObservableProperty]
    private string _stockLabel = string.Empty;

    /// <summary>The same ratio in [0, 1], driving the card's coverage bar.</summary>
    [ObservableProperty]
    private double _progress;

    public PinToggle Pin { get; }

    /// <summary>
    /// Re-reads the numbers after the plan was recomputed for the same item.
    /// <para>
    /// The plan is rebuilt whole on every inventory change, but one edit moves one row — so an
    /// unchanged row returns before formatting anything. Record equality is the comparison:
    /// <see cref="GatheringItem"/> is the numbers, and both derived values follow from them.
    /// </para>
    /// </summary>
    public void Update(GatheringItem item)
    {
        if (item == _item)
        {
            return;
        }

        _item = item;
        StockLabel = string.Format(CultureInfo.InvariantCulture, "{0} / {1}", item.Have, item.Required);
        Progress = item.CoveredFraction;
    }
}
