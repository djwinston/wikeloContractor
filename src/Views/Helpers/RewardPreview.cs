using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.Views.Helpers;

/// <summary>
/// Attached property that resolves and loads a contract's reward preview into an
/// <see cref="Image"/> asynchronously: custom override → first reward image (thumbnail,
/// then original as a decode fallback) → nothing (the category placeholder icon stays).
/// Files come from the <see cref="IImageCacheService"/> disk cache.
/// </summary>
public static class RewardPreview
{
    /// <summary>Decode width for list thumbnails (64 px box, leaves headroom for DPI scaling).</summary>
    private const int _listDecodePixelWidth = 128;

    /// <summary>Decode width for the detail page image (260 px box + DPI headroom).</summary>
    private const int _detailDecodePixelWidth = 640;

    /// <summary>Session-lifetime memoization of decoded thumbnails (frozen, shareable).</summary>
    private static readonly ConcurrentDictionary<string, ImageSource> _decoded = new();

    /// <summary>
    /// Final result per candidate-URL list, including failures (null). Filter refreshes
    /// regenerate every card container, so this is the synchronous fast path that avoids
    /// re-running downloads, disk checks and decodes on each keystroke. The key includes
    /// the override URL, so editing image-overrides.json still takes effect on refresh.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ImageSource?> _resolved = new();

    public static readonly DependencyProperty ContractProperty = DependencyProperty.RegisterAttached(
        "Contract",
        typeof(WikeloContract),
        typeof(RewardPreview),
        new PropertyMetadata(null, OnContractChanged));

    /// <summary>Per-reward variant used by the contract detail view.</summary>
    public static readonly DependencyProperty RewardProperty = DependencyProperty.RegisterAttached(
        "Reward",
        typeof(ContractReward),
        typeof(RewardPreview),
        new PropertyMetadata(null, OnRewardChanged));

    public static WikeloContract? GetContract(DependencyObject obj) => (WikeloContract?)obj.GetValue(ContractProperty);

    public static void SetContract(DependencyObject obj, WikeloContract? value) => obj.SetValue(ContractProperty, value);

    public static ContractReward? GetReward(DependencyObject obj) => (ContractReward?)obj.GetValue(RewardProperty);

    public static void SetReward(DependencyObject obj, ContractReward? value) => obj.SetValue(RewardProperty, value);

    private static void OnContractChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, ContractProperty, e.NewValue, (e.NewValue as WikeloContract)?.Rewards ?? [], _listDecodePixelWidth);

    private static void OnRewardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, RewardProperty, e.NewValue, e.NewValue is ContractReward reward ? [reward] : [], _detailDecodePixelWidth);

    private static async void LoadInto(
        DependencyObject d,
        DependencyProperty property,
        object? value,
        IReadOnlyList<ContractReward> rewards,
        int decodePixelWidth)
    {
        if (d is not Image image)
        {
            return;
        }

        var candidates = CandidateUrls(rewards).ToList();
        if (value is null || candidates.Count == 0)
        {
            image.Source = null;
            return;
        }

        // Decode width is part of both memo keys: the same URL is cached separately
        // for the 64 px list thumbnail and the large detail image.
        var memoKey = $"{decodePixelWidth}|" + string.Join("\n", candidates);
        if (_resolved.TryGetValue(memoKey, out var memoized))
        {
            image.Source = memoized;
            return;
        }

        image.Source = null;

        ImageSource? source;
        try
        {
            source = await ResolveAsync(candidates, decodePixelWidth);
        }
        catch (Exception)
        {
            // Preview is best-effort decoration; the placeholder icon simply stays.
            return;
        }

        _resolved[memoKey] = source;

        // The template may have been rebound (filtering, refresh) while we were loading.
        if (ReferenceEquals(image.GetValue(property), value))
        {
            image.Source = source;
        }
    }

    /// <summary>First loadable image across the given candidates.</summary>
    private static async Task<ImageSource?> ResolveAsync(IReadOnlyList<string> candidates, int decodePixelWidth)
    {
        var imageCache = App.Services.GetRequiredService<IImageCacheService>();

        foreach (var candidate in candidates)
        {
            var decodedKey = $"{decodePixelWidth}|{candidate}";
            if (_decoded.TryGetValue(decodedKey, out var cached))
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
                _decoded[decodedKey] = decoded;
                return decoded;
            }
        }

        return null;
    }

    /// <summary>Candidate URLs across the rewards, override first, in stable order.</summary>
    private static IEnumerable<string> CandidateUrls(IReadOnlyList<ContractReward> rewards)
    {
        var overrides = App.Services.GetRequiredService<IImageOverrideService>();

        return rewards.SelectMany(Candidates).Distinct();

        IEnumerable<string> Candidates(ContractReward reward)
        {
            if (overrides.GetOverride(reward.ItemUuid, reward.Name) is { } custom)
            {
                yield return custom;
            }

            foreach (var image in reward.Images)
            {
                if (image.ThumbnailUrl is not null)
                {
                    yield return image.ThumbnailUrl;
                }

                if (image.OriginalUrl is not null && image.OriginalUrl != image.ThumbnailUrl)
                {
                    yield return image.OriginalUrl;
                }
            }
        }
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
