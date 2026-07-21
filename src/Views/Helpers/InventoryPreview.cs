using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using WikeloContractor.Services;

namespace WikeloContractor.Views.Helpers;

/// <summary>
/// Attached property that loads an inventory item's image into an <see cref="Image"/> asynchronously.
/// Unlike reward previews, requirements have no API images — the only source is the user-editable
/// override config (<see cref="IInventoryImageOverrideService"/>). When none is configured the
/// <see cref="Image.Source"/> stays null and the category placeholder icon shows.
/// </summary>
public static class InventoryPreview
{
    /// <summary>Decode width for list thumbnails (64 px box, leaves headroom for DPI scaling).</summary>
    private const int _decodePixelWidth = 128;

    /// <summary>Native resolution for the full-window preview (0 = no downscale on decode).</summary>
    private const int _previewDecodePixelWidth = 0;

    /// <summary>Session-lifetime memoization of decoded thumbnails (frozen, shareable).</summary>
    private static readonly ConcurrentDictionary<string, ImageSource?> _resolved = new();

    public static readonly DependencyProperty ItemNameProperty = DependencyProperty.RegisterAttached(
        "ItemName",
        typeof(string),
        typeof(InventoryPreview),
        new PropertyMetadata(null, OnItemNameChanged));

    /// <summary>Full-resolution variant for the inventory's full-window image preview.</summary>
    public static readonly DependencyProperty PreviewItemNameProperty = DependencyProperty.RegisterAttached(
        "PreviewItemName",
        typeof(string),
        typeof(InventoryPreview),
        new PropertyMetadata(null, OnPreviewItemNameChanged));

    public static string? GetItemName(DependencyObject obj) => (string?)obj.GetValue(ItemNameProperty);

    public static void SetItemName(DependencyObject obj, string? value) => obj.SetValue(ItemNameProperty, value);

    public static string? GetPreviewItemName(DependencyObject obj) => (string?)obj.GetValue(PreviewItemNameProperty);

    public static void SetPreviewItemName(DependencyObject obj, string? value) => obj.SetValue(PreviewItemNameProperty, value);

    private static void OnItemNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, ItemNameProperty, e.NewValue, _decodePixelWidth);

    private static void OnPreviewItemNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        LoadInto(d, PreviewItemNameProperty, e.NewValue, _previewDecodePixelWidth);

    private static async void LoadInto(
        DependencyObject d,
        DependencyProperty property,
        object? value,
        int decodePixelWidth)
    {
        if (d is not Image image)
        {
            return;
        }

        var name = value as string;
        if (string.IsNullOrEmpty(name))
        {
            image.Source = null;
            return;
        }

        var overrides = App.Services.GetRequiredService<IInventoryImageOverrideService>();
        var url = overrides.GetOverride(name);
        if (url is null)
        {
            image.Source = null;
            return;
        }

        // The full-window preview decodes at native resolution (width 0) and is resolved fresh each
        // time; the bounded thumbnail variant is memoized. The key includes the override URL (so
        // editing the config takes effect on the next open) and the decode width (so the same URL is
        // cached separately per size). Same rule as RewardPreview.
        var memoize = decodePixelWidth != 0;
        var memoKey = $"{decodePixelWidth}|{name}\n{url}";
        if (memoize && _resolved.TryGetValue(memoKey, out var memoized))
        {
            image.Source = memoized;
            return;
        }

        image.Source = null;

        ImageSource? source;
        try
        {
            source = await ThumbnailLoader.ResolveAsync([url], decodePixelWidth);
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
}
