using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

public class ReputationLevelsTests
{
    [Theory]
    [InlineData(0, ReputationTier.NewCustomer)]
    [InlineData(339, ReputationTier.NewCustomer)]
    [InlineData(340, ReputationTier.VeryGoodCustomer)]
    [InlineData(998, ReputationTier.VeryGoodCustomer)]
    [InlineData(999, ReputationTier.VeryBestCustomer)]
    [InlineData(5000, ReputationTier.VeryBestCustomer)]
    public void Compute_picks_the_tier_at_each_boundary(int total, ReputationTier expected)
    {
        Assert.Equal(expected, ReputationLevels.Compute(total).Tier);
    }

    [Theory]
    [InlineData(0, 340)]
    [InlineData(339, 340)]
    [InlineData(340, 999)]
    [InlineData(998, 999)]
    public void Compute_reports_the_next_threshold_below_the_top_rank(int total, int expectedNext)
    {
        Assert.Equal(expectedNext, ReputationLevels.Compute(total).NextThreshold);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(2600)]
    public void Compute_has_no_next_threshold_and_is_full_at_the_top_rank(int total)
    {
        var status = ReputationLevels.Compute(total);

        Assert.Null(status.NextThreshold);
        Assert.Equal(1.0, status.Fraction);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(170, 0.5)]     // halfway to Very Good (340)
    [InlineData(340, 0.0)]     // start of Very Good
    public void Compute_reports_progress_within_the_current_tier(int total, double expectedFraction)
    {
        var status = ReputationLevels.Compute(total);

        Assert.Equal(expectedFraction, status.Fraction, precision: 3);
    }

    [Fact]
    public void Compute_scales_progress_across_the_Very_Good_span()
    {
        // 340 → 999 spans 659 points; 505 sits 165 into it.
        var status = ReputationLevels.Compute(505);

        Assert.Equal(165.0 / 659.0, status.Fraction, precision: 6);
    }

    [Fact]
    public void Segments_lists_the_three_ranks_in_order_with_their_thresholds()
    {
        var segments = ReputationLevels.Segments(0);

        Assert.Equal(
            [ReputationTier.NewCustomer, ReputationTier.VeryGoodCustomer, ReputationTier.VeryBestCustomer],
            segments.Select(s => s.Tier));
        Assert.Equal([0, 340, 999], segments.Select(s => s.Threshold));
    }

    [Theory]
    [InlineData(0, 0.0, 0.0, 0.0)]
    [InlineData(170, 0.5, 0.0, 0.0)]            // halfway through New
    [InlineData(340, 1.0, 0.0, 0.0)]            // New passed, Very Good just started
    [InlineData(460, 1.0, 120.0 / 659, 0.0)]    // the screenshot case
    [InlineData(998, 1.0, 658.0 / 659, 0.0)]
    [InlineData(999, 1.0, 1.0, 1.0)]            // top rank has no ceiling — full once reached
    [InlineData(5000, 1.0, 1.0, 1.0)]
    [InlineData(-10, 0.0, 0.0, 0.0)]            // never below empty
    public void Segments_fill_passed_ranks_and_the_current_one_by_progress(
        int total, double newFill, double veryGoodFill, double veryBestFill)
    {
        var segments = ReputationLevels.Segments(total);

        Assert.Equal(newFill, segments[0].Fill, precision: 6);
        Assert.Equal(veryGoodFill, segments[1].Fill, precision: 6);
        Assert.Equal(veryBestFill, segments[2].Fill, precision: 6);
    }
}
