using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
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

            // Reward preview images: external CDN downloads + disk cache + user overrides.
            // Registered as a singleton (not AddHttpClient's transient) so the in-flight
            // download deduplication and the politeness semaphore are app-wide; one plain
            // HttpClient for the app lifetime, no factory indirection needed.
            _ = services.AddSingleton<IImageCacheService>(_ => new ImageCacheService(new System.Net.Http.HttpClient()));
            _ = services.AddSingleton<IImageOverrideService, ImageOverrideService>();

            // Completed contracts + accumulated Wikelo reputation.
            _ = services.AddSingleton<ICompletionService, CompletionService>();

            // Personal inventory: counter store + user-supplied item images.
            _ = services.AddSingleton<IInventoryStore, InventoryStore>();
            _ = services.AddSingleton<IInventoryImageOverrideService, InventoryImageOverrideService>();

            // Completion ↔ inventory flow (deduction/warning dialogs), shared by catalog + detail.
            _ = services.AddSingleton<ViewModels.ContractCompletionInteraction>();

            // Self-update (Velopack). No-op in a dev run; drives Settings' "Check for updates".
            _ = services.AddSingleton<IAppUpdateService, AppUpdateService>();

            // Main window
            _ = services.AddSingleton<INavigationWindow, Views.MainWindow>();
            _ = services.AddSingleton<ViewModels.MainWindowViewModel>();

            // Pages and their ViewModels
            _ = services.AddSingleton<Views.Pages.CatalogPage>();
            _ = services.AddSingleton<ViewModels.CatalogViewModel>();
            _ = services.AddSingleton<Views.Pages.ContractDetailPage>();
            _ = services.AddSingleton<ViewModels.ContractDetailViewModel>();
            _ = services.AddSingleton<Views.Pages.InventoryPage>();
            _ = services.AddSingleton<ViewModels.InventoryViewModel>();
            _ = services.AddSingleton<Views.Pages.SettingsPage>();
            _ = services.AddSingleton<ViewModels.SettingsViewModel>();
            _ = services.AddSingleton<Views.Pages.AboutPage>();
            _ = services.AddSingleton<ViewModels.AboutViewModel>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // Must run before any UI: handles Velopack install/update/uninstall hooks (the installer
        // relaunches the exe with special args) and exits the process for those, so a normal
        // launch falls straight through to starting the host.
        VelopackApp.Build().Run();

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
