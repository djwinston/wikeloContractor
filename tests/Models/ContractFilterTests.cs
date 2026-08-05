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

    /// <summary>
    /// Applies a filter to a contract that is not completed — the state every case outside the
    /// completion section is about. Spelling the flag out at each of them would only bury the one
    /// criterion the case actually exercises.
    /// </summary>
    private static bool MatchesOpen(ContractFilter filter, WikeloContract contract) =>
        filter.Matches(contract, isCompleted: false);

    [Fact]
    public void No_criteria_matches_everything() =>
        Assert.True(MatchesOpen(ContractFilter.None, Contract()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_search_matches_everything(string? search) =>
        Assert.True(MatchesOpen(new ContractFilter(search, null, null), Contract()));

    [Fact]
    public void Search_matches_the_title_case_insensitively() =>
        Assert.True(MatchesOpen(
            new ContractFilter("WIKELO", null, null), Contract(title: "Deliver ore to Wikelo")));

    [Fact]
    public void Search_matches_the_description() =>
        Assert.True(MatchesOpen(
            new ContractFilter("smuggled", null, null), Contract(description: "Bring the smuggled cargo")));

    [Fact]
    public void Search_matches_a_reward_name() =>
        Assert.True(MatchesOpen(
            new ContractFilter("Endro", null, null), Contract(rewards: ["Ana Arms Endro"])));

    [Fact]
    public void Search_that_matches_nothing_excludes_the_contract() =>
        Assert.False(MatchesOpen(
            new ContractFilter("Drake", null, null),
            Contract(title: "Deliver ore", description: "Bring gold", rewards: ["Ana Arms Endro"])));

    [Fact]
    public void A_null_description_does_not_throw() =>
        Assert.False(MatchesOpen(new ContractFilter("anything", null, null), Contract(description: null)));

    [Fact]
    public void Category_matches_the_primary_category_before_enrichment() =>
        Assert.True(MatchesOpen(
            new ContractFilter(null, ContractCategory.Ship, null),
            Contract(category: ContractCategory.Ship, categories: [])));

    [Fact]
    public void Category_matches_any_of_the_enriched_categories()
    {
        // A ship contract with bonus armor shows under both filters.
        var contract = Contract(
            category: ContractCategory.Ship,
            categories: [ContractCategory.Ship, ContractCategory.Armor]);

        Assert.True(MatchesOpen(new ContractFilter(null, ContractCategory.Armor, null), contract));
        Assert.True(MatchesOpen(new ContractFilter(null, ContractCategory.Ship, null), contract));
    }

    [Fact]
    public void A_non_matching_category_excludes_the_contract() =>
        Assert.False(MatchesOpen(
            new ContractFilter(null, ContractCategory.Weapon, null),
            Contract(category: ContractCategory.Ship, categories: [ContractCategory.Ship])));

    [Fact]
    public void Resource_matches_a_required_item_case_insensitively() =>
        Assert.True(MatchesOpen(
            new ContractFilter(null, null, "carinite (pure)"),
            Contract(requirements: ["Carinite (Pure)", "Gold"])));

    [Fact]
    public void A_non_required_resource_excludes_the_contract() =>
        Assert.False(MatchesOpen(
            new ContractFilter(null, null, "Quantanium"), Contract(requirements: ["Gold"])));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_null_completion_matches_either_state(bool isCompleted) =>
        Assert.True(ContractFilter.None.Matches(Contract(), isCompleted));

    [Fact]
    public void Completion_keeps_only_the_state_asked_for()
    {
        var contract = Contract();
        var completedOnly = new ContractFilter(null, null, null, Completed: true);
        var openOnly = new ContractFilter(null, null, null, Completed: false);

        Assert.True(completedOnly.Matches(contract, isCompleted: true));
        Assert.False(completedOnly.Matches(contract, isCompleted: false));

        Assert.True(openOnly.Matches(contract, isCompleted: false));
        Assert.False(openOnly.Matches(contract, isCompleted: true));
    }

    [Fact]
    public void All_criteria_must_hold_together()
    {
        var contract = Contract(
            title: "Deliver ore to Wikelo",
            category: ContractCategory.Ship,
            categories: [ContractCategory.Ship],
            requirements: ["Gold"]);

        Assert.True(MatchesOpen(new ContractFilter("Wikelo", ContractCategory.Ship, "Gold"), contract));
        // Same filter, one criterion flipped — the whole thing must fail.
        Assert.False(MatchesOpen(new ContractFilter("Wikelo", ContractCategory.Ship, "Quantanium"), contract));
        Assert.False(MatchesOpen(new ContractFilter("Wikelo", ContractCategory.Armor, "Gold"), contract));
        Assert.False(MatchesOpen(new ContractFilter("Drake", ContractCategory.Ship, "Gold"), contract));

        // And completion ANDs with the rest rather than overriding it: a filter every other
        // criterion satisfies still fails on the state.
        var open = new ContractFilter("Wikelo", ContractCategory.Ship, "Gold", Completed: false);
        Assert.True(open.Matches(contract, isCompleted: false));
        Assert.False(open.Matches(contract, isCompleted: true));
    }
}
