using System.Windows.Interop;
using WikeloContractor.Interop;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IHotkeyService" />
public sealed class HotkeyService : IHotkeyService, IDisposable
{
    private HwndSource? _sink;

    private IHotkeyBackend? _backend;

    private HotkeyBackendKind _kind = HotkeyBackendKind.Auto;

    private bool _disposed;

    public event EventHandler<HotkeyPressed>? Pressed;

    public event EventHandler? ResultChanged;

    public HotkeyApplyResult LastResult { get; private set; } = HotkeyApplyResult.None;

    public string BackendName => _backend?.Name ?? "none";

    public void Start(HotkeyBackendKind kind)
    {
        _kind = kind;
        _ = EnsureBackend();
    }

    public HotkeyApplyResult Apply(HotkeyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        LastResult = EnsureBackend().Apply(plan);
        ResultChanged?.Invoke(this, EventArgs.Empty);
        return LastResult;
    }

    public void Stop()
    {
        if (_backend is not null)
        {
            _backend.Pressed -= OnBackendPressed;
            _backend.Dispose();
            _backend = null;
        }

        if (_sink is not null)
        {
            _sink.RemoveHook(OnMessage);
            _sink.Dispose();
            _sink = null;
        }

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
    /// The message sink's handle. A dedicated hidden window, not MainWindow and not the overlay: WPF
    /// destroys windows before <c>Exit</c> reaches the host's <c>StopAsync</c>, so hooking a real
    /// window would make hotkey teardown depend on window-close ordering.
    /// <para>
    /// It is a normal top-level window that is simply never shown, <b>not</b> a message-only one. A
    /// message-only window would do for <c>WM_HOTKEY</c>, but a Raw Input sink registered against one
    /// never receives <c>WM_INPUT</c> — and Raw Input is the backend that actually works in game. The
    /// tool-window and no-activate styles keep this window out of Alt+Tab and out of the focus chain.
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
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
        };

        _sink = new HwndSource(parameters);
        _sink.AddHook(OnMessage);
        return _sink.Handle;
    }

    /// <summary>
    /// Creates the backend on first use. <see cref="HotkeyBackendKind.Auto"/> prefers Raw Input and
    /// falls back when the subscription is refused.
    /// <para>
    /// An explicitly chosen Raw Input backend falls back too, rather than leaving the user with no
    /// hotkeys at all — the log line says which one is live, and that is the honest report.
    /// </para>
    /// </summary>
    private IHotkeyBackend EnsureBackend()
    {
        if (_backend is not null)
        {
            return _backend;
        }

        var sink = EnsureSink();
        _backend = CreateBackend(sink);
        _backend.Pressed += OnBackendPressed;

        AppLog.Write("Info", $"Global hotkeys: {_backend.Name} backend (requested {_kind})");
        return _backend;
    }

    private IHotkeyBackend CreateBackend(nint sink)
    {
        if (_kind != HotkeyBackendKind.RegisterHotKey)
        {
            var rawInput = new RawInputBackend();
            if (rawInput.Start(sink))
            {
                return rawInput;
            }

            rawInput.Dispose();
        }

        var registerHotKey = new RegisterHotkeyBackend();
        _ = registerHotKey.Start(sink);
        return registerHotKey;
    }

    private void OnBackendPressed(object? sender, HotkeyPressed pressed) => Pressed?.Invoke(this, pressed);

    /// <summary>
    /// The sink's window procedure hook. Internal so a test can drive decoding directly: what is worth
    /// proving is that a message turns into the right action and slot, and asserting that the OS
    /// actually granted a global combination would be flaky on any machine but the author's.
    /// </summary>
    internal nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_backend?.HandleMessage(msg, wParam, lParam) == true)
        {
            handled = true;
        }

        return nint.Zero;
    }
}
