using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WikeloContractor.Services;

/// <summary>
/// Shared locations and serialization defaults for the app's data under
/// <c>%AppData%\WikeloContractor</c>. Both <see cref="SettingsService"/> and
/// <see cref="ContractCatalogService"/> persist through here so the folder name and
/// JSON conventions live in one place.
/// </summary>
internal static class AppStorage
{
    /// <summary>Root data directory (<c>%AppData%\WikeloContractor</c>), created on first access.</summary>
    public static string Root { get; } = EnsureCreated(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WikeloContractor"));

    /// <summary>Shared options for every persisted JSON file: indented, enums as strings.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Returns a subdirectory under <see cref="Root"/>, creating it if needed.</summary>
    public static string GetDirectory(string name) => EnsureCreated(Path.Combine(Root, name));

    private static string EnsureCreated(string path)
    {
        _ = Directory.CreateDirectory(path);
        return path;
    }
}
