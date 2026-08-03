using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <summary>
/// How presses physically reach us. Two implementations exist because the obvious one does not work
/// where the app is actually used: <see cref="RegisterHotkeyBackend"/> goes through the system hotkey
/// table, which a focused game can shut off, while <see cref="RawInputBackend"/> receives
/// <c>WM_INPUT</c> from a sink that the foreground window has no say over.
/// <para>
/// The backend owns the OS resources and the decoding of one message. It owns nothing about the
/// domain — that stays in <see cref="HotkeyService"/> above it and <c>IOverlayService</c> above that.
/// </para>
/// </summary>
internal interface IHotkeyBackend : IDisposable
{
    /// <summary>Name for the log and the diagnostics row, so a field report says which path ran.</summary>
    string Name { get; }

    /// <summary>Raised on the sink window's thread — the UI thread — when one of ours fires.</summary>
    event EventHandler<HotkeyPressed>? Pressed;

    /// <summary>
    /// Claims whatever the OS needs, delivering to <paramref name="sink"/>. False means the mechanism
    /// is unavailable on this machine and the caller should try another one; it is not an exception,
    /// because "unavailable" is an expected outcome here rather than a bug.
    /// </summary>
    bool Start(nint sink);

    /// <summary>Replaces the live set with the plan's, reporting what the OS accepted.</summary>
    HotkeyApplyResult Apply(HotkeyPlan plan);

    /// <summary>
    /// Window-message hook. Returns true only when the message is <b>fully</b> ours and must not go on
    /// to <c>DefWindowProc</c> — which is why the Raw Input backend answers false even for the message
    /// it consumes: <c>WM_INPUT</c> still has to reach the default handler for buffer cleanup.
    /// </summary>
    bool HandleMessage(int msg, nint wParam, nint lParam);

    /// <summary>Releases everything claimed. Safe to call twice.</summary>
    void Stop();
}
