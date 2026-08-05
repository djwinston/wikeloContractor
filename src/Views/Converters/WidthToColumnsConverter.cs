using System.Globalization;
using System.Windows.Data;

namespace WikeloContractor.Views.Converters;

/// <summary>
/// Available width → how many columns fit, for a <c>UniformGrid</c> that should follow the window
/// instead of a fixed count. The minimum column width comes from <c>ConverterParameter</c>, so one
/// converter serves every grid rather than a class per layout.
/// <para>
/// A <c>WrapPanel</c> with a fixed <c>ItemWidth</c> would flow the same items with no code at all,
/// and was rejected for how it looks: items keep their width and leave a ragged gutter on the right.
/// <c>UniformGrid</c> columns stretch to fill the row, which is what a column grid should be.
/// </para>
/// <para>
/// Never returns 0: a <c>UniformGrid</c> reads that as "decide for yourself" and lays the items out
/// roughly square, which at a narrow window is the one arrangement that must not happen.
/// </para>
/// </summary>
public sealed class WidthToColumnsConverter : IValueConverter
{
    /// <summary>Used when the parameter is missing or unparseable — a readable card width.</summary>
    private const double DefaultMinimum = 260;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var minimum = parameter switch
        {
            double d when d > 0 => d,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                          && parsed > 0 => parsed,
            _ => DefaultMinimum,
        };

        // Width is NaN before the first layout pass and 0 while the tab is still unrealized.
        var width = value is double w && !double.IsNaN(w) ? w : 0;

        return Math.Max(1, (int)(width / minimum));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
