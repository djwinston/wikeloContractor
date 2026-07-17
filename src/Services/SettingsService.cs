using System.IO;
using System.Text.Json;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath = Path.Combine(AppStorage.Root, "settings.json");

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
    }

    public async Task SaveAsync()
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, Current, AppStorage.JsonOptions);
    }
}
