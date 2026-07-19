using System.IO;

namespace WikeloContractor.Services;

/// <summary>
/// Two-layer reward image overrides. The bundled file (<c>Resources/image-overrides.json</c>
/// next to the exe, maintained in the repository) ships shared defaults to every user; the
/// user's file in <c>%AppData%</c> layers personal edits on top and wins per key. Both files
/// are re-read lazily whenever they change on disk, so edits apply while the app is running
/// (a list refresh picks them up). A template for the user file is created on first access.
/// The two-layer + hot-reload mechanics live in <see cref="OverrideFileSet"/>.
/// </summary>
public sealed class ImageOverrideService : IImageOverrideService
{
    private const string _userTemplate = """
        {
          "$comment": "Personal reward images layered over the app's shipped defaults (these win per key). Key: item UUID or item name (case-insensitive); value: image URL or absolute local file path. Applied on the next catalog refresh / app start.",
          "overrides": {
          }
        }
        """;

    private readonly OverrideFileSet _files;

    public ImageOverrideService()
        : this(
            Path.Combine(AppStorage.Root, "image-overrides.json"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "image-overrides.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the service at temp files and disable the stat throttle.</summary>
    internal ImageOverrideService(string userFilePath, string? bundledFilePath = null, TimeSpan? statInterval = null) =>
        _files = new OverrideFileSet(
            userFilePath,
            bundledFilePath ?? Path.Combine(AppContext.BaseDirectory, "Resources", "image-overrides.json"),
            statInterval ?? TimeSpan.FromSeconds(1),
            _userTemplate);

    // UUID beats name; within each, the user's file beats the bundled one.
    public string? GetOverride(string? itemUuid, string itemName) => _files.Get(itemUuid, itemName);
}
