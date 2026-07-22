using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The per-contract readiness rollup shown on the catalog card and the detail page: the colored
/// requirement chips, whether the contract can be turned in, and the "X / Y satisfied" label.
/// The single home for that composition so the two view models can't drift; built from the
/// per-requirement primitives in <see cref="InventoryReadiness"/> / <see cref="RequirementChip"/>.
/// </summary>
public sealed class ContractReadiness
{
    /// <summary>Readiness of a contract with no requirements — nothing to show, nothing to turn in.</summary>
    public static readonly ContractReadiness Empty = new([], satisfied: 0, total: 0, isCompleted: false);

    private ContractReadiness(IReadOnlyList<RequirementChip> chips, int satisfied, int total, bool isCompleted)
    {
        Chips = chips;
        IsReady = total > 0 && satisfied == total;
        ShowReadiness = !isCompleted && total > 0;
        ReadinessLabel = $"{satisfied} / {total}";
        // A completed contract reads as fully done regardless of what is left in the inventory —
        // the items were spent on it, so a half-empty bar under a completed row would be a lie.
        Fraction = isCompleted ? 1d : total > 0 ? (double)satisfied / total : 0d;
    }

    /// <summary>Requirement chips carrying per-item availability vs the inventory (drives chip color).</summary>
    public IReadOnlyList<RequirementChip> Chips { get; }

    /// <summary>All requirements are fully covered by the inventory — the contract can be turned in.</summary>
    public bool IsReady { get; }

    /// <summary>Readiness (badge + count) is only meaningful before the contract is completed.</summary>
    public bool ShowReadiness { get; }

    /// <summary>"3 / 5" — satisfied requirements out of total.</summary>
    public string ReadinessLabel { get; }

    /// <summary>Satisfied share in [0, 1] for the row's ProgressBar (Maximum="1"); 1 when completed.</summary>
    public double Fraction { get; }

    /// <summary>
    /// Composes the readiness for a contract's requirements against the inventory. Completed contracts
    /// get neutral (uncolored) chips since availability is moot, but the satisfied count still reflects
    /// the real inventory.
    /// </summary>
    public static ContractReadiness From(IReadOnlyList<ContractRequirement> requirements, IInventoryStore store, bool isCompleted)
    {
        if (requirements.Count == 0)
        {
            return Empty;
        }

        var chips = new RequirementChip[requirements.Count];
        var satisfied = 0;
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = requirements[i];
            chips[i] = isCompleted ? RequirementChip.Neutral(requirement) : RequirementChip.From(requirement, store);
            if (InventoryReadiness.IsSatisfied(requirement, store.GetCount(requirement.Name)))
            {
                satisfied++;
            }
        }

        return new ContractReadiness(chips, satisfied, requirements.Count, isCompleted);
    }
}
