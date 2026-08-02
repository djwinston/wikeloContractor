using System.IO;
using System.Text;

namespace WikeloContractor.Services;

/// <summary>
/// The app's file log. Deliberately dependency-free and static: <c>VelopackApp.Build().Run()</c> is
/// the first line of startup, long before the host exists, and the update hooks it handles are
/// exactly what needs recording.
/// <para>
/// The file lands **next to <c>Update.exe</c>**, in the install root — not next to the executable.
/// The exe lives in <c>current\</c>, which Velopack replaces wholesale on every update ("Backing up
/// current dir… Replacing current dir with…"), so a log written there is destroyed at the one moment
/// it is needed: right after an update went wrong.
/// </para>
/// </summary>
internal static class AppLog
{
    private const string _fileName = "WikeloContractor.log";

    /// <summary>Copy of the updater's own log; see <see cref="MirrorUpdaterLog"/>.</summary>
    private const string _updaterMirrorName = "velopack-updater.log";

    /// <summary>Rotate at 1 MB, keeping a single previous file — enough to span a few sessions.</summary>
    private const long _maxBytes = 1024 * 1024;

    private static readonly Lock _lock = new();

    /// <summary>Folder holding the log files; surfaced by the About page's "Open logs folder".</summary>
    public static string Directory { get; } = ResolveDirectory(AppContext.BaseDirectory, File.Exists);

    public static string FilePath { get; } = Path.Combine(Directory, _fileName);

    /// <summary>
    /// True when running from a Velopack install rather than a dev build — i.e. the log sits above
    /// the binaries. Guards the work that only makes sense for a real install.
    /// </summary>
    private static bool IsInstallLayout =>
        !string.Equals(
            Directory.TrimEnd(Path.DirectorySeparatorChar),
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Picks the install root when the app is running from a Velopack layout, and the app's own
    /// folder otherwise (a dev run, where there is nothing above to write to).
    /// <para>
    /// Pure apart from the injected probe, so the rule is unit-testable without a real install.
    /// The layout is recognised structurally — a <c>current</c> folder whose parent holds
    /// <c>Update.exe</c> — rather than by asking Velopack, because this runs before Velopack starts.
    /// </para>
    /// </summary>
    internal static string ResolveDirectory(string baseDirectory, Func<string, bool> fileExists)
    {
        try
        {
            var appDir = new DirectoryInfo(baseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (appDir.Name.Equals("current", StringComparison.OrdinalIgnoreCase)
                && appDir.Parent is { } root
                && fileExists(Path.Combine(root.FullName, "Update.exe")))
            {
                return root.FullName;
            }
        }
        catch (Exception)
        {
            // A malformed path must not stop the app from starting; fall through to the app folder.
        }

        return baseDirectory;
    }

    public static void Write(string level, string message, Exception? exception = null)
    {
        var line = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("  [").Append(level).Append("]  ")
            .Append(message);

        if (exception is not null)
        {
            _ = line.Append(Environment.NewLine).Append(exception);
        }

        Append(line.ToString());
    }

    private static void Append(string line)
    {
        lock (_lock)
        {
            try
            {
                Rotate();
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                // A read-only install dir, a locked file, a full disk — logging must never be the
                // reason the app misbehaves. Losing a line is strictly better than throwing.
            }
        }
    }

    private static void Rotate()
    {
        var file = new FileInfo(FilePath);
        if (!file.Exists || file.Length < _maxBytes)
        {
            return;
        }

        var previous = Path.ChangeExtension(FilePath, ".1.log");
        File.Delete(previous);
        File.Move(FilePath, previous);
    }

    /// <summary>
    /// Copies the updater's log next to ours. <c>Update.exe</c> is a separate native binary that
    /// always writes to <c>%LocalAppData%\velopack\</c> — <c>ApplyUpdatesAndRestart</c> builds its
    /// command line without <c>--log</c>, so the path cannot be redirected. Mirroring it means one
    /// findable folder holds the whole story instead of half of it sitting in a hidden profile
    /// directory, which is what made one update failure take an hour to diagnose.
    /// </summary>
    public static void MirrorUpdaterLog()
    {
        // A dev run has no updater history worth copying, and the file is ~130 KB — not something
        // to duplicate into bin\ on every F5.
        if (!IsInstallLayout)
        {
            return;
        }

        try
        {
            var source = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "velopack",
                "velopack_WikeloContractor.log");

            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(Directory, _updaterMirrorName), overwrite: true);
            }
        }
        catch (Exception)
        {
            // Best effort — the original is still on disk, only the convenience copy is missing.
        }
    }
}
