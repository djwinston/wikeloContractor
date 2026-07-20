using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WikeloContractor.Models;
using WikeloContractor.Services;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views;

public partial class MainWindow : INavigationWindow
{
    public MainWindowViewModel ViewModel { get; }

    /// <summary>Last (app theme, shell-light) pair the icons were built for; skips redundant rebuilds.</summary>
    private (ApplicationTheme Theme, bool ShellLight)? _appliedIconState;

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

        // Watch the system theme only when the user selected "System"
        if (settingsService.Current.Theme == AppTheme.System)
        {
            SystemThemeWatcher.Watch(this);
        }

        InitializeComponent();

        navigationService.SetNavigationControl(RootNavigation);

        UpdateAppIcon(ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += OnThemeChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, System.Windows.Media.Color systemAccent) =>
        UpdateAppIcon(currentApplicationTheme);

    /// <summary>Windows theme changes do not raise <see cref="ApplicationThemeManager.Changed"/>.</summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Dispatcher.Invoke(() => UpdateAppIcon(ApplicationThemeManager.GetAppTheme()));
        }
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
        Icon = shellLight
            ? TaskbarIcon(ref _taskbarLightShellIcon, "icon.png")
            : TaskbarIcon(ref _taskbarDarkShellIcon, "icon-light.png");
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

    protected override void OnClosed(EventArgs e)
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnClosed(e);

        // Closing the main window shuts down the application
        Application.Current.Shutdown();
    }
}
