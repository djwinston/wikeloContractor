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

    /// <summary>
    /// Subfolder holding the shared fragments a guide pulls in with <c>{{include: key}}</c>. Its own
    /// files are never guides: the guide scan is <see cref="SearchOption.TopDirectoryOnly"/>, so they
    /// are out of reach by construction rather than by a name filter.
    /// </summary>
    private const string _sharedFolderName = "_shared";

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

        // Fragments load first: a guide cannot be finished without the text it includes. Same
        // two-layer rule as the guides, so a personal fragment overrides the shipped one.
        var fragments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadFragments(Path.Combine(_bundledDirectory, _sharedFolderName), fragments);
        LoadFragments(Path.Combine(_userDirectory, _sharedFolderName), fragments);

        var guides = new Dictionary<string, SourcingGuide>(StringComparer.OrdinalIgnoreCase);

        // Bundled first, then the user's — the second pass overwrites, so personal files win per item.
        LoadDirectory(_bundledDirectory, guides, fragments);
        LoadDirectory(_userDirectory, guides, fragments);

        _guides = guides;
    }

    /// <summary>Path + last-write time of every <c>.md</c> in both folders, cheap to compute (no reads).</summary>
    private string BuildSignature()
    {
        var builder = new System.Text.StringBuilder();

        // The fragment folders count too: editing a shared block must invalidate every guide that
        // includes it, not just the file that was touched.
        foreach (var directory in (string[])
                 [
                     _bundledDirectory,
                     _userDirectory,
                     Path.Combine(_bundledDirectory, _sharedFolderName),
                     Path.Combine(_userDirectory, _sharedFolderName),
                 ])
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

    /// <summary>Reads the shared fragments, keyed by front matter <c>name</c> like the guides are.</summary>
    private static void LoadFragments(string directory, Dictionary<string, string> into)
    {
        foreach (var path in EnumerateMarkdown(directory))
        {
            if (ReadText(path) is not { } text)
            {
                continue;
            }

            var (frontMatter, body) = MarkdownDocument.SplitFrontMatter(text);
            if (frontMatter.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            {
                into[name.Trim()] = body;
            }
        }
    }

    private static void LoadDirectory(
        string directory,
        Dictionary<string, SourcingGuide> into,
        IReadOnlyDictionary<string, string> fragments)
    {
        foreach (var path in EnumerateMarkdown(directory))
        {
            if (TryRead(path, fragments) is not { } entry)
            {
                continue;
            }

            into[entry.Name] = entry.Guide;
        }
    }

    /// <summary>
    /// Every <c>.md</c> directly in a folder, or nothing when it is absent or unreadable. A missing
    /// folder is normal: bundled content only appears after the first build copy, and the user layer
    /// only once the user creates it.
    /// </summary>
    private static IEnumerable<string> EnumerateMarkdown(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>Locked or unreadable files are skipped rather than losing the whole layer.</summary>
    private static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static (string Name, SourcingGuide Guide)? TryRead(string path, IReadOnlyDictionary<string, string> fragments)
    {
        if (ReadText(path) is not { } text)
        {
            return null;
        }

        var (frontMatter, body) = MarkdownDocument.SplitFrontMatter(text);

        // No `name` means the file cannot be attached to an item; a stray README must not become one.
        if (!frontMatter.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        frontMatter.TryGetValue("summary", out var summary);

        // Optional metadata. Absent for anything bought or mined, which is most of the corpus — the
        // page hides each row on its own, so a missing key needs no placeholder value here.
        frontMatter.TryGetValue("contract", out var contract);
        frontMatter.TryGetValue("faction", out var faction);

        // Includes are spliced in first so a fragment's own comments are stripped by the same pass.
        // Stripping comments here means a file that is nothing but authoring hints correctly reports
        // HasBody == false and the page shows its placeholder.
        var content = MarkdownDocument.StripComments(MarkdownDocument.ResolveIncludes(body, fragments)).Trim();

        return (name.Trim(), new SourcingGuide(
            summary?.Trim() ?? string.Empty,
            content,
            contract?.Trim() ?? string.Empty,
            faction?.Trim() ?? string.Empty));
    }
}
