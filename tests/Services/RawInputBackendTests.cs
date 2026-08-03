using WikeloContractor.Models;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

/// <summary>
/// The Raw Input backend's decode. No window and no WPF <c>Application</c> are needed: the OS part —
/// the subscription and the <c>WM_INPUT</c> payload, which only Windows can produce — is left out, and
/// what is driven instead is the key-down seam plus an injected modifier reader.
/// <para>
/// What is <b>not</b> asserted: that <c>RegisterRawInputDevices</c> succeeded. That is the machine's
/// business, and <c>HotkeyServiceTests</c> covers the fallback that exists for when it does not.
/// </para>
/// </summary>
public sealed class RawInputBackendTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    private const HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    private const int VirtualKey1 = 0x31;
    private const int VirtualKey3 = 0x33;
    private const int VirtualKeyI = 0x49;
    private const int VirtualKeyA = 0x41;

    private static OverlaySettings Settings() => new()
    {
        IncrementPattern = "Ctrl+Alt",
        DecrementPattern = "Ctrl+Shift",
        ToggleOverlayKey = "Ctrl+Alt+O",
        ToggleInteractiveKey = "Ctrl+Alt+I",
    };

    /// <summary>An applied backend plus the presses it reported, with the modifiers held fixed.</summary>
    private static (RawInputBackend Backend, List<HotkeyPressed> Seen) Applied(
        HotkeyModifiers held, int pinnedCount = 4)
    {
        var seen = new List<HotkeyPressed>();
        var backend = new RawInputBackend(() => held);
        backend.Pressed += (_, pressed) => seen.Add(pressed);
        _ = backend.Apply(HotkeyPlan.Build(Settings(), pinnedCount));

        return (backend, seen);
    }

    [Fact]
    public void A_slot_digit_under_the_increment_pattern_reports_that_slot()
    {
        var (backend, seen) = Applied(CtrlAlt);

        backend.OnKeyDown(VirtualKey3);

        Assert.Equal(new HotkeyPressed(HotkeyAction.Increment, 3), Assert.Single(seen));
    }

    [Fact]
    public void The_same_digit_under_the_other_pattern_decrements()
    {
        var (backend, seen) = Applied(CtrlShift);

        backend.OnKeyDown(VirtualKey3);

        Assert.Equal(new HotkeyPressed(HotkeyAction.Decrement, 3), Assert.Single(seen));
    }

    [Fact]
    public void A_toggle_reports_no_slot()
    {
        var (backend, seen) = Applied(CtrlAlt);

        backend.OnKeyDown(VirtualKeyI);

        Assert.Equal(new HotkeyPressed(HotkeyAction.ToggleInteractive, 0), Assert.Single(seen));
    }

    [Fact]
    public void The_digit_alone_is_ignored()
    {
        // The sink sees every keystroke on the machine. Typing "3" into another application must not
        // move an inventory counter.
        var (backend, seen) = Applied(HotkeyModifiers.None);

        backend.OnKeyDown(VirtualKey3);

        Assert.Empty(seen);
    }

    [Fact]
    public void A_key_that_is_no_binding_of_ours_is_ignored()
    {
        var (backend, seen) = Applied(CtrlAlt);

        backend.OnKeyDown(VirtualKeyA);

        Assert.Empty(seen);
    }

    [Fact]
    public void The_no_key_code_is_ignored()
    {
        // Some keyboards emit VK 0xFF as part of a multi-key sequence; it is never a real press.
        var (backend, seen) = Applied(CtrlAlt);

        backend.OnKeyDown(0xFF);

        Assert.Empty(seen);
    }

    [Fact]
    public void Auto_repeat_is_passed_through()
    {
        // Holding the digit to add twenty ore in one gesture is what the overlay exists for, so a
        // repeated make code must count every time.
        var (backend, seen) = Applied(CtrlAlt);

        backend.OnKeyDown(VirtualKey1);
        backend.OnKeyDown(VirtualKey1);
        backend.OnKeyDown(VirtualKey1);

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void Nothing_fires_before_a_plan_is_applied()
    {
        var seen = new List<HotkeyPressed>();
        var backend = new RawInputBackend(() => CtrlAlt);
        backend.Pressed += (_, pressed) => seen.Add(pressed);

        backend.OnKeyDown(VirtualKey1);

        Assert.Empty(seen);
    }

    [Fact]
    public void Re_applying_forgets_the_slots_that_are_no_longer_pinned()
    {
        var (backend, seen) = Applied(CtrlAlt, pinnedCount: 6);
        _ = backend.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 2));

        backend.OnKeyDown(0x36); // digit 6, pinned a moment ago

        Assert.Empty(seen);
    }

    [Fact]
    public void Stopping_clears_the_bindings()
    {
        var (backend, seen) = Applied(CtrlAlt);
        backend.Stop();

        backend.OnKeyDown(VirtualKey1);

        Assert.Empty(seen);
    }

    [Fact]
    public void Nothing_can_fail_to_register()
    {
        // No combination is claimed from the OS, so partial failure — the thing the RegisterHotKey
        // path reports — cannot happen here. Conflicts between two of our own bindings still do.
        var plan = HotkeyPlan.Build(Settings(), pinnedCount: 10);
        var result = new RawInputBackend(() => CtrlAlt).Apply(plan);

        Assert.Empty(result.Failed);
        Assert.Equal(plan.Registrations.Count, result.Registered.Count);
    }

    [Fact]
    public void A_message_that_is_not_raw_input_is_not_ours()
    {
        Assert.False(new RawInputBackend(() => CtrlAlt).HandleMessage(0x0312 /* WM_HOTKEY */, 1, 0));
    }
}
