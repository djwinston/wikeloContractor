using System.Windows.Input;
using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// The slot-to-digit mapping. Three places have to agree on it — the overlay badge, the settings hint
/// and the hotkey plan — so it lives in one place and is pinned down here.
/// </summary>
public class OverlaySlotsTests
{
    [Theory]
    [InlineData(1, Key.D1)]
    [InlineData(5, Key.D5)]
    [InlineData(9, Key.D9)]
    [InlineData(10, Key.D0)]
    public void Slots_map_onto_the_main_row_digits(int slot, Key expected) =>
        Assert.Equal(expected, OverlaySlots.KeyFor(slot));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void Out_of_range_slots_have_no_key(int slot) =>
        Assert.Equal(Key.None, OverlaySlots.KeyFor(slot));

    [Theory]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "0")]
    public void The_badge_shows_the_digit_that_drives_the_slot(int slot, string expected) =>
        Assert.Equal(expected, OverlaySlots.DigitLabel(slot));

    [Fact]
    public void Every_slot_in_range_has_a_distinct_key()
    {
        var keys = Enumerable.Range(1, OverlaySlots.MaxSlots).Select(OverlaySlots.KeyFor).ToList();

        Assert.DoesNotContain(Key.None, keys);
        Assert.Equal(OverlaySlots.MaxSlots, keys.Distinct().Count());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Slot_range_is_one_based(int slot, bool valid) =>
        Assert.Equal(valid, OverlaySlots.IsValidSlot(slot));
}
