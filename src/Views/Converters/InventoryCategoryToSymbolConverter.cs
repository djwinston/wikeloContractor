using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;
using WikeloContractor.Models;

namespace WikeloContractor.Views.Converters;

/// <summary>Maps an inventory category to the placeholder icon shown until an item image loads.</summary>
public sealed class InventoryCategoryToSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            InventoryCategory.OreMineral => SymbolRegular.Diamond24,
            InventoryCategory.Armor => SymbolRegular.Shield24,
            InventoryCategory.Weapon => SymbolRegular.Target24,
            InventoryCategory.Vehicle => SymbolRegular.VehicleCar24,
            InventoryCategory.Component => SymbolRegular.Settings24,
            InventoryCategory.CreatureMaterial => SymbolRegular.LeafOne24,
            InventoryCategory.Collectible => SymbolRegular.Ribbon24,
            InventoryCategory.Consumable => SymbolRegular.Food24,
            InventoryCategory.Favor => SymbolRegular.Star24,
            _ => SymbolRegular.Box24,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
