using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// A required-item chip prepared for display: its name, amount label, and how well it is covered by
/// the current inventory (drives the chip color). Built by the catalog card and detail view models.
/// </summary>
public sealed record RequirementChip
{
    public required string Name { get; init; }

    public required string AmountLabel { get; init; }

    public required RequirementAvailability Availability { get; init; }

    public static RequirementChip From(ContractRequirement requirement, IInventoryStore store) => new()
    {
        Name = requirement.Name,
        AmountLabel = requirement.AmountLabel,
        Availability = InventoryReadiness.Availability(requirement, store.GetCount(requirement.Name)),
    };

    /// <summary>An uncolored chip — used for completed contracts, where availability is moot.</summary>
    public static RequirementChip Neutral(ContractRequirement requirement) => new()
    {
        Name = requirement.Name,
        AmountLabel = requirement.AmountLabel,
        Availability = RequirementAvailability.None,
    };
}
