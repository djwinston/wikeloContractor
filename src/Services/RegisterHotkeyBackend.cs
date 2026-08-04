using System.Runtime.InteropServices;
using System.Windows.Input;
using WikeloContractor.Interop;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <summary>
/// The classic <c>RegisterHotKey</c> path: the OS holds each combination for us and posts
/// <c>WM_HOTKEY</c> carrying the id we registered it under.
/// <para>
/// Kept as the fallback rather than deleted. It is the only path that guarantees <em>exclusivity</em>
/// — nothing else on the machine can see a combination we hold — and if a future Windows build ever
/// restricts Raw Input sinks, this still works. What it cannot do is survive a foreground application
/// that disables system hotkeys, which is exactly the case the overlay was built for; see
/// <see cref="RawInputBackend"/>.
/// </para>
/// </summary>
internal sealed class RegisterHotkeyBackend : IHotkeyBackend
{
    /// <summary>Ids currently held by Win32, so <see cref="Stop"/> releases exactly what it took.</summary>
    private readonly List<int> _held = [];

    /// <summary>Hotkey id → what it means, for decoding <c>WM_HOTKEY</c>.</summary>
    private readonly Dictionary<int, HotkeyRegistration> _byId = [];

    private nint _sink;

    public string Name => "RegisterHotKey";

    public event EventHandler<HotkeyPressed>? Pressed;

    public bool Start(nint sink)
    {
        _sink = sink;

        // Nothing is claimed up front — combinations are taken one at a time in Apply — so there is no
        // way for this to be "unavailable".
        return true;
    }

    public HotkeyApplyResult Apply(HotkeyPlan plan)
    {
        ReleaseHeld();
        _byId.Clear();

        var registered = new List<HotkeyRegistration>(plan.Registrations.Count);
        var failed = new List<HotkeyRegistration>();

        foreach (var registration in plan.Registrations)
        {
            // Decoding is keyed on the plan, not on what Win32 accepted: an id we failed to claim
            // never arrives anyway, and mapping up front keeps the decode path provable in a test
            // that must not depend on the machine's hotkey table.
            _byId[registration.Id] = registration;

            var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(registration.Binding.Key);

            // HotkeyModifiers mirrors the MOD_* values, so this is a cast rather than a mapping.
            // MOD_NOREPEAT is deliberately NOT set: holding the key to add twenty ore in one go is
            // the gesture the overlay exists for.
            if (NativeMethods.RegisterHotKey(_sink, registration.Id, (uint)registration.Binding.Modifiers, virtualKey))
            {
                _held.Add(registration.Id);
                registered.Add(registration);
                continue;
            }

            failed.Add(registration);
            AppLog.Write(
                "Warning",
                $"RegisterHotKey failed for {registration.Binding.Format()} ({registration.Action}, slot {registration.Slot}), "
                    + $"win32 error {Marshal.GetLastWin32Error()} — most likely another application already owns it");
        }

        return new HotkeyApplyResult(registered, failed, plan.Conflicts);
    }

    public bool HandleMessage(int msg, nint wParam, nint lParam)
    {
        if (msg != NativeMethods.WM_HOTKEY || !_byId.TryGetValue((int)wParam, out var registration))
        {
            return false;
        }

        Pressed?.Invoke(this, new HotkeyPressed(registration.Action, registration.Slot));
        return true;
    }

    public void Stop()
    {
        ReleaseHeld();
        _byId.Clear();
    }

    public void Dispose() => Stop();

    private void ReleaseHeld()
    {
        if (_sink == nint.Zero || _held.Count == 0)
        {
            _held.Clear();
            return;
        }

        foreach (var id in _held)
        {
            _ = NativeMethods.UnregisterHotKey(_sink, id);
        }

        _held.Clear();
    }
}
