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
