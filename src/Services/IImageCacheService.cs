namespace WikeloContractor.Services;

/// <summary>Disk cache for reward preview images downloaded from external CDNs.</summary>
public interface IImageCacheService
{
    /// <summary>
    /// Resolves an image reference to a local file path. This is the one place that defines the
    /// accepted forms, and every override value passes through it:
    /// <list type="bullet">
    /// <item>an <c>http(s)</c> URL — downloaded on first use, then served from the disk cache;</item>
    /// <item>an absolute local path — used as is (a personal <c>%AppData%</c> override);</item>
    /// <item>a relative path — resolved against the install dir (an image bundled with the app,
    /// e.g. <c>Resources/img/catalog/foo.webp</c>).</item>
    /// </list>
    /// A path is returned only when the file exists; null when the image cannot be obtained.
    /// </summary>
    Task<string?> GetLocalPathAsync(string reference, CancellationToken cancellationToken = default);
}
