using WikeloContractor.Models;

namespace WikeloContractor.ViewModels;

/// <summary>Display-ready reputation standing for the Catalog progress bar (localized at build time).</summary>
public sealed class ReputationSummary
{
    /// <summary>Localized rank name, e.g. "Very Good Customer".</summary>
    public required string TierLabel { get; init; }

    /// <summary>"640 / 999 XP", or the max-rank line at the top tier.</summary>
    public required string ProgressText { get; init; }

    /// <summary>Progress toward the next rank in [0, 1] for the ProgressBar (Maximum="1").</summary>
    public required double Fraction { get; init; }

    public static ReputationSummary From(ReputationStatus status)
    {
        var progressText = status.NextThreshold is { } next
            ? Localized.Format("Reputation_Progress", status.TotalXp, next)
            : Localized.Format("Reputation_Max", status.TotalXp);

        return new ReputationSummary
        {
            TierLabel = Localized.String(ReputationTierDisplay.LabelKey(status.Tier)) ?? string.Empty,
            ProgressText = progressText,
            Fraction = status.Fraction,
        };
    }
}
