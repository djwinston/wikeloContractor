namespace WikeloContractor.Services;

/// <summary>
/// Replacement images for reward items, merged from two files: the bundled
/// <c>Resources/img-catalog-overrides.json</c> (shipped with the app, maintained in the repo)
/// and the user's <c>%AppData%\WikeloContractor\img-catalog-overrides.json</c>, which wins per
/// key. Covers items the wiki has no image for (e.g. Wikelo-exclusive variants).
/// </summary>
public interface ICatalogImageOverrideService
{
    /// <summary>
    /// Returns the custom image reference for an item, matched by UUID first, then by name
    /// (case-insensitive); null when no override is configured. See
    /// <see cref="IImageCacheService.GetLocalPathAsync"/> for the accepted forms.
    /// </summary>
    string? GetOverride(string? itemUuid, string itemName);
}
