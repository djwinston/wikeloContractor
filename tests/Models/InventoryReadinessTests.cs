using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

public sealed class InventoryReadinessTests
{
    [Fact]
    public void Required_amount_prefers_piece_max_then_min_then_scu()
    {
        Assert.Equal(3, InventoryReadiness.RequiredAmount(new ContractRequirement { Name = "x", MaxAmount = 3, MinAmount = 1 }));
        Assert.Equal(2, InventoryReadiness.RequiredAmount(new ContractRequirement { Name = "x", MinAmount = 2 }));
        Assert.Equal(36, InventoryReadiness.RequiredAmount(new ContractRequirement { Name = "x", MaxScu = 36 }));
        Assert.Equal(1, InventoryReadiness.RequiredAmount(new ContractRequirement { Name = "x" }));
    }

    [Theory]
    [InlineData(0, RequirementAvailability.None)]
    [InlineData(1, RequirementAvailability.Partial)]
    [InlineData(2, RequirementAvailability.Partial)]
    [InlineData(3, RequirementAvailability.Full)]
    [InlineData(5, RequirementAvailability.Full)]
    public void Availability_reflects_held_vs_needed(int have, RequirementAvailability expected)
    {
        var requirement = new ContractRequirement { Name = "Gold", MaxAmount = 3 };

        Assert.Equal(expected, InventoryReadiness.Availability(requirement, have));
    }

    [Fact]
    public void Required_count_rounds_up_to_a_whole_deducted_unit()
    {
        Assert.Equal(3, InventoryReadiness.RequiredCount(new ContractRequirement { Name = "x", MaxAmount = 3 }));
        Assert.Equal(36, InventoryReadiness.RequiredCount(new ContractRequirement { Name = "x", MaxScu = 36 }));
        Assert.Equal(2, InventoryReadiness.RequiredCount(new ContractRequirement { Name = "x", MaxScu = 1.5 }));
        Assert.Equal(1, InventoryReadiness.RequiredCount(new ContractRequirement { Name = "x" }));
    }

    [Fact]
    public void A_single_needed_item_is_full_at_one()
    {
        var requirement = new ContractRequirement { Name = "Wikelo Favor", MaxAmount = 1 };

        Assert.Equal(RequirementAvailability.None, InventoryReadiness.Availability(requirement, 0));
        Assert.Equal(RequirementAvailability.Full, InventoryReadiness.Availability(requirement, 1));
    }
}
