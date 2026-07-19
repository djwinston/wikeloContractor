namespace WikeloContractor.Models;

/// <summary>
/// The bucket a required item falls into on the inventory page. Requirements carry no type from the
/// API, so the bucket is derived from the item name (see <see cref="InventoryCategoryClassifier"/>).
/// </summary>
public enum InventoryCategory
{
    OreMineral,
    Armor,
    Weapon,
    Vehicle,
    Component,
    CreatureMaterial,
    Collectible,
    Consumable,
    Favor,
    Other,
}

/// <summary>Maps an inventory category to its localization resource key — the single home for this decision.</summary>
public static class InventoryCategoryDisplay
{
    public static string LabelKey(InventoryCategory category) => category switch
    {
        InventoryCategory.OreMineral => "Inventory_Cat_OreMineral",
        InventoryCategory.Armor => "Inventory_Cat_Armor",
        InventoryCategory.Weapon => "Inventory_Cat_Weapon",
        InventoryCategory.Vehicle => "Inventory_Cat_Vehicle",
        InventoryCategory.Component => "Inventory_Cat_Component",
        InventoryCategory.CreatureMaterial => "Inventory_Cat_CreatureMaterial",
        InventoryCategory.Collectible => "Inventory_Cat_Collectible",
        InventoryCategory.Consumable => "Inventory_Cat_Consumable",
        InventoryCategory.Favor => "Inventory_Cat_Favor",
        _ => "Inventory_Cat_Other",
    };
}

/// <summary>
/// Classifies a required item into an <see cref="InventoryCategory"/> from its name (and whether it is
/// delivered by SCU). The rules are ordered — the first match wins — and heuristic: they are meant to
/// be refined as new items appear. Everything unmatched falls through to <see cref="InventoryCategory.Other"/>.
/// </summary>
public static class InventoryCategoryClassifier
{
    public static InventoryCategory Classify(string name, bool hasScu)
    {
        // Favor / currency-like tokens (Wikelo Favor, MG/Council Scrip, Polaris Bit). "Bit" is matched
        // as a whole word so it does not fire inside e.g. "Orbital".
        if (ContainsAny(name, "Favor", "Scrip") || ContainsWord(name, "Bit"))
        {
            return InventoryCategory.Favor;
        }

        // Mined ores and minerals — SCU delivery is the strongest signal; the rest are known ore names.
        if (hasScu || ContainsAny(name, "(Ore)", "(Pure)", "Carinite", "Quantainium", "Savrilium", "Sadaryx"))
        {
            return InventoryCategory.OreMineral;
        }

        // Hand weapons.
        if (ContainsAny(name, "Rifle", "Shotgun", "Pistol", "Cannon", "Launcher", "LMG", "Sniper"))
        {
            return InventoryCategory.Weapon;
        }

        // Vehicles handed over (currently only the Argo ATLS family).
        if (name.Contains("ATLS", StringComparison.OrdinalIgnoreCase))
        {
            return InventoryCategory.Vehicle;
        }

        // Ship/vehicle components and tech parts.
        if (name.StartsWith("RCMBNT", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(name, "Drive", "Comp-Board", "Metamaterial", "Orbital Positioning"))
        {
            return InventoryCategory.Component;
        }

        // Armor set pieces (checked after weapons/components so e.g. "Comp-Board" is not caught by "Core").
        if (ContainsAny(name, "Helmet", "Core", "Arms", "Legs", "Backpack", "Flight Suit", "Flight Helmet"))
        {
            return InventoryCategory.Armor;
        }

        // Harvested creature and alien materials.
        if (ContainsAny(name, "Valakkar", "Kopion", "Yormandi", "Vanduul", "Fungus", "Horn", "Fang", "Pearl", "Tongue", "Eye", "Artifact Fragment"))
        {
            return InventoryCategory.CreatureMaterial;
        }

        // Medals and markers.
        if (ContainsAny(name, "Medal", "Marker"))
        {
            return InventoryCategory.Collectible;
        }

        // Food, drink and fuel.
        if (ContainsAny(name, "Smoothie", "Ice Cream", "Water", "Fuel Canister", "Blend"))
        {
            return InventoryCategory.Consumable;
        }

        return InventoryCategory.Other;
    }

    private static bool ContainsAny(string name, params string[] needles) =>
        needles.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsWord(string name, string word) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            name, $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
