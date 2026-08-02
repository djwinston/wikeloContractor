using WikeloContractor.Models;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The hotkey service's decoding path. Lives in the WPF tier because the message sink is a real
/// window and has to be created on an STA dispatcher thread.
/// <para>
/// What is <b>not</b> asserted: that <c>RegisterHotKey</c> succeeded. Whether the OS grants
/// <c>Ctrl+Alt+1</c> depends on what else is running on the machine, so a test demanding it would be
/// flaky everywhere but one desk. The decode is driven through the message hook instead, which is
/// where our own logic lives.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class HotkeyServiceTests(WpfAppFixture fixture)
{
    private static OverlaySettings Settings() => new()
    {
        IncrementPattern = "Ctrl+Alt",
        DecrementPattern = "Ctrl+Shift",
        ToggleOverlayKey = "Ctrl+Alt+O",
        ToggleInteractiveKey = "Ctrl+Alt+I",
    };

    /// <summary>Feeds the service a WM_HOTKEY as Windows would, and returns what it reported.</summary>
    private static HotkeyPressed? Fire(HotkeyService service, int id)
    {
        HotkeyPressed? seen = null;
        void OnPressed(object? _, HotkeyPressed pressed) => seen = pressed;

        service.Pressed += OnPressed;
        try
        {
            var handled = false;
            _ = service.OnMessage(nint.Zero, 0x0312, id, nint.Zero, ref handled);
        }
        finally
        {
            service.Pressed -= OnPressed;
        }

        return seen;
    }

    private static int IdOf(HotkeyApplyResult result, HotkeyAction action, int slot) =>
        result.Registered.Concat(result.Failed).Single(r => r.Action == action && r.Slot == slot).Id;

    private Task<T> WithServiceAsync<T>(Func<HotkeyService, T> body) => fixture.OnUiAsync(() =>
    {
        var service = new HotkeyService();
        try
        {
            return body(service);
        }
        finally
        {
            service.Dispose();
        }
    });

    [Fact]
    public async Task A_slot_hotkey_decodes_to_its_action_and_slot()
    {
        var pressed = await WithServiceAsync(service =>
        {
            var result = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 4));
            return Fire(service, IdOf(result, HotkeyAction.Increment, 3));
        });

        Assert.Equal(new HotkeyPressed(HotkeyAction.Increment, 3), pressed);
    }

    [Fact]
    public async Task Increment_and_decrement_on_the_same_slot_stay_distinct()
    {
        var (up, down) = await WithServiceAsync(service =>
        {
            var result = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 2));
            return (
                Fire(service, IdOf(result, HotkeyAction.Increment, 2)),
                Fire(service, IdOf(result, HotkeyAction.Decrement, 2)));
        });

        Assert.Equal(new HotkeyPressed(HotkeyAction.Increment, 2), up);
        Assert.Equal(new HotkeyPressed(HotkeyAction.Decrement, 2), down);
    }

    [Fact]
    public async Task A_toggle_decodes_with_no_slot()
    {
        var pressed = await WithServiceAsync(service =>
        {
            var result = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 1));
            return Fire(service, IdOf(result, HotkeyAction.ToggleInteractive, 0));
        });

        Assert.Equal(new HotkeyPressed(HotkeyAction.ToggleInteractive, 0), pressed);
    }

    [Fact]
    public async Task An_unknown_id_is_ignored()
    {
        var pressed = await WithServiceAsync(service =>
        {
            _ = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 1));
            return Fire(service, 0x7FFF);
        });

        Assert.Null(pressed);
    }

    [Fact]
    public async Task Another_window_message_is_ignored()
    {
        var pressed = await WithServiceAsync(service =>
        {
            var result = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 1));
            var id = IdOf(result, HotkeyAction.Increment, 1);

            HotkeyPressed? seen = null;
            service.Pressed += (_, p) => seen = p;
            var handled = false;
            _ = service.OnMessage(nint.Zero, 0x0100 /* WM_KEYDOWN */, id, nint.Zero, ref handled);
            return seen;
        });

        Assert.Null(pressed);
    }

    [Fact]
    public async Task Re_applying_forgets_the_slots_that_are_no_longer_pinned()
    {
        // Unpinning items must release their digits, not leave stale ones decoding into empty slots.
        var pressed = await WithServiceAsync(service =>
        {
            var wide = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 6));
            var staleId = IdOf(wide, HotkeyAction.Increment, 6);

            _ = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 2));
            return Fire(service, staleId);
        });

        Assert.Null(pressed);
    }

    [Fact]
    public async Task Every_planned_registration_is_accounted_for()
    {
        // Whether Win32 accepts them is the machine's business; what must hold is that none is lost.
        var (planned, seen) = await WithServiceAsync(service =>
        {
            var plan = HotkeyPlan.Build(Settings(), pinnedCount: 10);
            var result = service.Apply(plan);
            return (plan.Registrations.Count, result.Registered.Count + result.Failed.Count);
        });

        Assert.Equal(22, planned); // two toggles plus ten slots each way
        Assert.Equal(planned, seen);
    }

    [Fact]
    public async Task Stopping_clears_the_result_and_the_decoding_table()
    {
        var (pressed, result) = await WithServiceAsync(service =>
        {
            var applied = service.Apply(HotkeyPlan.Build(Settings(), pinnedCount: 3));
            var id = IdOf(applied, HotkeyAction.Increment, 1);

            service.Stop();
            return (Fire(service, id), service.LastResult);
        });

        Assert.Null(pressed);
        Assert.Empty(result.Registered);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public async Task Start_is_idempotent_and_stop_is_safe_twice()
    {
        // Called from host startup/shutdown, which can run either more than once (a restart) or after
        // a partial failure — neither may throw.
        await fixture.OnUiAsync(() =>
        {
            var service = new HotkeyService();
            service.Start();
            service.Start();
            service.Stop();
            service.Stop();
            service.Dispose();
            service.Dispose();
        });
    }
}
