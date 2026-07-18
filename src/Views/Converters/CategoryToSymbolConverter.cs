using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;
using WikeloContractor.Models;

namespace WikeloContractor.Views.Converters;

/// <summary>Maps a contract's reward category to the placeholder icon of the preview box.</summary>
public sealed class CategoryToSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ContractCategory.Ship => SymbolRegular.Rocket24,
            ContractCategory.GroundVehicle => SymbolRegular.VehicleCar24,
            ContractCategory.Paint => SymbolRegular.PaintBrush24,
            ContractCategory.Weapon => SymbolRegular.Target24,
            ContractCategory.Armor => SymbolRegular.Shield24,
            _ => SymbolRegular.Box24,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
