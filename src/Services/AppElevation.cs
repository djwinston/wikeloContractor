using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace WikeloContractor.Services;

/// <summary>
/// Whether the app runs elevated, and how to relaunch it that way.
/// <para>
/// This exists for one reason: <b>Windows UIPI does not deliver a global hotkey while a
/// higher-integrity window is in the foreground.</b> Star Citizen ships with Easy Anti-Cheat and is
/// commonly launched elevated, so an unelevated companion app registers its hotkeys successfully,
/// receives them on the desktop, and receives nothing at all once the player alt-tabs into the game —
/// with no error anywhere, because the registration really did succeed.
/// </para>
/// <para>
/// The alternative that would work unelevated is a low-level keyboard hook, which is exactly the
/// technique anti-cheat exists to stop. Asking the user to elevate is the honest trade.
/// </para>
/// </summary>
internal static class AppElevation
{
    /// <summary>True when the process runs with administrator rights.</summary>
    public static bool IsElevated { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception)
        {
            // Never worth failing startup over; assume the safer "not elevated" and show the hint.
            AppLog.Write("Warn", "Could not determine the process elevation level.", exception);
            return false;
        }
    }

    /// <summary>
    /// Relaunches the app through the UAC prompt. Returns false when the user declines it, so the
    /// caller can leave the current instance running instead of exiting into nothing.
    /// </summary>
    public static bool TryRestartElevated()
    {
        if (Environment.ProcessPath is not { } path)
        {
            return false;
        }

        try
        {
            // UseShellExecute is required for the "runas" verb — this is the UAC prompt, and it is
            // the user's own confirmation, so no dialog of ours precedes it.
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas",
            });

            AppLog.Write("Information", "Relaunching elevated at the user's request.");
            return true;
        }
        catch (Win32Exception)
        {
            // The user dismissed the UAC prompt.
            return false;
        }
        catch (Exception exception)
        {
            AppLog.Write("Error", "Failed to relaunch elevated.", exception);
            return false;
        }
    }
}
