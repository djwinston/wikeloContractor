namespace WikeloContractor.Models;

/// <summary>The three Wikelo Emporium standing ranks, in ascending order.</summary>
public enum ReputationTier
{
    NewCustomer,
    VeryGoodCustomer,
    VeryBestCustomer,
}

/// <summary>Maps a tier to its localization resource key — the single home for this decision.</summary>
public static class ReputationTierDisplay
{
    public static string LabelKey(ReputationTier tier) => tier switch
    {
        ReputationTier.NewCustomer => "Reputation_Tier_New",
        ReputationTier.VeryGoodCustomer => "Reputation_Tier_VeryGood",
        ReputationTier.VeryBestCustomer => "Reputation_Tier_VeryBest",
        _ => "Reputation_Tier_New",
    };
}

/// <summary>
/// Current standing computed from accumulated reputation.
/// <para>
/// <b>Reputation, not XP.</b> The UI renders these numbers as "XP" — see docs/design-system.md,
/// "Terminology" — but that is a display mask over the API's value, applied in the localization
/// strings and nowhere else. This member was called <c>TotalXp</c>, which put the label in the one
/// layer the rule exists to keep clean: if the game ever renames what it shows, the point is that
/// only the resource strings change.
/// </para>
/// </summary>
/// <param name="Tier">The rank the total falls into.</param>
/// <param name="TotalReputation">Accumulated Wikelo reputation.</param>
/// <param name="NextThreshold">Reputation that unlocks the next rank; null at the top rank.</param>
/// <param name="Fraction">Progress toward the next rank in [0, 1]; 1 at the top rank.</param>
public readonly record struct ReputationStatus(
    ReputationTier Tier,
    int TotalReputation,
    int? NextThreshold,
    double Fraction);

/// <summary>
/// Wikelo standing thresholds and the tier lookup. The API does not expose these
/// (<c>min_standing</c>/<c>rank_index</c> are null on every mission), so the values live here as
/// the single source of truth: New Customer (0) → Very Good Customer (340) → Very Best Customer (999).
/// </summary>
public static class ReputationLevels
{
    public const int VeryGoodThreshold = 340;

    public const int VeryBestThreshold = 999;

    public static ReputationStatus Compute(int totalReputation)
    {
        if (totalReputation >= VeryBestThreshold)
        {
            return new ReputationStatus(ReputationTier.VeryBestCustomer, totalReputation, null, 1.0);
        }

        if (totalReputation >= VeryGoodThreshold)
        {
            var span = VeryBestThreshold - VeryGoodThreshold;
            return new ReputationStatus(
                ReputationTier.VeryGoodCustomer,
                totalReputation,
                VeryBestThreshold,
                (double)(totalReputation - VeryGoodThreshold) / span);
        }

        return new ReputationStatus(
            ReputationTier.NewCustomer,
            totalReputation,
            VeryGoodThreshold,
            (double)totalReputation / VeryGoodThreshold);
    }

    /// <summary>
    /// One segment per rank, in ascending order, for the catalog's three-section rank bar. A rank's
    /// segment fills as the total climbs from its threshold to the next one; ranks already passed are
    /// full, ranks not reached are empty. The top rank has no ceiling, so its segment is simply full
    /// once reached — which also keeps <see cref="ReputationStatus.Fraction"/> equal to the current
    /// rank's fill below the top.
    /// </summary>
    public static IReadOnlyList<ReputationSegment> Segments(int totalReputation)
    {
        return
        [
            Segment(ReputationTier.NewCustomer, 0, VeryGoodThreshold),
            Segment(ReputationTier.VeryGoodCustomer, VeryGoodThreshold, VeryBestThreshold),
            Segment(ReputationTier.VeryBestCustomer, VeryBestThreshold, null),
        ];

        ReputationSegment Segment(ReputationTier tier, int threshold, int? next)
        {
            var fill = next is { } ceiling
                ? Math.Clamp((double)(totalReputation - threshold) / (ceiling - threshold), 0, 1)
                : totalReputation >= threshold ? 1 : 0;
            return new ReputationSegment(tier, threshold, fill);
        }
    }
}

/// <summary>One rank's section of the rank bar.</summary>
/// <param name="Tier">The rank this section stands for.</param>
/// <param name="Threshold">Reputation at which the rank is reached.</param>
/// <param name="Fill">How much of the section is filled, in [0, 1].</param>
public readonly record struct ReputationSegment(ReputationTier Tier, int Threshold, double Fill);
