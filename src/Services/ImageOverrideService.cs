using System.IO;
using System.Text.Json;

namespace WikeloContractor.Services;

/// <summary>
/// Two-layer reward image overrides. The bundled file (<c>Resources/image-overrides.json</c>
/// next to the exe, maintained in the repository) ships shared defaults to every user; the
/// user's file in <c>%AppData%</c> layers personal edits on top and wins per key. Both files
/// are re-read lazily whenever they change on disk, so edits apply while the app is running
/// (a list refresh picks them up). A template for the user file is created on first access.
/// </summary>
public sealed class ImageOverrideService : IImageOverrideService
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

    private DateTime _lastStatAtUtc = DateTime.MinValue;

    public ImageOverrideService()
        : this(
            Path.Combine(AppStorage.Root, "image-overrides.json"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "image-overrides.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the service at temp files and disable the stat throttle.</summary>
    internal ImageOverrideService(string userFilePath, string? bundledFilePath = null, TimeSpan? statInterval = null)
    {
        _user = new OverrideFile(userFilePath);
        _bundled = new OverrideFile(bundledFilePath ?? Path.Combine(AppContext.BaseDirectory, "Resources", "image-overrides.json"));
        _statInterval = statInterval ?? TimeSpan.FromSeconds(1);
    }

    public string? GetOverride(string? itemUuid, string itemName)
    {
        lock (_lock)
        {
            EnsureLoaded();

            // UUID beats name; within each, the user's file beats the bundled one.
            if (itemUuid is not null && (Lookup(itemUuid) is { } byUuid))
            {
                return byUuid;
            }

            return Lookup(itemName);
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
        if (!File.Exists(file.Path))
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
        const string template = """
        {
          "$comment": "Personal reward images layered over the app's shipped defaults (these win per key). Key: item UUID or item name (case-insensitive); value: image URL or absolute local file path. Applied on the next catalog refresh / app start.",
          "overrides": {
          }
        }
        """;

        try
        {
            File.WriteAllText(_user.Path, template);
        }
        catch (IOException)
        {
            // Read-only location — personal overrides simply stay unavailable.
        }
    }
}
