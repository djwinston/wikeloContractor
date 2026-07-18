namespace WikeloContractor.Services;

/// <summary>
/// User-defined replacement images for reward items, read from
/// <c>%AppData%\WikeloContractor\image-overrides.json</c>. Lets the user swap an image
/// they dislike or supply one for items the wiki does not cover.
/// </summary>
public interface IImageOverrideService
{
    /// <summary>
    /// Returns the custom image (URL or absolute local path) for an item, matched by UUID
    /// first, then by name (case-insensitive). Null when no override is configured.
    /// </summary>
    string? GetOverride(string? itemUuid, string itemName);
}
