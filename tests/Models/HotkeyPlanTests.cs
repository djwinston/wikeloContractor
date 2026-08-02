using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// What actually gets handed to <c>RegisterHotKey</c>. Every combination in the plan is taken from
/// the whole machine — Star Citizen included — so the set has to be exactly what was asked for and no
/// wider, and our own collisions must be caught here rather than surfacing as an opaque Win32 failure.
/// </summary>
public class HotkeyPlanTests
{
    private static OverlaySettings Defaults() => new();

    [Fact]
    public void With_nothing_pinned_only_the_two_toggles_are_registered()
    {
        var plan = HotkeyPlan.Build(Defaults(), pinnedCount: 0);

        Assert.Equal(2, plan.Registrations.Count);
        Assert.All(plan.Registrations, r => Assert.Equal(0, r.Slot));
        Assert.False(plan.HasConflicts);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(3, 8)]
    [InlineData(10, 22)]
    public void Only_the_pinned_digits_are_claimed(int pinned, int expected)
    {
        // Registering all twenty up front would steal twenty global combinations from the machine
        // even when two items are pinned.
        var plan = HotkeyPlan.Build(Defaults(), pinned);

        Assert.Equal(expected, plan.Registrations.Count);
    }

    [Fact]
    public void More_pinned_than_slots_is_clamped()
    {
        var plan = HotkeyPlan.Build(Defaults(), pinnedCount: 99);

        Assert.Equal(2 + (OverlaySlots.MaxSlots * 2), plan.Registrations.Count);
    }

    [Fact]
    public void Ids_and_combinations_are_unique_across_a_full_plan()
    {
        var plan = HotkeyPlan.Build(Defaults(), OverlaySlots.MaxSlots);

        Assert.Equal(plan.Registrations.Count, plan.Registrations.Select(r => r.Id).Distinct().Count());
        Assert.Equal(plan.Registrations.Count, plan.Registrations.Select(r => r.Binding).Distinct().Count());
    }

    [Fact]
    public void Both_patterns_set_the_same_reports_conflicts_instead_of_double_registering()
    {
        var settings = Defaults();
        settings.DecrementPattern = settings.IncrementPattern;

        var plan = HotkeyPlan.Build(settings, pinnedCount: 3);

        // Three increments survive, three decrements are dropped as duplicates.
        Assert.Equal(2 + 3, plan.Registrations.Count);
        Assert.Equal(3, plan.Conflicts.Count);
        Assert.All(plan.Conflicts, c => Assert.Equal(HotkeyAction.Decrement, c.Dropped.Action));
    }

    [Fact]
    public void A_toggle_colliding_with_a_slot_keeps_the_toggle()
    {
        // Losing a slot digit costs one item's hotkey; losing ToggleInteractive can leave a
        // click-through overlay unreachable, so the toggle has to win.
        var settings = Defaults();
        settings.ToggleInteractiveKey = "Ctrl+Alt+D2";

        var plan = HotkeyPlan.Build(settings, pinnedCount: 3);

        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal(HotkeyAction.ToggleInteractive, conflict.Kept.Action);
        Assert.Equal(HotkeyAction.Increment, conflict.Dropped.Action);
        Assert.Equal(2, conflict.Dropped.Slot);

        Assert.Contains(plan.Registrations, r => r.Action == HotkeyAction.ToggleInteractive);
        Assert.DoesNotContain(plan.Registrations, r => r.Action == HotkeyAction.Increment && r.Slot == 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("D1")]
    public void An_unusable_pattern_disables_its_row_without_affecting_the_rest(string pattern)
    {
        var settings = Defaults();
        settings.IncrementPattern = pattern;

        var plan = HotkeyPlan.Build(settings, pinnedCount: 4);

        Assert.DoesNotContain(plan.Registrations, r => r.Action == HotkeyAction.Increment);
        Assert.Equal(4, plan.Registrations.Count(r => r.Action == HotkeyAction.Decrement));
    }

    [Fact]
    public void A_pattern_row_that_already_carries_a_key_is_refused()
    {
        // Otherwise every slot would resolve to the same combination and report ten collisions.
        var settings = Defaults();
        settings.IncrementPattern = "Ctrl+Alt+O";

        var plan = HotkeyPlan.Build(settings, pinnedCount: 5);

        Assert.DoesNotContain(plan.Registrations, r => r.Action == HotkeyAction.Increment);
    }

    [Fact]
    public void An_empty_toggle_binding_simply_disables_that_toggle()
    {
        var settings = Defaults();
        settings.ToggleOverlayKey = "";

        var plan = HotkeyPlan.Build(settings, pinnedCount: 0);

        var only = Assert.Single(plan.Registrations);
        Assert.Equal(HotkeyAction.ToggleInteractive, only.Action);
    }

    [Fact]
    public void Every_slot_maps_to_its_own_digit()
    {
        var plan = HotkeyPlan.Build(Defaults(), OverlaySlots.MaxSlots);

        var increments = plan.Registrations
            .Where(r => r.Action == HotkeyAction.Increment)
            .OrderBy(r => r.Slot)
            .ToList();

        Assert.Equal(OverlaySlots.MaxSlots, increments.Count);
        Assert.All(increments, r => Assert.Equal(OverlaySlots.KeyFor(r.Slot), r.Binding.Key));
    }
}
