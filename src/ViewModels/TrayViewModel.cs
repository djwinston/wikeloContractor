using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The notification-area menu, and the one window rule behind it.
/// <para>
/// The tray exists for the session shape this app is used in: the window is not what the player looks
/// at while playing — the overlay is — so the shell spends most of a session out of the way. The menu
/// is therefore deliberately small: bring the window back, put the HUD up or take it down, quit.
/// </para>
/// <para>
/// Holds no <c>Window</c> reference, only <see cref="ITrayHost"/>, which the window hands over itself
/// through <see cref="Attach"/>. Injecting the window here instead would be a construction cycle:
/// the tray menu lives in <c>MainWindow.xaml</c> and binds to this.
/// </para>
/// </summary>
public sealed partial class TrayViewModel(
    IOverlayService overlay,
    OverlayViewModel hud,
    ISettingsService settings)
{
    private ITrayHost? _host;

    /// <summary>
    /// The HUD's own state, so the menu can show whether the overlay is currently up. Worth the
    /// binding because the overlay may be up behind a fullscreen game, where the menu is the only
    /// place its state is visible.
    /// </summary>
    public OverlayViewModel Hud { get; } = hud;

    /// <summary>Binds the shell window. Called by the window itself, once, from its constructor.</summary>
    public void Attach(ITrayHost host) => _host = host;

    /// <summary>Forgets the window. A closed WPF window is permanently dead — nothing may be sent to it.</summary>
    public void Detach() => _host = null;

    /// <summary>
    /// The shell's window state changed. Whether minimizing means "to the tray" is the user's
    /// setting, read live rather than cached: the Settings page writes it while the app runs.
    /// </summary>
    public void OnWindowStateChanged(WindowState state)
    {
        if (state != WindowState.Minimized || !settings.Current.MinimizeToTray)
        {
            return;
        }

        // No icon, no hiding. Hide() removes the window from the taskbar AND from Alt+Tab, so with
        // nothing in the notification area the shell would be reachable only through Task Manager —
        // the same "the app is broken and I can't fix it" failure OverlayService.Initialize guards
        // against when the interactive-mode hotkey did not register. Minimizing normally is the
        // honest fallback: the setting is a preference, not a promise.
        if (_host is { IsTrayAvailable: true })
        {
            _host.HideWindow();
        }
    }

    [RelayCommand]
    private void ShowApp() => _host?.RestoreWindow();

    [RelayCommand]
    private void ToggleOverlay() => overlay.Toggle();

    /// <summary>
    /// Quits by closing the shell rather than calling <c>Application.Shutdown</c> directly, so the
    /// tray does not become a second exit path — the window's <c>OnClosed</c> is what triggers the
    /// shutdown that flushes the inventory store and saves the overlay's geometry.
    /// </summary>
    [RelayCommand]
    private void Exit() => _host?.CloseWindow();
}
