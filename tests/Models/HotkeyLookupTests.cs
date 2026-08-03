using System.Windows.Input;
using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

public sealed class HotkeyLookupTests
{
    private static OverlaySettings Settings() => new()
    {
        IncrementPattern = "Ctrl+Alt",
        DecrementPattern = "Ctrl+Shift",
        ToggleOverlayKey = "Ctrl+Alt+O",
        ToggleInteractiveKey = "Ctrl+Alt+I",
    };

    private static HotkeyLookup Lookup(int pinnedCount = 4) =>
        HotkeyLookup.From(HotkeyPlan.Build(Settings(), pinnedCount));

    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    private const HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    [Fact]
    public void A_slot_combination_resolves_to_its_action_and_slot()
    {
        var match = Lookup().Match(CtrlAlt, Key.D3);

        Assert.Equal(HotkeyAction.Increment, match!.Action);
        Assert.Equal(3, match.Slot);
    }

    [Fact]
    public void The_two_patterns_stay_apart_on_the_same_digit()
    {
        var lookup = Lookup();

        Assert.Equal(HotkeyAction.Increment, lookup.Match(CtrlAlt, Key.D2)!.Action);
        Assert.Equal(HotkeyAction.Decrement, lookup.Match(CtrlShift, Key.D2)!.Action);
    }

    [Fact]
    public void A_toggle_resolves_with_no_slot()
    {
        var match = Lookup().Match(CtrlAlt, Key.I);

        Assert.Equal(HotkeyAction.ToggleInteractive, match!.Action);
        Assert.Equal(0, match.Slot);
    }

    [Fact]
    public void An_extra_modifier_does_not_match()
    {
        // Mirrors RegisterHotKey: Ctrl+Alt+Shift+3 is not Ctrl+Alt+3. Subset matching would make the
        // increment pattern fire on the decrement combination whenever one contains the other.
        Assert.Null(Lookup().Match(CtrlAlt | HotkeyModifiers.Shift, Key.D3));
    }

    [Fact]
    public void The_bare_key_without_modifiers_does_not_match()
    {
        Assert.Null(Lookup().Match(HotkeyModifiers.None, Key.D3));
    }

    [Fact]
    public void A_digit_beyond_the_pinned_count_is_unknown()
    {
        // Only the digits actually pinned are planned, so the fifth slot exists for nobody.
        Assert.Null(Lookup(pinnedCount: 4).Match(CtrlAlt, Key.D5));
    }

    [Fact]
    public void Slot_ten_is_the_zero_digit()
    {
        Assert.Equal(10, Lookup(pinnedCount: 10).Match(CtrlAlt, Key.D0)!.Slot);
    }

    [Fact]
    public void Trigger_keys_are_recognised_regardless_of_modifiers()
    {
        // The cheap early-out the Raw Input backend runs before it reads anything else.
        var lookup = Lookup(pinnedCount: 2);

        Assert.True(lookup.IsTrigger(Key.D1));
        Assert.True(lookup.IsTrigger(Key.O));
        Assert.False(lookup.IsTrigger(Key.D9));
        Assert.False(lookup.IsTrigger(Key.A));
    }

    [Fact]
    public void An_empty_lookup_matches_nothing()
    {
        Assert.False(HotkeyLookup.Empty.IsTrigger(Key.D1));
        Assert.Null(HotkeyLookup.Empty.Match(CtrlAlt, Key.D1));
    }

    [Fact]
    public void With_nothing_pinned_only_the_toggles_are_known()
    {
        var lookup = Lookup(pinnedCount: 0);

        Assert.False(lookup.IsTrigger(Key.D1));
        Assert.NotNull(lookup.Match(CtrlAlt, Key.O));
    }
}
