using System.IO;
using System.Text.Json;

namespace WikeloContractor.Services;

/// <summary>
/// Reads <c>image-overrides.json</c> lazily and re-reads it whenever the file changes on
/// disk, so the user can edit overrides while the app is running (a list refresh picks
/// them up). A template file is created on first access.
/// </summary>
public sealed class ImageOverrideService : IImageOverrideService
{
    private readonly string _filePath;
    private readonly Lock _lock = new();

    /// <summary>How often the file is re-stat'ed at most — lookups run in the render path.</summary>
    private readonly TimeSpan _statInterval;

    private Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _loadedWriteTimeUtc = DateTime.MinValue;
    private DateTime _lastStatAtUtc = DateTime.MinValue;

    public ImageOverrideService()
        : this(Path.Combine(AppStorage.Root, "image-overrides.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the service at a temp file and disable the stat throttle.</summary>
    internal ImageOverrideService(string filePath, TimeSpan? statInterval = null)
    {
        _filePath = filePath;
        _statInterval = statInterval ?? TimeSpan.FromSeconds(1);
    }

    public string? GetOverride(string? itemUuid, string itemName)
    {
        lock (_lock)
        {
            EnsureLoaded();

            if (itemUuid is not null && _overrides.TryGetValue(itemUuid, out var byUuid))
            {
                return byUuid;
            }

            return _overrides.TryGetValue(itemName, out var byName) ? byName : null;
        }
    }

    private void EnsureLoaded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastStatAtUtc < _statInterval)
        {
            return;
        }

        _lastStatAtUtc = now;

        if (!File.Exists(_filePath))
        {
            WriteTemplate();
        }

        var writeTimeUtc = File.GetLastWriteTimeUtc(_filePath);
        if (writeTimeUtc == _loadedWriteTimeUtc)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_filePath));

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

            _overrides = overrides;
            _loadedWriteTimeUtc = writeTimeUtc;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Malformed or locked file — keep the previously loaded overrides.
        }
    }

    private void WriteTemplate()
    {
        const string template = """
        {
          "$comment": "Custom reward images. Key: item UUID or item name (case-insensitive); value: image URL or absolute local file path. Applied on the next catalog refresh / app start.",
          "overrides": {
          }
        }
        """;

        try
        {
            File.WriteAllText(_filePath, template);
        }
        catch (IOException)
        {
            // Read-only location — overrides simply stay unavailable.
        }
    }
}
