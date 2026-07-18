using Velopack;
using Velopack.Sources;

namespace WikeloContractor.Services;

/// <summary>
/// Velopack-backed self-update service. The release feed is this repository's GitHub Releases,
/// which <c>release.yml</c> publishes with <c>vpk upload github</c>. See <see cref="IAppUpdateService"/>.
/// </summary>
public sealed class AppUpdateService : IAppUpdateService
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    private AppUpdateStatus _status = AppUpdateStatus.Idle;

    public AppUpdateService()
    {
        // In a dev run the app is not installed by Velopack; UpdateManager still constructs but
        // IsInstalled is false, so every operation short-circuits. Constructing the GithubSource
        // does no I/O.
        _manager = new UpdateManager(new GithubSource(AppHttp.RepoUrl, null, false));
    }

    public AppUpdateStatus Status => _status;

    public string? AvailableVersion { get; private set; }

    public event EventHandler? StatusChanged;

    public async Task CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled || _status is AppUpdateStatus.Checking or AppUpdateStatus.Downloading)
        {
            return;
        }

        try
        {
            SetStatus(AppUpdateStatus.Checking);

            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                AvailableVersion = null;
                SetStatus(AppUpdateStatus.UpToDate);
                return;
            }

            AvailableVersion = update.TargetFullRelease.Version.ToString();
            SetStatus(AppUpdateStatus.Downloading);

            await _manager.DownloadUpdatesAsync(update);
            _pending = update;
            SetStatus(AppUpdateStatus.ReadyToRestart);
        }
        catch (Exception)
        {
            // Network failure, malformed feed, etc. — surfaced to the user, retried on next check.
            SetStatus(AppUpdateStatus.Failed);
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager.IsInstalled && _status == AppUpdateStatus.ReadyToRestart && _pending is not null)
        {
            _manager.ApplyUpdatesAndRestart(_pending);
        }
    }

    private void SetStatus(AppUpdateStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
