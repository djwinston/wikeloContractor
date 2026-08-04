using System.Runtime.InteropServices;
using System.Windows.Input;
using WikeloContractor.Interop;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <summary>
/// The default backend: a Raw Input keyboard subscription with <c>RIDEV_INPUTSINK</c>, so
/// <c>WM_INPUT</c> arrives whoever is in the foreground.
/// <para>
/// This exists because <see cref="RegisterHotkeyBackend"/> demonstrably does not work in game.
/// <c>RegisterHotKey</c> delivers through the system hotkey table, and a foreground application can
/// take that table out of service for everybody — Raw Input's own <c>RIDEV_NOHOTKEYS</c> does exactly
/// that. The symptom is the worst kind: registration succeeds, no error is reported anywhere, the keys
/// work on the desktop, and nothing happens once the game has focus. Running elevated does not fix it.
/// </para>
/// <para>
/// Three consequences worth knowing. We do <b>not</b> claim the combination, so Star Citizen still
/// receives the same keystroke — nothing is stolen, but a combination the player has also bound
/// in-game will do both things. Nothing can "fail to register", so
/// <see cref="HotkeyApplyResult.Failed"/> is always empty here. And the sink sees every keystroke on
/// the machine, which is why <see cref="HotkeyLookup.IsTrigger"/> is consulted before anything else is
/// read: input that is none of our business is dropped in one set lookup, never stored, never logged.
/// </para>
/// <para>
/// This is not a keyboard hook. Raw Input is passive — it cannot swallow, alter or inject a keystroke
/// — which is what keeps it on the right side of anti-cheat. See <see cref="NativeMethods"/>.
/// </para>
/// </summary>
internal sealed class RawInputBackend : IHotkeyBackend
{
    private readonly Func<HotkeyModifiers> _readModifiers;

    private HotkeyLookup _lookup = HotkeyLookup.Empty;

    private nint _sink;

    private bool _subscribed;

    /// <param name="readModifiers">
    /// How to read the modifier keys held at the moment of a press; defaults to the physical key
    /// state. Injectable so the decode can be tested without a keyboard.
    /// </param>
    internal RawInputBackend(Func<HotkeyModifiers>? readModifiers = null) =>
        _readModifiers = readModifiers ?? PhysicalModifiers;

    public string Name => "RawInput";

    public event EventHandler<HotkeyPressed>? Pressed;

    public bool Start(nint sink)
    {
        if (_subscribed)
        {
            return true;
        }

        _sink = sink;

        var device = new NativeMethods.RawInputDevice
        {
            UsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
            Usage = NativeMethods.HID_USAGE_GENERIC_KEYBOARD,
            Flags = NativeMethods.RIDEV_INPUTSINK,
            Target = sink,
        };

        _subscribed = NativeMethods.RegisterRawInputDevices(
            ref device, 1, (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>());

        if (!_subscribed)
        {
            AppLog.Write(
                "Warning",
                $"RegisterRawInputDevices failed, win32 error {Marshal.GetLastWin32Error()} — "
                    + "falling back to the RegisterHotKey backend");
        }

        return _subscribed;
    }

    public HotkeyApplyResult Apply(HotkeyPlan plan)
    {
        _lookup = HotkeyLookup.From(plan);

        // Nothing was asked of the OS, so nothing can have been refused. Conflicts still pass through:
        // those are collisions between two of our own bindings, which this backend cannot fix either.
        return new HotkeyApplyResult(plan.Registrations, [], plan.Conflicts);
    }

    public bool HandleMessage(int msg, nint wParam, nint lParam)
    {
        if (msg != NativeMethods.WM_INPUT)
        {
            return false;
        }

        var buffer = default(NativeMethods.RawInputKeyboard);
        var size = (uint)Marshal.SizeOf<NativeMethods.RawInputKeyboard>();

        var copied = NativeMethods.GetRawInputData(
            lParam,
            NativeMethods.RID_INPUT,
            ref buffer,
            ref size,
            (uint)Marshal.SizeOf<NativeMethods.RawInputHeader>());

        if (copied != unchecked((uint)-1)
            && buffer.Header.Type == NativeMethods.RIM_TYPEKEYBOARD
            && (buffer.Keyboard.Flags & NativeMethods.RI_KEY_BREAK) == 0)
        {
            OnKeyDown(buffer.Keyboard.VKey);
        }

        // Never true, even though the message was consumed: WM_INPUT must still reach DefWindowProc so
        // the system can release the buffer behind the handle.
        return false;
    }

    public void Stop()
    {
        _lookup = HotkeyLookup.Empty;

        if (!_subscribed)
        {
            return;
        }

        // RIDEV_REMOVE requires a null target — passing the sink handle here fails the call.
        var device = new NativeMethods.RawInputDevice
        {
            UsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
            Usage = NativeMethods.HID_USAGE_GENERIC_KEYBOARD,
            Flags = NativeMethods.RIDEV_REMOVE,
            Target = nint.Zero,
        };

        _ = NativeMethods.RegisterRawInputDevices(
            ref device, 1, (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>());

        _subscribed = false;
        _sink = nint.Zero;
    }

    public void Dispose() => Stop();

    /// <summary>
    /// One key going down, already stripped of the message plumbing. Internal so the decode — the only
    /// part of this backend that holds a decision — is testable without a real <c>WM_INPUT</c>, whose
    /// payload only the OS can produce.
    /// <para>
    /// Auto-repeat is <b>not</b> filtered out. Windows repeats the make code while a key is held, and
    /// holding a slot digit to add twenty ore in one gesture is what the overlay is for; the
    /// <c>RegisterHotKey</c> path matches by deliberately omitting <c>MOD_NOREPEAT</c>.
    /// </para>
    /// </summary>
    internal void OnKeyDown(int virtualKey)
    {
        if (virtualKey == NativeMethods.VK_NONE)
        {
            return;
        }

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);

        // Cheapest possible rejection, and it runs before the modifiers are even read: everything the
        // user types that is not one of our slot digits or toggles stops here.
        if (key == Key.None || !_lookup.IsTrigger(key))
        {
            return;
        }

        if (_lookup.Match(_readModifiers(), key) is { } registration)
        {
            Pressed?.Invoke(this, new HotkeyPressed(registration.Action, registration.Slot));
        }
    }

    /// <summary>
    /// The modifiers physically held right now. Read from the OS rather than tracked from the raw
    /// stream: a modifier released while the secure desktop was up (a UAC prompt) never reaches this
    /// process, and a tracked set would keep it "held" forever afterwards.
    /// </summary>
    private static HotkeyModifiers PhysicalModifiers()
    {
        var modifiers = HotkeyModifiers.None;

        if (IsDown(NativeMethods.VK_CONTROL)) { modifiers |= HotkeyModifiers.Control; }
        if (IsDown(NativeMethods.VK_MENU)) { modifiers |= HotkeyModifiers.Alt; }
        if (IsDown(NativeMethods.VK_SHIFT)) { modifiers |= HotkeyModifiers.Shift; }
        if (IsDown(NativeMethods.VK_LWIN) || IsDown(NativeMethods.VK_RWIN)) { modifiers |= HotkeyModifiers.Win; }

        return modifiers;

        // The high bit is "currently down"; the low bit is "pressed since the last call" and is
        // deliberately ignored — it would make a modifier look held long after it was released.
        static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
