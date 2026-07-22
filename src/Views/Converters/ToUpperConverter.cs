using System.Globalization;
using System.Windows.Data;

namespace WikeloContractor.Views.Converters;

/// <summary>
/// Uppercases a bound string for the design system's overline labels. XAML needs this because WPF
/// has no text-transform: the only alternative is baking the casing into the localization resource,
/// which is wrong for a value shown in mixed case elsewhere (the inventory category names appear
/// both as section headers and as filter dropdown entries).
/// <para>Uses the current UI culture, so Turkish-style casing rules are respected.</para>
/// </summary>
public sealed class ToUpperConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.ToUpper(CultureInfo.CurrentUICulture) ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
