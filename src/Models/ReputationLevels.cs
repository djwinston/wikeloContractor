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

/// <summary>Current standing computed from accumulated reputation.</summary>
/// <param name="Tier">The rank the total falls into.</param>
/// <param name="TotalXp">Accumulated Wikelo reputation.</param>
/// <param name="NextThreshold">Reputation that unlocks the next rank; null at the top rank.</param>
/// <param name="Fraction">Progress toward the next rank in [0, 1]; 1 at the top rank.</param>
public readonly record struct ReputationStatus(
    ReputationTier Tier,
    int TotalXp,
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

    public static ReputationStatus Compute(int totalXp)
    {
        if (totalXp >= VeryBestThreshold)
        {
            return new ReputationStatus(ReputationTier.VeryBestCustomer, totalXp, null, 1.0);
        }

        if (totalXp >= VeryGoodThreshold)
        {
            var span = VeryBestThreshold - VeryGoodThreshold;
            return new ReputationStatus(
                ReputationTier.VeryGoodCustomer,
                totalXp,
                VeryBestThreshold,
                (double)(totalXp - VeryGoodThreshold) / span);
        }

        return new ReputationStatus(
            ReputationTier.NewCustomer,
            totalXp,
            VeryGoodThreshold,
            (double)totalXp / VeryGoodThreshold);
    }
}
