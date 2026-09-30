using WikeloContractor.Models;

namespace WikeloContractor.ViewModels;

/// <summary>Display-ready reputation standing for the Catalog rank bar (localized at build time).</summary>
public sealed class ReputationSummary
{
    /// <summary>Localized rank name, e.g. "Very Good Customer".</summary>
    public required string TierLabel { get; init; }

    /// <summary>"640 / 999 XP", or the max-rank line at the top tier.</summary>
    public required string ProgressText { get; init; }

    /// <summary>
    /// Fill in [0, 1] of each rank's bar section, lowest rank first — so the bar shows which of the
    /// three ranks is reached and how far into the current one the total is.
    /// </summary>
    public required IReadOnlyList<double> Segments { get; init; }

    public static ReputationSummary From(int totalReputation)
    {
        var status = ReputationLevels.Compute(totalReputation);
        var progressText = status.NextThreshold is { } next
            ? Localized.Format("Reputation_Progress", status.TotalReputation, next)
            : Localized.Format("Reputation_Max", status.TotalReputation);

        return new ReputationSummary
        {
            TierLabel = Localized.String(ReputationTierDisplay.LabelKey(status.Tier)) ?? string.Empty,
            ProgressText = progressText,
            Segments = ReputationLevels.Segments(totalReputation).Select(s => s.Fill).ToList(),
        };
    }
}
