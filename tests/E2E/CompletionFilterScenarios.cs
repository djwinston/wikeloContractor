using WikeloContractor.Models;
using WikeloContractor.Models.Api;
using WikeloContractor.ViewModels;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The completed / not completed filter, on both list pages.
/// <para>
/// <see cref="Tests.Models.ContractFilterTests"/> covers the predicate. What matters here is the
/// wiring around it: the filter is one axis of the shared <see cref="ContractFilter"/>, so both
/// pages get it from the same base VM, and — the part that is easy to get wrong — a contract
/// completed while the filter is active has to leave the list without the user re-navigating.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class CompletionFilterScenarios(WpfAppFixture app)
{
    private const int Any = 0;
    private const int NotCompleted = 1;
    private const int Completed = 2;

    private static ScriptedWikiApi TwoContracts()
    {
        var api = new ScriptedWikiApi
        {
            Missions =
            [
                ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special"),
                ScriptedWikiApi.Mission("m2", "Testudo Armor Trade"),
            ],
        };

        // Requirements only arrive with enrichment, and the gathering-plan case below needs a
        // non-empty plan to be worth asserting on.
        api.MissionDetails["m1"] = new MissionDetailDto
        {
            Uuid = "m1",
            HaulingOrders = [new HaulingOrderDto { Name = "Gold", MinScu = 36, MaxScu = 36 }],
        };
        api.MissionDetails["m2"] = new MissionDetailDto
        {
            Uuid = "m2",
            HaulingOrders = [new HaulingOrderDto { Name = "Carinite (Pure)", MaxAmount = 4 }],
        };

        return api;
    }

    private async Task<CatalogHarness> ReadyAsync()
    {
        var harness = await CatalogHarness.CreateAsync(app, TwoContracts());

        await harness.LoadAndEnrichAsync();

        await app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());
        await app.OnUiAsync(() => harness.Favorited.OnNavigatedToAsync());

        return harness;
    }

    private static IReadOnlyList<string> Titles(ContractListViewModel page) =>
        [.. page.Contracts!.Cast<ContractCardViewModel>().Select(card => card.Contract.Title)];

    [Fact]
    public async Task Not_completed_hides_what_is_already_done()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            var done = harness.Catalog.Current!.Contracts.Single(c => c.Uuid == "m2");
            await harness.Completion.SetCompletedAsync(done, true);
        });

        await app.OnUiAsync(() =>
        {
            harness.Catalogue.CompletionIndex = NotCompleted;
            Assert.Equal(["Asgard Wikelo War Special"], Titles(harness.Catalogue));

            harness.Catalogue.CompletionIndex = Completed;
            Assert.Equal(["Testudo Armor Trade"], Titles(harness.Catalogue));

            harness.Catalogue.CompletionIndex = Any;
            Assert.Equal(2, Titles(harness.Catalogue).Count);
        });
    }

    [Fact]
    public async Task Completing_a_contract_under_the_filter_removes_its_row_there_and_then()
    {
        // The regression this exists for: the filter reads a card property the collection view
        // cannot observe, so without a refresh on the completion event the row would sit there
        // looking un-filtered until something else happened to re-run the filter.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Catalogue.CompletionIndex = NotCompleted;
            Assert.Equal(2, Titles(harness.Catalogue).Count);
        });

        await app.OnUiAsync(async () =>
        {
            var done = harness.Catalog.Current!.Contracts.Single(c => c.Uuid == "m2");
            await harness.Completion.SetCompletedAsync(done, true);
        });

        await app.OnUiAsync(() => Assert.Equal(["Asgard Wikelo War Special"], Titles(harness.Catalogue)));
    }

    [Fact]
    public async Task The_favorites_page_filters_by_completion_from_the_same_base_view_model()
    {
        // A starred contract stays starred once it is done — this is how a working library stops
        // filling up with finished rows, without un-starring anything.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Favorites.SetFavoriteAsync("m2", true);

            var done = harness.Catalog.Current!.Contracts.Single(c => c.Uuid == "m2");
            await harness.Completion.SetCompletedAsync(done, true);
        });

        await app.OnUiAsync(() =>
        {
            harness.Favorited.CompletionIndex = NotCompleted;

            Assert.Equal(["Asgard Wikelo War Special"], Titles(harness.Favorited));

            // Still starred: hidden from the list, not dropped from the favorites store.
            Assert.True(harness.Favorites.IsFavorite("m2"));
        });
    }

    [Fact]
    public async Task Clearing_the_filters_resets_the_completion_state_too()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            var done = harness.Catalog.Current!.Contracts.Single(c => c.Uuid == "m2");
            await harness.Completion.SetCompletedAsync(done, true);
        });

        await app.OnUiAsync(() =>
        {
            harness.Catalogue.CompletionIndex = Completed;
            harness.Catalogue.ClearFiltersCommand.Execute(null);

            Assert.Equal(Any, harness.Catalogue.CompletionIndex);
            Assert.Equal(2, Titles(harness.Catalogue).Count);
        });
    }

    [Fact]
    public async Task The_gathering_plan_ignores_the_completion_filter_like_every_other_one()
    {
        // The plan already excludes completed contracts by its own rule; what it must not do is
        // also follow the *filter*, which is a way to find a row, not a statement about the plan.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Favorites.SetFavoriteAsync("m2", true);
        });

        await app.OnUiAsync(() =>
        {
            var before = harness.Favorited.Gathering.Count;

            harness.Favorited.CompletionIndex = Completed;

            Assert.Empty(Titles(harness.Favorited));
            Assert.Equal(before, harness.Favorited.Gathering.Count);
        });
    }
}
