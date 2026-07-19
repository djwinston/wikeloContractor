using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

public sealed class InventoryCategoryClassifierTests
{
    [Theory]
    [InlineData("Wikelo Favor", false, InventoryCategory.Favor)]
    [InlineData("MG Scrip", false, InventoryCategory.Favor)]
    [InlineData("Polaris Bit", false, InventoryCategory.Favor)]
    [InlineData("Carinite (Pure)", false, InventoryCategory.OreMineral)]
    [InlineData("Jaclium (Ore)", false, InventoryCategory.OreMineral)]
    [InlineData("Quantainium", true, InventoryCategory.OreMineral)]
    [InlineData("Ace Interceptor Helmet", false, InventoryCategory.Armor)]
    [InlineData("Antium Core", false, InventoryCategory.Armor)]
    [InlineData("R97 Shotgun", false, InventoryCategory.Weapon)]
    [InlineData("Parallax Energy Assault Rifle", false, InventoryCategory.Weapon)]
    [InlineData("NN-13 Cannon", false, InventoryCategory.Weapon)]
    [InlineData("Argo ATLS", false, InventoryCategory.Vehicle)]
    [InlineData("Argo ATLS GEO", false, InventoryCategory.Vehicle)]
    [InlineData("RCMBNT-PWL-1", false, InventoryCategory.Component)]
    [InlineData("ASD Secure Drive", false, InventoryCategory.Component)]
    [InlineData("DCHS-05 Orbital Positioning Comp-Board", false, InventoryCategory.Component)]
    [InlineData("Irradiated Valakkar Pearl (Grade AAA)", false, InventoryCategory.CreatureMaterial)]
    [InlineData("Tundra Kopion Horn", false, InventoryCategory.CreatureMaterial)]
    [InlineData("UEE 6th Platoon Medal (Pristine)", false, InventoryCategory.Collectible)]
    [InlineData("Tevarin War Service Marker (Pristine)", false, InventoryCategory.Collectible)]
    [InlineData("Vestal Water", false, InventoryCategory.Consumable)]
    [InlineData("Berry Blend Smoothie", false, InventoryCategory.Consumable)]
    [InlineData("Expired Quantanium Fuel Canister", false, InventoryCategory.Consumable)]
    [InlineData("Some Unmapped Thing", false, InventoryCategory.Other)]
    public void Classify_buckets_representative_live_names(string name, bool hasScu, InventoryCategory expected) =>
        Assert.Equal(expected, InventoryCategoryClassifier.Classify(name, hasScu));

    [Fact]
    public void Classification_is_case_insensitive() =>
        Assert.Equal(InventoryCategory.Weapon, InventoryCategoryClassifier.Classify("r97 shotgun", hasScu: false));
}
