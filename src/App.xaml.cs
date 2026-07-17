using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using WikeloContractor.Services;
using WikeloContractor.Services.Api;

namespace WikeloContractor;

public partial class App
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // WPF UI: page provider for NavigationView
            _ = services.AddNavigationViewPageProvider();

            // Application lifecycle
            _ = services.AddHostedService<ApplicationHostService>();

            // Application services
            _ = services.AddSingleton<ISettingsService, SettingsService>();
            _ = services.AddSingleton<ILocalizationService, LocalizationService>();
            _ = services.AddSingleton<INavigationService, NavigationService>();

            // Star Citizen Wiki API + contract catalog
            _ = services.AddHttpClient<IStarCitizenWikiClient, StarCitizenWikiClient>();
            _ = services.AddSingleton<IContractCatalogService, ContractCatalogService>();
            _ = services.AddSingleton<ViewModels.RateLimitWatcher>();

            // Main window
            _ = services.AddSingleton<INavigationWindow, Views.MainWindow>();
            _ = services.AddSingleton<ViewModels.MainWindowViewModel>();

            // Pages and their ViewModels
            _ = services.AddSingleton<Views.Pages.CatalogPage>();
            _ = services.AddSingleton<ViewModels.CatalogViewModel>();
            _ = services.AddSingleton<Views.Pages.InventoryPage>();
            _ = services.AddSingleton<ViewModels.InventoryViewModel>();
            _ = services.AddSingleton<Views.Pages.SettingsPage>();
            _ = services.AddSingleton<ViewModels.SettingsViewModel>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        await _host.StartAsync();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // TODO (Phase 5): logging and a friendly error message
    }
}
