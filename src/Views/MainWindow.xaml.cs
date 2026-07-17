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
    }

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
        base.OnClosed(e);

        // Closing the main window shuts down the application
        Application.Current.Shutdown();
    }
}
