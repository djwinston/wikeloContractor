using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// The combined shortfall across several contracts. Pure arithmetic, so every case below is a table
/// entry rather than a click path — which is the point of the plan living in a model.
/// </summary>
public sealed class GatheringPlanTests
{
    private static WikeloContract Contract(params (string Name, int Amount)[] requirements) => new()
    {
        Uuid = Guid.NewGuid().ToString("N"),
        Title = "Contract",
        Requirements = [.. requirements.Select(r => new ContractRequirement { Name = r.Name, MaxAmount = r.Amount })],
    };

    /// <summary>An inventory reader over a plain dictionary; anything unlisted is zero.</summary>
    private static Func<string, int> Stock(params (string Name, int Count)[] counts)
    {
        var stock = counts.ToDictionary(c => c.Name, c => c.Count, StringComparer.OrdinalIgnoreCase);
        return name => stock.GetValueOrDefault(name);
    }

    [Fact]
    public void The_same_item_in_two_contracts_is_one_line_with_both_amounts()
    {
        // The reason the feature exists: the inventory is one pool, so two contracts asking for 36
        // each need 72 between them — which no per-contract readiness chip can say.
        var plan = GatheringPlan.Build(
            [Contract(("Gold", 36)), Contract(("Gold", 36))],
            Stock());

        var gold = Assert.Single(plan);
        Assert.Equal(72, gold.Required);
        Assert.Equal(72, gold.Outstanding);
    }

    [Fact]
    public void What_the_player_already_holds_comes_off_the_total()
    {
        var plan = GatheringPlan.Build(
            [Contract(("Gold", 36)), Contract(("Gold", 36))],
            Stock(("Gold", 50)));

        var gold = Assert.Single(plan);
        Assert.Equal(22, gold.Outstanding);
    }

    [Fact]
    public void A_covered_item_is_left_out_entirely()
    {
        // The list answers "what do I still need". An item needing nothing is not an answer to it.
        var plan = GatheringPlan.Build([Contract(("Gold", 36), ("Quantanium", 4))], Stock(("Gold", 40)));

        var item = Assert.Single(plan);
        Assert.Equal("Quantanium", item.Name);
    }

    [Fact]
    public void A_surplus_never_becomes_a_negative_shortfall()
    {
        // Holding twice what is needed must not subsidise the next item, nor render as "-36".
        var plan = GatheringPlan.Build([Contract(("Gold", 36))], Stock(("Gold", 100)));

        Assert.Empty(plan);
    }

    [Fact]
    public void Item_names_are_matched_regardless_of_case()
    {
        // Requirement names come from the API and the inventory is keyed by name; a casing
        // difference between two contracts must not split one item into two shopping lines.
        var plan = GatheringPlan.Build(
            [Contract(("Gold", 10)), Contract(("GOLD", 10))],
            Stock(("gold", 5)));

        var gold = Assert.Single(plan);
        Assert.Equal(20, gold.Required);
        Assert.Equal(15, gold.Outstanding);
    }

    [Fact]
    public void The_same_item_listed_twice_by_one_contract_is_still_summed()
    {
        var plan = GatheringPlan.Build([Contract(("Gold", 10), ("Gold", 6))], Stock());

        Assert.Equal(16, Assert.Single(plan).Required);
    }

    [Fact]
    public void Scu_amounts_are_summed_in_the_whole_units_completion_deducts()
    {
        // Same rule as the deduction (RequiredCount ceilings), so the list a player mines against is
        // the list completing will actually consume.
        var contract = new WikeloContract
        {
            Uuid = "m1",
            Title = "Hauling",
            Requirements =
            [
                new ContractRequirement { Name = "Gold", MinScu = 36, MaxScu = 36 },
                new ContractRequirement { Name = "Titanium", MaxScu = 1.5 },
            ],
        };

        var plan = GatheringPlan.Build([contract], Stock());

        Assert.Equal(36, plan.Single(i => i.Name == "Gold").Required);
        Assert.Equal(2, plan.Single(i => i.Name == "Titanium").Required);
    }

    [Fact]
    public void The_list_is_alphabetical_so_it_does_not_reshuffle_while_it_is_worked_through()
    {
        var plan = GatheringPlan.Build(
            [Contract(("Quantanium", 1), ("Agricium", 99), ("Gold", 5))],
            Stock());

        Assert.Equal(["Agricium", "Gold", "Quantanium"], plan.Select(i => i.Name));
    }

    [Fact]
    public void No_contracts_is_an_empty_plan_rather_than_a_failure()
    {
        Assert.Empty(GatheringPlan.Build([], Stock()));
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(9, 0.25)]
    [InlineData(36, 1.0)]
    public void Coverage_is_what_is_held_over_what_is_asked_for(int held, double expected)
    {
        var plan = GatheringPlan.Build([Contract(("Gold", 36))], Stock(("Gold", held)));

        // A fully covered item leaves the plan, so read the fraction off the item itself.
        var item = new GatheringItem { Name = "Gold", Required = 36, Have = held };

        Assert.Equal(expected, item.CoveredFraction);
        Assert.Equal(held < 36, plan.Count > 0);
    }

    [Fact]
    public void Holding_more_than_the_plan_asks_for_is_still_just_done()
    {
        // Same reason Outstanding floors at 0: a surplus is not 200% coverage, and a progress bar
        // handed 2.0 would render as a full bar with a value nothing else in the app produces.
        var item = new GatheringItem { Name = "Gold", Required = 36, Have = 72 };

        Assert.Equal(1.0, item.CoveredFraction);
        Assert.Equal(0, item.Outstanding);
    }

    [Fact]
    public void An_item_nothing_asks_for_reads_as_no_progress_rather_than_dividing_by_zero()
    {
        var item = new GatheringItem { Name = "Gold", Required = 0, Have = 5 };

        Assert.Equal(0, item.CoveredFraction);
    }
}
