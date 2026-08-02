using System.IO;
using System.Text.Json;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath;

    public SettingsService()
        : this(Path.Combine(AppStorage.Root, "settings.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the settings at a temp file.</summary>
    internal SettingsService(string filePath) => _filePath = filePath;

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, AppStorage.JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupted file — start with defaults; the file will be overwritten on save
            Current = new AppSettings();
        }

        // A property initializer only covers a *missing* member. An explicit `"Overlay": null` in a
        // hand-edited file would deserialize as null and NullReference the overlay on startup.
        Current.Overlay ??= new OverlaySettings();
    }

    public async Task SaveAsync()
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, Current, AppStorage.JsonOptions);
    }
}
