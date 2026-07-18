using System.Globalization;
using System.Windows.Data;

namespace WikeloContractor.Views.Converters;

/// <summary>
/// Visible when the value is present (non-null, non-empty string) — hides optional detail
/// rows. With <see cref="Invert"/> the logic flips: visible while the value is absent,
/// which keeps a placeholder icon up until an image loads.
/// </summary>
public sealed class PresenceToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var present = value is not (null or "");
        return present != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
