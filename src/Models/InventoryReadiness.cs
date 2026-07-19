namespace WikeloContractor.Models;

/// <summary>How much of a single required item the player currently holds, relative to the amount needed.</summary>
public enum RequirementAvailability
{
    /// <summary>None in the inventory.</summary>
    None,

    /// <summary>Some, but fewer than required.</summary>
    Partial,

    /// <summary>At least the required amount.</summary>
    Full,
}

/// <summary>Compares contract requirements against inventory counts — the single home for readiness math.</summary>
public static class InventoryReadiness
{
    /// <summary>
    /// The amount a requirement needs, as a single number the inventory counter is compared against.
    /// Piece amounts win over SCU (the counter is a piece count); the API encodes a fixed "bring N"
    /// as the max, so max is preferred. Falls back to 1 when nothing is specified.
    /// </summary>
    public static double RequiredAmount(ContractRequirement requirement) =>
        requirement.MaxAmount ?? requirement.MinAmount ?? requirement.MaxScu ?? requirement.MinScu ?? 1;

    /// <summary>The whole-unit amount deducted from the inventory when a contract is completed.</summary>
    public static int RequiredCount(ContractRequirement requirement) =>
        (int)Math.Ceiling(RequiredAmount(requirement));

    /// <summary>Availability of one requirement given how many the player holds.</summary>
    public static RequirementAvailability Availability(ContractRequirement requirement, int have)
    {
        if (have <= 0)
        {
            return RequirementAvailability.None;
        }

        return have >= RequiredAmount(requirement) ? RequirementAvailability.Full : RequirementAvailability.Partial;
    }

    /// <summary>Whether a requirement is fully covered by the given held count — the "satisfied" rule.</summary>
    public static bool IsSatisfied(ContractRequirement requirement, int have) =>
        Availability(requirement, have) == RequirementAvailability.Full;
}
