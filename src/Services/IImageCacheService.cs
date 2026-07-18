namespace WikeloContractor.Services;

/// <summary>Disk cache for reward preview images downloaded from external CDNs.</summary>
public interface IImageCacheService
{
    /// <summary>
    /// Returns a local file path for the image at <paramref name="url"/>, downloading it on
    /// first use. Absolute local paths (custom overrides) are passed through when the file
    /// exists. Returns null when the image cannot be obtained.
    /// </summary>
    Task<string?> GetLocalPathAsync(string url, CancellationToken cancellationToken = default);
}
