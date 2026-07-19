using System.Reflection;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The About page: app version, self-update (Velopack), attribution and disclaimer. Split out of
/// Settings so each page stays focused.
/// </summary>
public partial class AboutViewModel : ViewModel
{
    private readonly IAppUpdateService _appUpdateService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppUpdateMessage))]
    [NotifyPropertyChangedFor(nameof(HasAppUpdateMessage))]
    [NotifyPropertyChangedFor(nameof(IsAppUpdateBusy))]
    [NotifyPropertyChangedFor(nameof(IsAppUpdateReady))]
    [NotifyCanExecuteChangedFor(nameof(CheckAppUpdateCommand))]
    private AppUpdateStatus _appUpdateStatus;

    public bool IsAppUpdateBusy => AppUpdateStatus is AppUpdateStatus.Checking or AppUpdateStatus.Downloading;

    public bool IsAppUpdateReady => AppUpdateStatus == AppUpdateStatus.ReadyToRestart;

    public bool HasAppUpdateMessage => AppUpdateMessage is not null;

    /// <summary>Localized status line for the self-update row; null when idle (nothing to show).</summary>
    public string? AppUpdateMessage => AppUpdateStatus switch
    {
        AppUpdateStatus.Checking => Localized.String("Settings_AppUpdate_Checking"),
        AppUpdateStatus.UpToDate => Localized.String("Settings_AppUpdate_UpToDate"),
        AppUpdateStatus.Downloading => Localized.String("Settings_AppUpdate_Downloading"),
        AppUpdateStatus.ReadyToRestart => Localized.Format("Settings_AppUpdate_Ready", _appUpdateService.AvailableVersion ?? string.Empty),
        AppUpdateStatus.Failed => Localized.String("Settings_AppUpdate_Failed"),
        _ => null,
    };

    /// <summary>App version from the assembly (single source: &lt;Version&gt; in the csproj).</summary>
    public string AppVersion { get; } =
        typeof(AboutViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public AboutViewModel(IAppUpdateService appUpdateService)
    {
        _appUpdateService = appUpdateService;

        AppUpdateStatus = appUpdateService.Status;

        // No unsubscription: both this VM and the service are app-lifetime DI singletons, so the
        // subscription lives exactly as long as both objects do (no leak, no per-navigation buildup).
        appUpdateService.StatusChanged += OnAppUpdateStatusChanged;
    }

    // StatusChanged may fire on a background thread; hop to the UI thread without blocking the worker.
    private void OnAppUpdateStatusChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.BeginInvoke(() => AppUpdateStatus = _appUpdateService.Status);

    /// <summary>Checks the release feed and downloads a pending update (→ "restart to apply").</summary>
    [RelayCommand(CanExecute = nameof(CanCheckAppUpdate))]
    private Task CheckAppUpdateAsync() => _appUpdateService.CheckAndDownloadAsync();

    private bool CanCheckAppUpdate() => !IsAppUpdateBusy;

    [RelayCommand]
    private void RestartForAppUpdate() => _appUpdateService.ApplyAndRestart();
}
