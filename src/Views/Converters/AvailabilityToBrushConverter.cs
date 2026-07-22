using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WikeloContractor.Models;

namespace WikeloContractor.Views.Converters;

/// <summary>
/// Maps a requirement's <see cref="RequirementAvailability"/> to the chip brushes: the caution and
/// success roles are low-alpha brand tints (which Fluent does not provide), but the neutral
/// "not in stock" role is a plain WPF-UI theme fill — the design spec maps it to
/// <c>ControlFillColorSecondaryBrush</c>, so it must adapt to the actual surface rather than a
/// hardcoded navy that reads as a dark hole on our Mica background.
/// <para>
/// The chip needs four brushes per state (fill, border, label, quantity), so the part is selected
/// by <c>ConverterParameter</c> — one parameterized converter rather than four near-identical
/// classes. Defaults to <see cref="ChipPart.Background"/> when no parameter is given.
/// </para>
/// <para>
/// Brushes are resolved by key at convert time, so the chip follows runtime theme changes when the
/// brand palette (or the WPF-UI theme) dictionary is swapped.
/// </para>
/// </summary>
public sealed class AvailabilityToBrushConverter : IValueConverter
{
    /// <summary>Which of a chip's four brushes to resolve.</summary>
    public enum ChipPart
    {
        Background,
        Border,
        Foreground,
        Value,
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var part = parameter switch
        {
            ChipPart p => p,
            string s when Enum.TryParse<ChipPart>(s, ignoreCase: true, out var parsed) => parsed,
            _ => ChipPart.Background,
        };

        // Neutral is a Fluent theme role, not a brand tint — resolve straight from the WPF-UI theme
        // so it tracks the real surface colour (see NeutralThemeKey).
        var key = value switch
        {
            RequirementAvailability.Full => $"ChipSuccess{part}Brush",
            RequirementAvailability.Partial => $"ChipCaution{part}Brush",
            _ => NeutralThemeKey(part),
        };

        return Application.Current.TryFindResource(key) as Brush;
    }

    /// <summary>WPF-UI theme key for the neutral chip's given part.</summary>
    private static string NeutralThemeKey(ChipPart part) => part switch
    {
        ChipPart.Border => "ControlStrokeColorDefaultBrush",
        ChipPart.Foreground => "TextFillColorSecondaryBrush",
        ChipPart.Value => "TextFillColorPrimaryBrush",
        _ => "ControlFillColorSecondaryBrush",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
