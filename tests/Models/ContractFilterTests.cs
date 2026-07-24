using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// The catalog/favorites filter predicate. Pure by design — no WPF <c>Application</c> and no
/// ViewModel needed, which is the point of keeping the decision out of the page VM.
/// </summary>
public class ContractFilterTests
{
    private static WikeloContract Contract(
        string title = "Deliver ore to Wikelo",
        string? description = null,
        ContractCategory category = ContractCategory.Ship,
        IReadOnlyList<ContractCategory>? categories = null,
        IReadOnlyList<string>? requirements = null,
        IReadOnlyList<string>? rewards = null) =>
        new()
        {
            Uuid = "uuid-1",
            Title = title,
            Description = description,
            Category = category,
            Categories = categories ?? [],
            Requirements = (requirements ?? ["Gold"])
                .Select(n => new ContractRequirement { Name = n, MaxAmount = 1 })
                .ToList(),
            Rewards = (rewards ?? [])
                .Select(n => new ContractReward { Name = n })
                .ToList(),
        };

    [Fact]
    public void No_criteria_matches_everything() =>
        Assert.True(ContractFilter.None.Matches(Contract()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_search_matches_everything(string? search) =>
        Assert.True(new ContractFilter(search, null, null).Matches(Contract()));

    [Fact]
    public void Search_matches_the_title_case_insensitively() =>
        Assert.True(new ContractFilter("WIKELO", null, null).Matches(Contract(title: "Deliver ore to Wikelo")));

    [Fact]
    public void Search_matches_the_description() =>
        Assert.True(new ContractFilter("smuggled", null, null)
            .Matches(Contract(description: "Bring the smuggled cargo")));

    [Fact]
    public void Search_matches_a_reward_name() =>
        Assert.True(new ContractFilter("Endro", null, null)
            .Matches(Contract(rewards: ["Ana Arms Endro"])));

    [Fact]
    public void Search_that_matches_nothing_excludes_the_contract() =>
        Assert.False(new ContractFilter("Drake", null, null)
            .Matches(Contract(title: "Deliver ore", description: "Bring gold", rewards: ["Ana Arms Endro"])));

    [Fact]
    public void A_null_description_does_not_throw() =>
        Assert.False(new ContractFilter("anything", null, null).Matches(Contract(description: null)));

    [Fact]
    public void Category_matches_the_primary_category_before_enrichment() =>
        Assert.True(new ContractFilter(null, ContractCategory.Ship, null)
            .Matches(Contract(category: ContractCategory.Ship, categories: [])));

    [Fact]
    public void Category_matches_any_of_the_enriched_categories()
    {
        // A ship contract with bonus armor shows under both filters.
        var contract = Contract(
            category: ContractCategory.Ship,
            categories: [ContractCategory.Ship, ContractCategory.Armor]);

        Assert.True(new ContractFilter(null, ContractCategory.Armor, null).Matches(contract));
        Assert.True(new ContractFilter(null, ContractCategory.Ship, null).Matches(contract));
    }

    [Fact]
    public void A_non_matching_category_excludes_the_contract() =>
        Assert.False(new ContractFilter(null, ContractCategory.Weapon, null)
            .Matches(Contract(category: ContractCategory.Ship, categories: [ContractCategory.Ship])));

    [Fact]
    public void Resource_matches_a_required_item_case_insensitively() =>
        Assert.True(new ContractFilter(null, null, "carinite (pure)")
            .Matches(Contract(requirements: ["Carinite (Pure)", "Gold"])));

    [Fact]
    public void A_non_required_resource_excludes_the_contract() =>
        Assert.False(new ContractFilter(null, null, "Quantanium")
            .Matches(Contract(requirements: ["Gold"])));

    [Fact]
    public void All_criteria_must_hold_together()
    {
        var contract = Contract(
            title: "Deliver ore to Wikelo",
            category: ContractCategory.Ship,
            categories: [ContractCategory.Ship],
            requirements: ["Gold"]);

        Assert.True(new ContractFilter("Wikelo", ContractCategory.Ship, "Gold").Matches(contract));
        // Same filter, one criterion flipped — the whole thing must fail.
        Assert.False(new ContractFilter("Wikelo", ContractCategory.Ship, "Quantanium").Matches(contract));
        Assert.False(new ContractFilter("Wikelo", ContractCategory.Armor, "Gold").Matches(contract));
        Assert.False(new ContractFilter("Drake", ContractCategory.Ship, "Gold").Matches(contract));
    }
}
