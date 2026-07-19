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

    /// <summary>Session-lifetime memoization of decoded thumbnails (frozen, shareable).</summary>
    private static readonly ConcurrentDictionary<string, ImageSource?> _resolved = new();

    public static readonly DependencyProperty ItemNameProperty = DependencyProperty.RegisterAttached(
        "ItemName",
        typeof(string),
        typeof(InventoryPreview),
        new PropertyMetadata(null, OnItemNameChanged));

    public static string? GetItemName(DependencyObject obj) => (string?)obj.GetValue(ItemNameProperty);

    public static void SetItemName(DependencyObject obj, string? value) => obj.SetValue(ItemNameProperty, value);

    private static async void OnItemNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        var name = e.NewValue as string;
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

        // The key includes the override URL, so editing the config still takes effect on the next open.
        var memoKey = $"{name}\n{url}";
        if (_resolved.TryGetValue(memoKey, out var memoized))
        {
            image.Source = memoized;
            return;
        }

        image.Source = null;

        ImageSource? source;
        try
        {
            source = await ThumbnailLoader.ResolveAsync([url], _decodePixelWidth);
        }
        catch (Exception)
        {
            // Preview is best-effort decoration; the placeholder icon simply stays.
            return;
        }

        _resolved[memoKey] = source;

        // The template may have been rebound (filtering, refresh) while we were loading.
        if (ReferenceEquals(image.GetValue(ItemNameProperty), name))
        {
            image.Source = source;
        }
    }
}
