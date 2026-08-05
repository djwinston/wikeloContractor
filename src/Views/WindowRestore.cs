namespace WikeloContractor.Views;

/// <summary>
/// Bringing a window back from the tray, where "hidden" and "minimized" are both true at once.
/// <para>
/// A three-line rule that lives in its own file because it is <b>ordering</b>, and the wrong order
/// fails silently: see <see cref="Restore"/>. Separated from <c>MainWindow</c> so the ordering can be
/// proven against a real <c>Window</c> in <c>tests/E2E/WindowRestoreTests</c> without constructing
/// the whole shell.
/// </para>
/// </summary>
internal static class WindowRestore
{
    /// <summary>
    /// Shows <paramref name="window"/>, restores it to <paramref name="restoreState"/> and focuses it.
    /// <para>
    /// <b>Show first, then the state.</b> The other order reads better and does not work: WPF defers
    /// a <c>WindowState</c> written while the window is hidden, so the property then reports
    /// <c>Normal</c> and <c>IsVisible</c> reports <c>true</c> while the HWND is still iconic. What the
    /// user sees is a taskbar button appearing and no window — measured with <c>IsIconic</c>, not
    /// reasoned about.
    /// </para>
    /// <para>
    /// The state is written unconditionally rather than only when currently minimized, for the same
    /// reason: after <c>Show()</c> the property is not a reliable account of what the HWND is doing.
    /// </para>
    /// </summary>
    /// <param name="restoreState">
    /// What to come back to. Never <c>Minimized</c>: a window minimized from maximized has to return
    /// maximized, and by the time it is minimized the property can no longer say which it was.
    /// </param>
    internal static void Restore(Window window, WindowState restoreState)
    {
        window.Show();
        window.WindowState = restoreState;
        _ = window.Activate();
    }
}
