using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WikeloContractor");

        _ = Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "settings.json");
    }

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
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions) ?? new AppSettings();
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
        await JsonSerializer.SerializeAsync(stream, Current, _jsonOptions);
    }
}
