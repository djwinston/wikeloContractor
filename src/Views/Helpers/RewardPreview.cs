using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Media;
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

    /// <summary>Native resolution for the full-window preview (0 = no downscale on decode).</summary>
    private const int _previewDecodePixelWidth = 0;

    /// <summary>
    /// Final result per candidate-URL list, including failures (null). Filter refreshes
    /// regenerate every card container, so this is the synchronous fast path that avoids
    /// re-running downloads, disk checks and decodes on each keystroke. The key includes
    /// the override URL, so editing img-catalog-overrides.json still takes effect on refresh.
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

    /// <summary>Full-resolution variant for the detail page's full-window image preview.</summary>
    public static readonly DependencyProperty PreviewRewardProperty = DependencyProperty.RegisterAttached(
        "PreviewReward",
        typeof(ContractReward),
        typeof(RewardPreview),
        new PropertyMetadata(null, OnPreviewRewardChanged));

    public static WikeloContract? GetContract(DependencyObject obj) => (WikeloContract?)obj.GetValue(ContractProperty);

    public static void SetContract(DependencyObject obj, WikeloContract? value) => obj.SetValue(ContractProperty, value);

    public static ContractReward? GetReward(DependencyObject obj) => (ContractReward?)obj.GetValue(RewardProperty);

    public static void SetReward(DependencyObject obj, ContractReward? value) => obj.SetValue(RewardProperty, value);

    public static ContractReward? GetPreviewReward(DependencyObject obj) => (ContractReward?)obj.GetValue(PreviewRewardProperty);

    public static void SetPreviewReward(DependencyObject obj, ContractReward? value) => obj.SetValue(PreviewRewardProperty, value);

    private static void OnContractChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, ContractProperty, e.NewValue, (e.NewValue as WikeloContract)?.Rewards ?? [], _listDecodePixelWidth);

    private static void OnRewardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, RewardProperty, e.NewValue, e.NewValue is ContractReward reward ? [reward] : [], _detailDecodePixelWidth);

    private static void OnPreviewRewardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, PreviewRewardProperty, e.NewValue, e.NewValue is ContractReward reward ? [reward] : [], _previewDecodePixelWidth);

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

        // The full-window preview decodes at native resolution (width 0); caching those multi-MB
        // bitmaps for the whole session is not worth it (one preview on screen at a time), so it is
        // resolved fresh each time. The bounded thumbnail/detail variants are memoized — decode width
        // is part of the key, so the same URL is cached separately per size.
        var memoize = decodePixelWidth != 0;
        var memoKey = $"{decodePixelWidth}|" + string.Join("\n", candidates);
        if (memoize && _resolved.TryGetValue(memoKey, out var memoized))
        {
            image.Source = memoized;
            return;
        }

        image.Source = null;

        ImageSource? source;
        try
        {
            source = await ThumbnailLoader.ResolveAsync(candidates, decodePixelWidth);
        }
        catch (Exception)
        {
            // Preview is best-effort decoration; the placeholder icon simply stays.
            return;
        }

        if (memoize)
        {
            _resolved[memoKey] = source;
        }

        // The template may have been rebound (filtering, refresh) while we were loading.
        if (ReferenceEquals(image.GetValue(property), value))
        {
            image.Source = source;
        }
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
}
