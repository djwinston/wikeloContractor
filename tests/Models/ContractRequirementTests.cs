using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

public class ContractRequirementTests
{
    [Theory]
    [InlineData(2, 2, "2")]
    [InlineData(1, 3, "1–3")]
    [InlineData(null, 1, "≤1")]
    [InlineData(1, null, "≥1")]
    [InlineData(null, null, "?")]
    public void AmountLabel_covers_all_range_shapes(int? min, int? max, string expected)
    {
        var requirement = new ContractRequirement { Name = "Gold", MinAmount = min, MaxAmount = max };

        Assert.Equal(expected, requirement.AmountLabel);
    }

    [Theory]
    [InlineData(36, 36, "36 SCU")]
    [InlineData(1.5, 3, "1.5–3 SCU")]
    [InlineData(null, 8, "≤8 SCU")]
    public void AmountLabel_prefers_scu_amounts_when_present(double? minScu, double? maxScu, string expected)
    {
        var requirement = new ContractRequirement { Name = "Quantainium", MinScu = minScu, MaxScu = maxScu };

        Assert.Equal(expected, requirement.AmountLabel);
    }

    [Fact]
    public void Contract_defaults_are_safe_before_enrichment()
    {
        var contract = new WikeloContract
        {
            Uuid = "u",
            Title = "t",
            Requirements = [],
        };

        Assert.Empty(contract.Rewards);
        Assert.Equal(ContractCategory.Unknown, contract.Category);
    }
}
