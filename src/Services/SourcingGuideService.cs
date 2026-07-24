using System.IO;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <inheritdoc cref="ISourcingGuideService" />
/// <remarks>
/// Two-layer like <see cref="InventoryImageOverrideService"/>, but resolved <em>per file</em> rather
/// than per key, so it scans directories instead of reusing <see cref="OverrideFileSet"/> — that one
/// is a key→value JSON engine and bending it to walk a folder would serve neither case well.
/// <para>
/// The lookup key is the front matter's <c>name</c>, not the file name, so renaming a file never
/// orphans an entry. Files are read once and cached; the set only changes on an app update (bundled)
/// or a manual edit (user layer), and the directory is re-stat'ed on a throttle to pick those up.
/// </para>
/// </remarks>
public sealed class SourcingGuideService : ISourcingGuideService
{
    /// <summary>Folder name used in both the install directory and <c>%AppData%</c>.</summary>
    private const string _folderName = "sourcing";

    private readonly string _userDirectory;
    private readonly string _bundledDirectory;
    private readonly TimeSpan _statInterval;
    private readonly Lock _lock = new();

    private Dictionary<string, SourcingGuide> _guides = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastStatAtUtc = DateTime.MinValue;
    private bool _loaded;

    /// <summary>
    /// Cheap fingerprint of both folders (each file's path + last-write time). Reparse — the
    /// expensive <see cref="File.ReadAllText"/> over ~95 files — happens only when this changes, so
    /// the throttle gates a directory stat, not a full re-read of the corpus.
    /// </summary>
    private string _signature = string.Empty;

    public SourcingGuideService()
        : this(
            Path.Combine(AppStorage.Root, _folderName),
            Path.Combine(AppContext.BaseDirectory, "Resources", _folderName))
    {
    }

    /// <summary>Test seam: lets unit tests point the service at temp folders and disable the throttle.</summary>
    internal SourcingGuideService(string userDirectory, string bundledDirectory, TimeSpan? statInterval = null)
    {
        _userDirectory = userDirectory;
        _bundledDirectory = bundledDirectory;
        _statInterval = statInterval ?? TimeSpan.FromSeconds(5);
    }

    public SourcingGuide? GetGuide(string itemName)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _guides.GetValueOrDefault(itemName);
        }
    }

    private void EnsureLoaded()
    {
        var now = DateTime.UtcNow;
        if (_loaded && now - _lastStatAtUtc < _statInterval)
        {
            return;
        }

        _lastStatAtUtc = now;

        // Stat both folders (cheap) and reparse only when a file was added, removed or touched.
        // Bundled content changes on an app update, the user layer rarely; most calls skip the read.
        var signature = BuildSignature();
        if (_loaded && signature == _signature)
        {
            return;
        }

        _loaded = true;
        _signature = signature;

        var guides = new Dictionary<string, SourcingGuide>(StringComparer.OrdinalIgnoreCase);

        // Bundled first, then the user's — the second pass overwrites, so personal files win per item.
        LoadDirectory(_bundledDirectory, guides);
        LoadDirectory(_userDirectory, guides);

        _guides = guides;
    }

    /// <summary>Path + last-write time of every <c>.md</c> in both folders, cheap to compute (no reads).</summary>
    private string BuildSignature()
    {
        var builder = new System.Text.StringBuilder();

        foreach (var directory in (string[])[_bundledDirectory, _userDirectory])
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var path in files)
            {
                _ = builder.Append(path).Append('|').Append(File.GetLastWriteTimeUtc(path).Ticks).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static void LoadDirectory(string directory, Dictionary<string, SourcingGuide> into)
    {
        if (!Directory.Exists(directory))
        {
            // No bundled folder in a dev run before the first build copy, and no user folder until
            // the user makes one — both simply mean "no entries from this layer".
            return;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return;
        }

        foreach (var path in files)
        {
            if (TryRead(path) is not { } entry)
            {
                continue;
            }

            into[entry.Name] = entry.Guide;
        }
    }

    private static (string Name, SourcingGuide Guide)? TryRead(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException)
        {
            // Locked or unreadable — skip this one rather than losing the whole layer.
            return null;
        }

        var (frontMatter, body) = MarkdownDocument.SplitFrontMatter(text);

        // No `name` means the file cannot be attached to an item; a stray README must not become one.
        if (!frontMatter.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        frontMatter.TryGetValue("summary", out var summary);

        // Comments carry the skeleton's authoring hints; stripping them here means a file that is
        // nothing but hints correctly reports HasBody == false and the page shows its placeholder.
        var content = MarkdownDocument.StripComments(body).Trim();

        return (name.Trim(), new SourcingGuide(summary?.Trim() ?? string.Empty, content));
    }
}
