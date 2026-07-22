namespace WikeloContractor.Services;

/// <summary>
/// Replacement images for reward items, merged from two files: the bundled
/// <c>Resources/img-catalog-overrides.json</c> (shipped with the app, maintained in the repo)
/// and the user's <c>%AppData%\WikeloContractor\img-catalog-overrides.json</c>, which wins per
/// key. Covers items the wiki has no image for (e.g. Wikelo-exclusive variants).
/// </summary>
public interface IImageOverrideService
{
    /// <summary>
    /// Returns the custom image (URL or absolute local path) for an item, matched by UUID
    /// first, then by name (case-insensitive). Null when no override is configured.
    /// </summary>
    string? GetOverride(string? itemUuid, string itemName);
}
