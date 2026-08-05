using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Velopack;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using WikeloContractor.Services;
using WikeloContractor.Services.Api;

namespace WikeloContractor;

public partial class App
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging =>
        {
            // File log next to Update.exe — see AppLog for why not next to the executable.
            _ = logging.AddProvider(new FileLoggerProvider());
        })
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
            _ = services.AddSingleton<ICatalogImageOverrideService, CatalogImageOverrideService>();

            // Completed contracts + accumulated Wikelo reputation.
            _ = services.AddSingleton<ICompletionService, CompletionService>();

            // Favorited contracts (the Favorites page is the catalog filtered to these).
            _ = services.AddSingleton<IFavoritesService, FavoritesService>();

            // Personal inventory: counter store + user-supplied item images.
            _ = services.AddSingleton<IInventoryStore, InventoryStore>();
            _ = services.AddSingleton<IInventoryImageOverrideService, InventoryImageOverrideService>();

            // Items the user pinned to the in-game overlay, in slot order. The budget counter is a
            // singleton because there is one set of pins: the inventory grid and the Favorites
            // gathering plan show the same "Overlay 3/10", not two copies of it.
            _ = services.AddSingleton<IPinnedItemsService, PinnedItemsService>();
            _ = services.AddSingleton<ViewModels.OverlayPinsViewModel>();

            // Global hotkeys (the overlay's whole point: no alt-tab out of the game).
            _ = services.AddSingleton<IHotkeyService, HotkeyService>();

            // In-game overlay. The window is TRANSIENT while everything else here is a singleton:
            // a closed WPF Window is permanently dead (Show() throws after Close()), so a singleton
            // window could never come back. The coordinator takes a factory rather than the window
            // itself — that also breaks what would otherwise be a construction cycle, and is the
            // seam the E2E tests substitute.
            _ = services.AddSingleton<ViewModels.OverlayViewModel>();
            _ = services.AddSingleton<IOverlayService, OverlayService>();
            _ = services.AddTransient<IOverlayWindow, Views.OverlayWindow>();
            _ = services.AddSingleton<Func<IOverlayWindow>>(provider => provider.GetRequiredService<IOverlayWindow>);

            // "Where to find" knowledge base: Markdown files shipped in the install dir, plus a
            // %AppData% layer the user owns. See docs/sourcing/README.md.
            _ = services.AddSingleton<ISourcingGuideService, SourcingGuideService>();

            // Completion ↔ inventory flow (deduction/warning dialogs), shared by catalog + detail.
            _ = services.AddSingleton<ViewModels.ContractCompletionInteraction>();

            // Self-update (Velopack). No-op in a dev run; drives Settings' "Check for updates".
            _ = services.AddSingleton<IAppUpdateService, AppUpdateService>();

            // Main window. It also implements ITrayHost, which is deliberately not registered:
            // the window hands itself to the tray view model, so the tray never resolves a window
            // and the two can be constructed in either order.
            _ = services.AddSingleton<INavigationWindow, Views.MainWindow>();
            _ = services.AddSingleton<ViewModels.MainWindowViewModel>();
            _ = services.AddSingleton<ViewModels.TrayViewModel>();

            // Pages and their ViewModels
            _ = services.AddSingleton<Views.Pages.CatalogPage>();
            _ = services.AddSingleton<ViewModels.CatalogViewModel>();
            _ = services.AddSingleton<Views.Pages.FavoritesPage>();
            _ = services.AddSingleton<ViewModels.FavoritesViewModel>();
            _ = services.AddSingleton<Views.Pages.ContractDetailPage>();
            _ = services.AddSingleton<ViewModels.ContractDetailViewModel>();
            _ = services.AddSingleton<Views.Pages.InventoryPage>();
            _ = services.AddSingleton<ViewModels.InventoryViewModel>();
            _ = services.AddSingleton<Views.Pages.SourcingPage>();
            _ = services.AddSingleton<ViewModels.SourcingViewModel>();
            _ = services.AddSingleton<Views.Pages.SourcingDetailPage>();
            _ = services.AddSingleton<ViewModels.SourcingDetailViewModel>();
            _ = services.AddSingleton<Views.Pages.SettingsPage>();
            _ = services.AddSingleton<ViewModels.SettingsViewModel>();
            _ = services.AddSingleton<Views.Pages.AboutPage>();
            _ = services.AddSingleton<ViewModels.AboutViewModel>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        AppLog.Write("Information", $"--- Startup, version {AppVersion.Current} ---");

        // Must run before any UI: handles Velopack install/update/uninstall hooks (the installer
        // relaunches the exe with special args) and exits the process for those, so a normal
        // launch falls straight through to starting the host. The logger is attached here rather
        // than through DI because those hooks run and exit before the host is ever built.
        VelopackApp.Build()
            .SetLogger(new VelopackFileLogger())
            .Run();

        // After the hooks: the updater's own log is complete for the run that just happened.
        AppLog.MirrorUpdaterLog();

        // The overlay makes this a two-window app, and the default OnLastWindowClose would then make
        // "does the app exit?" depend on how many windows happen to be open. MainWindow.OnClosed's
        // Application.Current.Shutdown() stays the single exit trigger.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        await _host.StartAsync();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Recorded but not swallowed: the crash still surfaces as it did before, it is simply no
        // longer invisible afterwards. Turning it into a friendly dialog is a separate decision.
        AppLog.Write("Critical", "Unhandled dispatcher exception.", e.Exception);
    }
}
