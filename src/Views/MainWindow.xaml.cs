using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WikeloContractor.Interop;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WikeloContractor.Models;
using WikeloContractor.Services;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views;

public partial class MainWindow : INavigationWindow, ITrayHost
{
    public MainWindowViewModel ViewModel { get; }

    /// <summary>Last (app theme, shell-light) pair the icons were built for; skips redundant rebuilds.</summary>
    private (ApplicationTheme Theme, bool ShellLight)? _appliedIconState;

    /// <summary>The state to come back to from the tray — normal or maximized, never minimized.</summary>
    private WindowState _restoreState = WindowState.Normal;

    /// <summary>The runtime number of the shell's <c>TaskbarCreated</c> broadcast; 0 until hooked.</summary>
    private uint _taskbarCreatedMessage;

    /// <summary>Frozen taskbar bitmaps, decoded once per art (navy on a light shell, cyan on a dark one).</summary>
    private BitmapImage? _taskbarLightShellIcon;
    private BitmapImage? _taskbarDarkShellIcon;

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigationService,
        ISettingsService settingsService)
    {
        ViewModel = viewModel;
        DataContext = this;

        // Watch the system theme only when the user selected "System".
        // updateAccents MUST stay false: it defaults to true, and then every system theme
        // evaluation re-derives the accent from the Windows accent colour, silently replacing the
        // brand accent ApplicationHostService.ApplyTheme just installed. Same reason ApplyTheme
        // passes updateAccent: false to ApplicationThemeManager.Apply.
        if (settingsService.Current.Theme == AppTheme.System)
        {
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: false);
        }

        InitializeComponent();

        navigationService.SetNavigationControl(RootNavigation);

        // A ContextMenu is not part of the visual tree, so it inherits nothing from this window.
        // NotifyIcon does forward its own DataContext to the menu, but only while the menu's is
        // still null and only once it has one itself — too many conditions for markup that silently
        // degrades into a menu of greyed-out items when it does not hold.
        TrayMenu.DataContext = this;
        ViewModel.Tray.Attach(this);

        UpdateAppIcon(ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += OnThemeChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>
    /// The WPF-UI theme changed. Our brand layer is not its business, so re-apply it or the dark
    /// chip tints, blueprint role and completed-row wash stay on a light surface.
    /// The <c>systemAccent</c> argument is deliberately ignored — the app always uses the brand one.
    /// </summary>
    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, System.Windows.Media.Color systemAccent) =>
        ReapplyBrandLayer(currentApplicationTheme);

    /// <summary>Windows theme changes do not raise <see cref="ApplicationThemeManager.Changed"/>.</summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Dispatcher.Invoke(() => ReapplyBrandLayer(ApplicationThemeManager.GetAppTheme()));
        }
    }

    /// <summary>
    /// Re-applies our theme-dependent layer after the WPF-UI theme changed underneath us.
    /// <para>
    /// Deliberately driven from BOTH <see cref="OnThemeChanged"/> and
    /// <see cref="OnUserPreferenceChanged"/>: a Windows light/dark flip notifies the
    /// <c>SystemThemeWatcher</c> and this window through the same OS message, and their order is
    /// not guaranteed. Whichever fires second sees the settled theme and corrects the layer, so
    /// the palette cannot end up stale. Both calls are idempotent.
    /// </para>
    /// </summary>
    private void ReapplyBrandLayer(ApplicationTheme applied)
    {
        ApplicationHostService.ApplyBrandLayer(applied);
        UpdateAppIcon(applied);
    }

    /// <summary>
    /// Keeps the app mark readable. The two surfaces differ in both asset kind and in which theme
    /// decides: the title bar sits on a surface this app paints, while the taskbar sits on one
    /// Windows paints, and the two themes are set independently.
    /// </summary>
    private void UpdateAppIcon(ApplicationTheme theme)
    {
        // General preference changes fire often (accent colour, cursor, …); rebuild only when a
        // signal an icon actually depends on has changed, so the PNG decode below is not repeated.
        var shellLight = IsWindowsShellLight();
        if (_appliedIconState == (theme, shellLight))
        {
            return;
        }

        _appliedIconState = (theme, shellLight);

        // Title bar: the vector mark, crisp at any DPI. Follows the app's own theme.
        // Do not set an explicit size: the ui:TitleBar template constrains its icon slot, and
        // anything larger is clipped flat top and bottom rather than scaled.
        var markKey = theme == ApplicationTheme.Dark ? "AppMarkLight" : "AppMarkDark";
        TitleBarControl.Icon = new ImageIcon { Source = (ImageSource)Application.Current.Resources[markKey] };

        // Taskbar/Alt-Tab: must stay a raster bitmap, WPF hands it to Win32 as an HICON. Follows
        // the Windows theme — picking by app theme puts the navy mark on a dark taskbar at a 1.2:1
        // contrast ratio whenever the two disagree.
        var shellArtwork = shellLight
            ? TaskbarIcon(ref _taskbarLightShellIcon, "icon.png")
            : TaskbarIcon(ref _taskbarDarkShellIcon, "icon-light.png");

        Icon = shellArtwork;

        // The notification area sits on the same surface Windows paints, so it takes the same
        // artwork by the same rule. Assigning after registration is supported — NotifyIcon reacts
        // by re-sending the icon to the shell.
        TrayIcon.Icon = shellArtwork;
    }

    /// <summary>Decodes a taskbar bitmap once and freezes it; later calls reuse the cached instance.</summary>
    private static BitmapImage TaskbarIcon(ref BitmapImage? cache, string assetFile)
    {
        if (cache is null)
        {
            var icon = new BitmapImage(new Uri($"pack://application:,,,/Assets/{assetFile}"));
            icon.Freeze();
            cache = icon;
        }

        return cache;
    }

    /// <summary>
    /// Reads the taskbar/Start theme. This is <c>SystemUsesLightTheme</c>, a separate setting from
    /// the <c>AppsUseLightTheme</c> value that drives app surfaces. Defaults to dark, the Windows 11
    /// default, when the value is missing.
    /// </summary>
    private static bool IsWindowsShellLight() =>
        Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme",
            0) is int value && value != 0;

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        // Not used: pages are provided by INavigationViewPageProvider via DI
    }

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();

    /// <inheritdoc />
    public void RestoreWindow() => WindowRestore.Restore(this, _restoreState);

    /// <inheritdoc />
    public void HideWindow() => Hide();

    /// <inheritdoc />
    public bool IsTrayAvailable => TrayIcon.IsRegistered;

    /// <summary>
    /// Listens for the shell's <c>TaskbarCreated</c> broadcast. Hooked here, on the shell's own
    /// window, because that message goes to top-level windows and this is the one the icon belongs to.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage(NativeMethods.TaskbarCreatedMessage);
        ((HwndSource)PresentationSource.FromVisual(this)!).AddHook(OnWindowMessage);
    }

    /// <summary>
    /// Explorer restarted and rebuilt the notification area, taking every icon in it with it.
    /// <c>Wpf.Ui.Tray</c> does not handle this at all, so without re-registering here the icon is
    /// gone for the rest of the session while <c>IsRegistered</c> still claims otherwise — and with
    /// minimize-to-tray on, that is a window with nowhere to come back from.
    /// <para>
    /// Re-registering is enough on its own: <c>TrayManager.Register</c> repopulates the whole
    /// <c>NOTIFYICONDATA</c>, icon and tooltip included, so nothing has to be re-applied afterwards.
    /// </para>
    /// </summary>
    private nint OnWindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // Never marked handled: the broadcast is not ours to consume.
        if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage)
        {
            TrayIcon.Register();

            // IsRegistered rather than Register's own answer, because that flag is what
            // ITrayHost.IsTrayAvailable reports and therefore what actually gates hiding.
            LogTrayRegistration("re-registered after the notification area was rebuilt");
        }

        return nint.Zero;
    }

    /// <summary>
    /// Left-clicking the tray icon opens the app, the convention every tray application follows.
    /// This also fires as the first half of a double click, which is harmless — restoring an already
    /// restored window does nothing.
    /// </summary>
    private void OnTrayIconLeftClick(object sender, RoutedEventArgs e) => ViewModel.Tray.ShowAppCommand.Execute(null);

    /// <summary>
    /// First render done, which is when <c>NotifyIcon</c> registers itself — on <c>OnRender</c>,
    /// not on load.
    /// </summary>
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        LogTrayRegistration("registered");
    }

    /// <summary>
    /// Records whether the icon is in the notification area. It reports a failure nowhere else:
    /// there is simply no icon, which looks exactly like the user keeping it in the overflow
    /// flyout. One log line is the whole diagnostic, same as the hotkey backend's.
    /// </summary>
    private void LogTrayRegistration(string what)
    {
        var registered = TrayIcon.IsRegistered;

        AppLog.Write(
            registered ? "Info" : "Warning",
            registered
                ? $"Tray icon {what}."
                : $"Tray icon not {what} — the notification area menu is unavailable and the window "
                  + "will minimize to the taskbar instead.");
    }

    /// <summary>
    /// Minimizing is the only state change the tray cares about; the rule itself is the VM's.
    /// The other states are remembered here, because coming back from the tray has to know which
    /// one to come back to and by then <see cref="Window.WindowState"/> only says "minimized".
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState != WindowState.Minimized)
        {
            _restoreState = WindowState;
        }

        ViewModel.Tray.OnWindowStateChanged(WindowState);
    }

    protected override void OnClosed(EventArgs e)
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        // Explicit, because NotifyIcon only unregisters when it is disposed and nothing disposes a
        // control. Left to the finalizer, the icon lingers in the notification area as a ghost that
        // disappears only when the user happens to hover over it.
        ViewModel.Tray.Detach();
        TrayIcon.Unregister();

        base.OnClosed(e);

        // Closing the main window shuts down the application
        Application.Current.Shutdown();
    }
}
