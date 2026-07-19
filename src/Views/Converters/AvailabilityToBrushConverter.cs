using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WikeloContractor.Models;

namespace WikeloContractor.Views.Converters;

/// <summary>
/// Maps a requirement's <see cref="RequirementAvailability"/> to the chip background brush: a subtle
/// success tint when fully covered, a caution tint when partially covered, the default fill otherwise.
/// Resolves theme brushes by key so it follows light/dark and runtime theme changes.
/// </summary>
public sealed class AvailabilityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            RequirementAvailability.Full => "SystemFillColorSuccessBackgroundBrush",
            RequirementAvailability.Partial => "SystemFillColorCautionBackgroundBrush",
            _ => "ControlFillColorSecondaryBrush",
        };

        return Application.Current.TryFindResource(key) as Brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
