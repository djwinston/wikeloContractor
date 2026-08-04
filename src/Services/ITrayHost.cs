namespace WikeloContractor.Services;

/// <summary>
/// The shell window, as much of it as the notification-area icon needs. An interface for the same
/// reason <see cref="IOverlayWindow"/> is one: the decisions — what minimizing means, what the menu
/// items do — are provable without a real <c>Window</c> and the render stack behind it.
/// </summary>
public interface ITrayHost
{
    /// <summary>
    /// Brings the shell back from the tray, where it is normally hidden <b>and</b> still minimized.
    /// The order is load-bearing and gets this wrong silently, so an implementation delegates to
    /// <c>Views.WindowRestore.Restore</c> rather than restating it — the same way
    /// <see cref="IOverlayWindow.ShowOverlay"/> names <c>OverlayPlacement</c> as the clamping
    /// authority instead of describing the clamp.
    /// </summary>
    void RestoreWindow();

    /// <summary>
    /// Takes the shell off the screen and out of the taskbar. The tray icon is then the only way
    /// back, which is why it is registered for the whole run rather than only while hidden.
    /// </summary>
    void HideWindow();

    /// <summary>
    /// Whether the notification area is currently showing our icon. Read immediately before hiding,
    /// never cached: this is false when registration failed at startup, and it goes false again if
    /// Explorer restarts and the icon cannot be put back.
    /// </summary>
    bool IsTrayAvailable { get; }

    /// <summary>
    /// Closes the shell. Deliberately the same call <c>INavigationWindow</c> already declares:
    /// closing MainWindow is the app's single exit trigger, and the tray's Exit item must route
    /// through it rather than become a second one that skips <c>StopAsync</c>.
    /// </summary>
    void CloseWindow();
}
