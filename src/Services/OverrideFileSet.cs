using System.IO;
using System.Text.Json;

namespace WikeloContractor.Services;

/// <summary>
/// A reusable two-layer key → value override set with hot reload. A bundled file (shipped next to the
/// exe, maintained in the repository) provides shared defaults; a user file in <c>%AppData%</c> layers
/// personal edits on top and wins per key. Both files are re-read lazily whenever they change on disk
/// (throttled, since lookups run in the render path); a template for the user file is written on first
/// access. Shared by <see cref="ImageOverrideService"/> and <see cref="InventoryImageOverrideService"/>.
/// </summary>
internal sealed class OverrideFileSet
{
    /// <summary>One overrides file with its own change tracking.</summary>
    private sealed class OverrideFile(string path)
    {
        public string Path { get; } = path;

        public DateTime LoadedWriteTimeUtc { get; set; } = DateTime.MinValue;

        public Dictionary<string, string> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly OverrideFile _bundled;
    private readonly OverrideFile _user;
    private readonly Lock _lock = new();

    /// <summary>How often the files are re-stat'ed at most — lookups run in the render path.</summary>
    private readonly TimeSpan _statInterval;

    /// <summary>Content written to the user file when it is missing (first run).</summary>
    private readonly string _userTemplate;

    private DateTime _lastStatAtUtc = DateTime.MinValue;

    public OverrideFileSet(string userFilePath, string? bundledFilePath, TimeSpan statInterval, string userTemplate)
    {
        _user = new OverrideFile(userFilePath);
        _bundled = new OverrideFile(bundledFilePath ?? string.Empty);
        _statInterval = statInterval;
        _userTemplate = userTemplate;
    }

    /// <summary>
    /// Looks up an override: <paramref name="primaryKey"/> first (when given), then
    /// <paramref name="secondaryKey"/>; within each, the user's file beats the bundled one.
    /// </summary>
    public string? Get(string? primaryKey, string secondaryKey)
    {
        lock (_lock)
        {
            EnsureLoaded();

            if (primaryKey is not null && Lookup(primaryKey) is { } byPrimary)
            {
                return byPrimary;
            }

            return Lookup(secondaryKey);
        }
    }

    private string? Lookup(string key) =>
        _user.Entries.TryGetValue(key, out var user) ? user
        : _bundled.Entries.TryGetValue(key, out var bundled) ? bundled
        : null;

    private void EnsureLoaded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastStatAtUtc < _statInterval)
        {
            return;
        }

        _lastStatAtUtc = now;

        if (!File.Exists(_user.Path))
        {
            WriteUserTemplate();
        }

        // The bundled file gets no template: its absence (e.g. dev run before first build copy)
        // simply means no shipped defaults.
        Reload(_user);
        Reload(_bundled);
    }

    private static void Reload(OverrideFile file)
    {
        if (string.IsNullOrEmpty(file.Path) || !File.Exists(file.Path))
        {
            return;
        }

        var writeTimeUtc = File.GetLastWriteTimeUtc(file.Path);
        if (writeTimeUtc == file.LoadedWriteTimeUtc)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file.Path));

            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.TryGetProperty("overrides", out var map) && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in map.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.String
                        && entry.Value.GetString() is { Length: > 0 } value)
                    {
                        overrides[entry.Name] = value;
                    }
                }
            }

            file.Entries = overrides;
            file.LoadedWriteTimeUtc = writeTimeUtc;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Malformed or locked file — keep the previously loaded overrides.
        }
    }

    private void WriteUserTemplate()
    {
        try
        {
            File.WriteAllText(_user.Path, _userTemplate);
        }
        catch (IOException)
        {
            // Read-only location — personal overrides simply stay unavailable.
        }
    }
}
