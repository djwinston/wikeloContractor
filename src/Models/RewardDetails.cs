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
}
