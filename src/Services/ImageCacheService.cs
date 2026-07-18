using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace WikeloContractor.Services;

/// <summary>
/// Download-once disk cache under <c>%AppData%\WikeloContractor\cache\images\</c>.
/// Images come from external CDNs (cstone.space, media.starcitizen.tools), which are
/// separate from the API's rate limit — downloads only need basic politeness (small
/// concurrency cap), never the catalog's 429 gate.
/// </summary>
public sealed class ImageCacheService : IImageCacheService
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    /// <summary>Deduplicates concurrent downloads of the same URL.</summary>
    private readonly ConcurrentDictionary<string, Task<string?>> _inFlight = new();

    /// <summary>Politeness cap: at most this many parallel downloads per CDN session.</summary>
    private readonly SemaphoreSlim _downloadSlots = new(4, 4);

    public ImageCacheService(HttpClient httpClient)
        : this(httpClient, AppStorage.GetDirectory(Path.Combine("cache", "images")))
    {
    }

    /// <summary>Test seam: lets unit tests redirect the cache to a temp directory.</summary>
    internal ImageCacheService(HttpClient httpClient, string cacheDirectory)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppHttp.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        _ = Directory.CreateDirectory(cacheDirectory);
        _cacheDirectory = cacheDirectory;
    }

    public async Task<string?> GetLocalPathAsync(string url, CancellationToken cancellationToken = default)
    {
        // Custom overrides may point at a local file instead of a URL — pass it through.
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(url) ? url : null;
        }

        var filePath = Path.Combine(_cacheDirectory, FileNameFor(url));
        if (File.Exists(filePath))
        {
            return filePath;
        }

        var download = _inFlight.GetOrAdd(url, _ => DownloadAsync(url, filePath, cancellationToken));
        try
        {
            return await download;
        }
        finally
        {
            // Failures are not cached: the next request retries the download.
            _ = _inFlight.TryRemove(url, out _);
        }
    }

    private async Task<string?> DownloadAsync(string url, string filePath, CancellationToken cancellationToken)
    {
        await _downloadSlots.WaitAsync(cancellationToken);
        var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using (var stream = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(stream, cancellationToken);
            }

            File.Move(tempPath, filePath, overwrite: true);
            return filePath;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // Leftover temp file is harmless; overwritten by the next attempt.
                }
            }

            _ = _downloadSlots.Release();
        }
    }

    /// <summary>Stable cache file name: SHA-256 of the URL plus its original extension.</summary>
    private static string FileNameFor(string url)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)));

        var extension = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Path.GetExtension(uri.AbsolutePath)
            : string.Empty;

        // Keep only plausible extensions (".png", ".webp", ...) so hostile URLs cannot inject paths.
        if (extension.Length is < 2 or > 6 || extension.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c)))
        {
            extension = ".img";
        }

        return hash + extension.ToLowerInvariant();
    }
}
