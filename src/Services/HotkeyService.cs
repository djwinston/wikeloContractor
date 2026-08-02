using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using WikeloContractor.Interop;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IHotkeyService" />
public sealed class HotkeyService : IHotkeyService, IDisposable
{
    /// <summary>Ids currently held by Win32, so <see cref="Stop"/> releases exactly what it took.</summary>
    private readonly List<int> _held = [];

    /// <summary>Hotkey id → what it means, for decoding <c>WM_HOTKEY</c>.</summary>
    private readonly Dictionary<int, HotkeyRegistration> _byId = [];

    private HwndSource? _sink;

    private bool _disposed;

    public event EventHandler<HotkeyPressed>? Pressed;

    public event EventHandler? ResultChanged;

    public HotkeyApplyResult LastResult { get; private set; } = HotkeyApplyResult.None;

    public void Start() => EnsureSink();

    public HotkeyApplyResult Apply(HotkeyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var handle = EnsureSink();

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
            if (NativeMethods.RegisterHotKey(handle, registration.Id, (uint)registration.Binding.Modifiers, virtualKey))
            {
                _held.Add(registration.Id);
                registered.Add(registration);
                continue;
            }

            failed.Add(registration);
            AppLog.Write(
                "Warn",
                $"RegisterHotKey failed for {registration.Binding.Format()} ({registration.Action}, slot {registration.Slot}), "
                    + $"win32 error {Marshal.GetLastWin32Error()} — most likely another application already owns it");
        }

        LastResult = new HotkeyApplyResult(registered, failed, plan.Conflicts);
        ResultChanged?.Invoke(this, EventArgs.Empty);
        return LastResult;
    }

    public void Stop()
    {
        ReleaseHeld();
        _byId.Clear();

        if (_sink is null)
        {
            return;
        }

        _sink.RemoveHook(OnMessage);
        _sink.Dispose();
        _sink = null;

        LastResult = HotkeyApplyResult.None;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    /// <summary>
    /// The message sink's handle. A dedicated <b>message-only</b> window, not MainWindow and not the
    /// overlay: WPF destroys windows before <c>Exit</c> reaches the host's <c>StopAsync</c>, so hooking
    /// a real window would make hotkey teardown depend on window-close ordering.
    /// <para>
    /// If <c>WM_HOTKEY</c> ever fails to reach a message-only window, dropping the
    /// <c>ParentWindow</c> line below turns this into an ordinary window that is simply never shown —
    /// that is the fallback, not a rewrite.
    /// </para>
    /// </summary>
    private nint EnsureSink()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sink is not null)
        {
            return _sink.Handle;
        }

        var parameters = new HwndSourceParameters("WikeloContractorHotkeySink")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };

        _sink = new HwndSource(parameters);
        _sink.AddHook(OnMessage);
        return _sink.Handle;
    }

    private void ReleaseHeld()
    {
        if (_sink is null || _held.Count == 0)
        {
            _held.Clear();
            return;
        }

        foreach (var id in _held)
        {
            _ = NativeMethods.UnregisterHotKey(_sink.Handle, id);
        }

        _held.Clear();
    }

    /// <summary>
    /// Decodes <c>WM_HOTKEY</c>. Internal so a test can drive it directly: what is worth proving is
    /// the id → action/slot mapping, and asserting that the OS actually granted a global combination
    /// would be flaky on any machine but the author's.
    /// </summary>
    internal nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY || !_byId.TryGetValue((int)wParam, out var registration))
        {
            return nint.Zero;
        }

        handled = true;
        Pressed?.Invoke(this, new HotkeyPressed(registration.Action, registration.Slot));
        return nint.Zero;
    }
}
