namespace WikeloContractor.Models;

/// <summary>
/// Display details of a reward item, captured from the same item/vehicle detail response
/// that enrichment already fetches for classification (zero extra API calls).
/// Item-specific and vehicle-specific groups are mutually exclusive — the other group is null.
/// The full field inventory of the API responses is documented in docs/api-item-fields.md.
/// </summary>
public sealed record RewardDetails
{
    // ---- Common ----

    /// <summary>English lore description (API data stays English by policy).</summary>
    public string? Description { get; init; }

    /// <summary>Manufacturer display name, e.g. "Quirinus Tech", "Gatac Manufacture".</summary>
    public string? Manufacturer { get; init; }

    // ---- Items (armor, weapons, ...) ----

    /// <summary>Human-readable type, e.g. "Arms (Armor)".</summary>
    public string? TypeLabel { get; init; }

    /// <summary>E.g. "Heavy".</summary>
    public string? SubTypeLabel { get; init; }

    /// <summary>E.g. "Common", "Rare".</summary>
    public string? Rarity { get; init; }

    /// <summary>
    /// Damage multipliers by damage type ("physical", "energy", ...), lower = better
    /// protection. Taken from <c>damage_resistance_map</c> (the <c>*_change</c> deltas are skipped).
    /// </summary>
    public IReadOnlyDictionary<string, double>? DamageResistances { get; init; }

    public double? TemperatureMin { get; init; }

    public double? TemperatureMax { get; init; }

    public double? RadiationCapacity { get; init; }

    public double? RadiationDissipationRate { get; init; }

    // ---- Vehicles (ships, ground vehicles, paint variant records) ----

    /// <summary>E.g. "Multi-Role".</summary>
    public string? Career { get; init; }

    /// <summary>E.g. "Starter / Pathfinder".</summary>
    public string? Role { get; init; }

    public double? CargoCapacityScu { get; init; }

    public int? CrewMin { get; init; }

    public int? CrewMax { get; init; }

    public double? Health { get; init; }

    public double? ShieldHp { get; init; }

    /// <summary>Standard combat maneuvering speed, m/s.</summary>
    public double? SpeedScm { get; init; }

    /// <summary>Top (navigation) speed, m/s.</summary>
    public double? SpeedMax { get; init; }

    /// <summary>Pledge store price, USD.</summary>
    public double? Msrp { get; init; }

    public string? PledgeUrl { get; init; }

    /// <summary>
    /// Installed weapons: fixed guns (from <c>weaponry</c>) plus turret mounts and missile
    /// racks (from <c>ports</c>). Null for non-vehicle rewards, empty when unarmed.
    /// </summary>
    public IReadOnlyList<ShipLoadoutEntry>? Weapons { get; init; }

    /// <summary>Total missiles carried (from <c>weaponry.missiles</c>).</summary>
    public int? MissileCount { get; init; }

    /// <summary>
    /// Installed core components (power plants, shields, coolers, quantum drive) from
    /// <c>ports</c>. Null for non-vehicle rewards.
    /// </summary>
    public IReadOnlyList<ShipLoadoutEntry>? Components { get; init; }
}

/// <summary>
/// Maps a core component's API <c>type</c> (see <see cref="ShipLoadoutEntry.Type"/>) to its
/// localization resource key — the single home for this decision, mirroring
/// <see cref="ContractCategoryDisplay"/>. Types outside this list are not core components
/// (e.g. guns) and render with no group label.
/// </summary>
public static class ComponentTypeDisplay
{
    public static string? LabelKey(string? type) => type switch
    {
        "PowerPlant" => "Details_Comp_PowerPlant",
        "Shield" => "Details_Comp_Shield",
        "Cooler" => "Details_Comp_Cooler",
        "QuantumDrive" => "Details_Comp_QuantumDrive",
        "JumpDrive" => "Details_Comp_JumpDrive",
        _ => null,
    };
}

/// <summary>
/// Maps an armor damage-resistance type (<c>damage_resistance_map</c> keys carried in
/// <see cref="RewardDetails.DamageResistances"/>) to its localization key — the single home for the
/// short display label, mirroring <see cref="ComponentTypeDisplay"/>. Unmapped types fall back to
/// the raw API name.
/// </summary>
public static class DamageTypeDisplay
{
    public static string? LabelKey(string type) => type.ToLowerInvariant() switch
    {
        "impact" => "Damage_Impact",
        "physical" => "Damage_Physical",
        "energy" => "Damage_Energy",
        "distortion" => "Damage_Distortion",
        "thermal" => "Damage_Thermal",
        "biochemical" or "biochem" => "Damage_Biochem",
        "stun" => "Damage_Stun",
        _ => null,
    };
}

/// <summary>One installed component or weapon of a vehicle, grouped ("4 × CoverAll (S2, Military B)").</summary>
public sealed record ShipLoadoutEntry
{
    /// <summary>Item display name, e.g. "Attrition-3 Repeater", "CoverAll".</summary>
    public required string Name { get; init; }

    /// <summary>API type of the equipped item, e.g. "PowerPlant", "Shield", "MissileLauncher".</summary>
    public string? Type { get; init; }

    /// <summary>Human kind label for guns, e.g. "Laser Repeater" (from the gun's own item record).</summary>
    public string? TypeLabel { get; init; }

    /// <summary>Component size class (S1..), when the API reports one.</summary>
    public int? Size { get; init; }

    /// <summary>Component grade, "A".."D".</summary>
    public string? Grade { get; init; }

    /// <summary>Component class, e.g. "Military", "Industrial", "Civilian".</summary>
    public string? Class { get; init; }

    public int Count { get; init; } = 1;
}
