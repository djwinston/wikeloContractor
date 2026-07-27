namespace WikeloContractor.Services;

/// <summary>
/// Supplies inventory item images. The API provides none for required items, so every image comes
/// from a user-editable config (bundled defaults + a <c>%AppData%</c> layer that wins per key),
/// keyed by item name (case-insensitive).
/// </summary>
public interface IInventoryImageOverrideService
{
    /// <summary>
    /// The configured image reference for an item name, or null when none is set. See
    /// <see cref="IImageCacheService.GetLocalPathAsync"/> for the accepted forms.
    /// </summary>
    string? GetOverride(string itemName);
}
