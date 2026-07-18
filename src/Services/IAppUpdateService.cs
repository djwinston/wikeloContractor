namespace WikeloContractor.Services;

/// <summary>Lifecycle of an application self-update via Velopack.</summary>
public enum AppUpdateStatus
{
    /// <summary>No check has run yet, or the app is not an installed build (dev run).</summary>
    Idle,
    Checking,
    UpToDate,
    Downloading,

    /// <summary>An update is downloaded and will be applied on the next restart.</summary>
    ReadyToRestart,
    Failed,
}

/// <summary>
/// Wraps Velopack's <c>UpdateManager</c> for in-app self-updates. Only meaningful in an installed
/// build; in a dev run (<c>dotnet run</c>) it stays <see cref="AppUpdateStatus.Idle"/> and every
/// call is a no-op, so debugging is unaffected.
/// </summary>
public interface IAppUpdateService
{
    AppUpdateStatus Status { get; }

    /// <summary>Version string of the pending update once one is found; otherwise null.</summary>
    string? AvailableVersion { get; }

    /// <summary>Raised when <see cref="Status"/> changes. May fire on a background thread.</summary>
    event EventHandler? StatusChanged;

    /// <summary>Checks the release feed and, if an update exists, downloads it (→ ReadyToRestart).</summary>
    Task CheckAndDownloadAsync();

    /// <summary>Relaunches into the downloaded update. No-op unless <see cref="Status"/> is ReadyToRestart.</summary>
    void ApplyAndRestart();
}
