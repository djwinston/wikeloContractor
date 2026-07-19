using System.IO;

namespace WikeloContractor.Services;

/// <summary>
/// Two-layer inventory image config, mirroring <see cref="ImageOverrideService"/>. The bundled file
/// (<c>Resources/inventory-image-overrides.json</c> next to the exe) ships shared defaults; the user's
/// file in <c>%AppData%</c> wins per key. Keys are item names (case-insensitive); values are image
/// URLs or absolute local file paths. Both files hot-reload via <see cref="OverrideFileSet"/>.
/// </summary>
public sealed class InventoryImageOverrideService : IInventoryImageOverrideService
{
    private const string _userTemplate = """
        {
          "$comment": "Personal inventory item images layered over the app's shipped defaults (these win per key). Key: item name (case-insensitive); value: image URL or absolute local file path. Applied on the next inventory open / app start.",
          "overrides": {
          }
        }
        """;

    private readonly OverrideFileSet _files;

    public InventoryImageOverrideService()
        : this(
            Path.Combine(AppStorage.Root, "inventory-image-overrides.json"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "inventory-image-overrides.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the service at temp files and disable the stat throttle.</summary>
    internal InventoryImageOverrideService(string userFilePath, string? bundledFilePath = null, TimeSpan? statInterval = null) =>
        _files = new OverrideFileSet(
            userFilePath,
            bundledFilePath ?? Path.Combine(AppContext.BaseDirectory, "Resources", "inventory-image-overrides.json"),
            statInterval ?? TimeSpan.FromSeconds(1),
            _userTemplate);

    public string? GetOverride(string itemName) => _files.Get(null, itemName);
}
