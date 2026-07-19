using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using WikeloContractor.Services;

namespace WikeloContractor.Views.Helpers;

/// <summary>
/// Shared image loading for the preview attached properties (<see cref="RewardPreview"/>,
/// <see cref="InventoryPreview"/>): resolves the first loadable URL across the candidates through the
/// <see cref="IImageCacheService"/> disk cache and decodes it off-thread at a bounded width. Frozen,
/// shareable results are memoized for the session, except at native resolution (width 0), which is a
/// one-off not worth caching.
/// </summary>
internal static class ThumbnailLoader
{
    /// <summary>Session-lifetime memoization of decoded thumbnails, keyed by decode width + URL.</summary>
    private static readonly ConcurrentDictionary<string, ImageSource> _decoded = new();

    /// <summary>First loadable image across the candidates, decoded at <paramref name="decodePixelWidth"/> (0 = native).</summary>
    public static async Task<ImageSource?> ResolveAsync(IReadOnlyList<string> candidates, int decodePixelWidth)
    {
        var imageCache = App.Services.GetRequiredService<IImageCacheService>();
        var memoize = decodePixelWidth != 0;

        foreach (var candidate in candidates)
        {
            var decodedKey = $"{decodePixelWidth}|{candidate}";
            if (memoize && _decoded.TryGetValue(decodedKey, out var cached))
            {
                return cached;
            }

            var localPath = await imageCache.GetLocalPathAsync(candidate);
            if (localPath is null)
            {
                continue;
            }

            // Decode failures fall through to the next candidate — e.g. a .webp
            // thumbnail on a machine without the WebP codec falls back to the original PNG.
            var decoded = await Task.Run(() => TryDecode(localPath, decodePixelWidth));
            if (decoded is not null)
            {
                if (memoize)
                {
                    _decoded[decodedKey] = decoded;
                }

                return decoded;
            }
        }

        return null;
    }

    private static BitmapImage? TryDecode(string path, int decodePixelWidth)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is NotSupportedException or System.IO.FileFormatException or System.IO.IOException)
        {
            return null;
        }
    }
}
