using WikeloContractor.Models;
using WikeloContractor.Models.Api;
using WikeloContractor.ViewModels;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The gathering plan on the Favorites page: what every starred contract still needs, between them.
/// <para>
/// <see cref="Tests.Models.GatheringPlanTests"/> covers the arithmetic. What is worth exercising here
/// is everything around it — which contracts get counted, and whether the number follows the four
/// things that can move it (starring, completing, an inventory edit, enrichment) without the user
/// leaving the page.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class GatheringScenarios(WpfAppFixture app)
{
    /// <summary>Two contracts that overlap on Gold — the case a per-contract chip cannot express.</summary>
    private static ScriptedWikiApi MiningCatalog()
    {
        var api = new ScriptedWikiApi
        {
            Missions =
            [
                ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special"),
                ScriptedWikiApi.Mission("m2", "Testudo Armor Trade"),
            ],
        };

        api.MissionDetails["m1"] = new MissionDetailDto
        {
            Uuid = "m1",
            HaulingOrders =
            [
                new HaulingOrderDto { Name = "Gold", MinScu = 36, MaxScu = 36 },
                new HaulingOrderDto { Name = "Carinite (Pure)", MaxAmount = 4 },
            ],
        };
        api.MissionDetails["m2"] = new MissionDetailDto
        {
            Uuid = "m2",
            HaulingOrders = [new HaulingOrderDto { Name = "Gold", MinScu = 36, MaxScu = 36 }],
        };

        return api;
    }

    /// <summary>Loads the catalog, lets enrichment settle, and opens the Favorites page.</summary>
    private async Task<CatalogHarness> ReadyAsync()
    {
        var harness = await CatalogHarness.CreateAsync(app, MiningCatalog());

        await harness.LoadAndEnrichAsync();

        await app.OnUiAsync(() => harness.Favorited.OnNavigatedToAsync());
        return harness;
    }

    private static GatheringRowViewModel? Line(CatalogHarness harness, string name) =>
        harness.Favorited.Gathering.FirstOrDefault(row => row.Name == name);

    [Fact]
    public async Task Two_starred_contracts_needing_the_same_item_ask_for_it_once_and_in_full()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Favorites.SetFavoriteAsync("m2", true);
        });

        await app.OnUiAsync(() =>
        {
            // 36 + 36: one inventory pool serves both, which is the whole point of the panel.
            Assert.Equal("0 / 72", Line(harness, "Gold")!.StockLabel);
            Assert.Equal("0 / 4", Line(harness, "Carinite (Pure)")!.StockLabel);
            Assert.True(harness.Favorited.HasGatheringPlan);
            Assert.False(harness.Favorited.HasNothingToGather);
        });
    }

    [Fact]
    public async Task Nothing_starred_shows_no_panel_at_all()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            Assert.Empty(harness.Favorited.Gathering);
            Assert.False(harness.Favorited.HasGatheringPlan);

            // Not "you have everything" either — with nothing starred the page's own empty state
            // speaks, and a reassurance under it would be answering a question nobody asked.
            Assert.False(harness.Favorited.HasNothingToGather);
        });
    }

    [Fact]
    public async Task Completing_a_contract_takes_its_items_out_of_the_plan()
    {
        // The correctness of the whole feature: completing already deducted those items from the
        // inventory, so counting them again would send the player out for things they handed over.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Favorites.SetFavoriteAsync("m2", true);
        });

        await app.OnUiAsync(() => Assert.Equal("0 / 72", Line(harness, "Gold")!.StockLabel));

        await app.OnUiAsync(async () =>
        {
            var completed = harness.Catalog.Current!.Contracts.Single(c => c.Uuid == "m2");
            await harness.Completion.SetCompletedAsync(completed, true);
        });

        await app.OnUiAsync(() =>
        {
            Assert.Equal("0 / 36", Line(harness, "Gold")!.StockLabel);

            // Still starred, so the card stays on the page — it just stops being shopping.
            Assert.Equal(2, harness.Favorited.Contracts!.Cast<object>().Count());
        });
    }

    [Fact]
    public async Task Stocking_the_inventory_moves_the_shortfall_without_leaving_the_page()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Inventory.SetCountAsync("Gold", 30);
        });

        await app.OnUiAsync(() =>
        {
            var gold = Line(harness, "Gold")!;

            // The card states the stock against the requirement and leaves the shortfall as the gap
            // between them — 6, here — rather than spelling it out a second time.
            Assert.Equal("30 / 36", gold.StockLabel);
            Assert.Equal(30d / 36, gold.Progress, precision: 6);
        });
    }

    [Fact]
    public async Task A_fully_stocked_plan_says_so_over_a_list_of_gathered_cards()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Inventory.SetCountAsync("Gold", 40);
            await harness.Inventory.SetCountAsync("Carinite (Pure)", 4);
        });

        await app.OnUiAsync(() =>
        {
            // The rows stay — the badge is what goes to zero.
            Assert.Equal(2, harness.Favorited.Gathering.Count);
            Assert.All(harness.Favorited.Gathering, row => Assert.True(row.IsCovered));
            Assert.Equal(0, harness.Favorited.OutstandingCount);
            Assert.True(harness.Favorited.HasNothingToGather);
            Assert.True(harness.Favorited.HasGatheringPlan);
        });
    }

    [Fact]
    public async Task An_item_the_inventory_covers_stays_on_the_list_with_its_pin()
    {
        // The bug this replaced: the row vanished the moment its last unit was counted, so a pinned
        // item lost the only place it could be unpinned from while the budget went on counting it.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await Line(harness, "Gold")!.Pin.ToggleCommand.ExecuteAsync(null);
            await harness.Inventory.SetCountAsync("Gold", 36);
        });

        await app.OnUiAsync(() =>
        {
            var gold = Line(harness, "Gold")!;

            Assert.True(gold.IsCovered);
            Assert.Equal("36 / 36", gold.StockLabel);
            Assert.True(gold.Pin.IsPinned);

            // One item short of the two, so the badge counts one.
            Assert.Equal(1, harness.Favorited.OutstandingCount);
            Assert.False(harness.Favorited.HasNothingToGather);
        });
    }

    [Fact]
    public async Task The_tab_filter_splits_the_plan_without_changing_it()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Inventory.SetCountAsync("Gold", 36);
        });

        await app.OnUiAsync(() =>
        {
            harness.Favorited.GatheringFilterIndex = 1;
            Assert.Equal(["Gold"], harness.Favorited.GatheringView.Cast<GatheringRowViewModel>().Select(r => r.Name));

            harness.Favorited.GatheringFilterIndex = 2;
            Assert.Equal(
                ["Carinite (Pure)"],
                harness.Favorited.GatheringView.Cast<GatheringRowViewModel>().Select(r => r.Name));

            harness.Favorited.GatheringFilterIndex = 0;
            Assert.Equal(2, harness.Favorited.GatheringView.Cast<GatheringRowViewModel>().Count());

            // The source never moved — the filter is a way to look at the plan, not part of it.
            Assert.Equal(2, harness.Favorited.Gathering.Count);
        });
    }

    [Fact]
    public async Task Finishing_an_item_drops_it_out_of_the_not_gathered_filter_right_away()
    {
        // The row is reconciled in place rather than re-added, so nothing else raises a change the
        // collection view could notice — the coverage flip has to refresh it.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Inventory.SetCountAsync("Gold", 30);
        });

        await app.OnUiAsync(() => harness.Favorited.GatheringFilterIndex = 2);

        await app.OnUiAsync(() => harness.Inventory.SetCountAsync("Gold", 36));

        await app.OnUiAsync(() =>
        {
            Assert.DoesNotContain(
                harness.Favorited.GatheringView.Cast<GatheringRowViewModel>(),
                row => row.Name == "Gold");
            Assert.NotNull(Line(harness, "Gold"));
        });
    }

    [Fact]
    public async Task A_filter_that_matches_nothing_says_so_rather_than_showing_a_blank_tab()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() => harness.Favorites.SetFavoriteAsync("m1", true));

        await app.OnUiAsync(() =>
        {
            Assert.False(harness.Favorited.IsGatheringEmpty);

            // Nothing is gathered yet, so "Gathered" is empty while the plan itself is not.
            harness.Favorited.GatheringFilterIndex = 1;
            Assert.True(harness.Favorited.IsGatheringEmpty);
        });
    }

    [Fact]
    public async Task Un_starring_a_contract_removes_what_only_it_needed()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Favorites.SetFavoriteAsync("m2", true);
            await harness.Favorites.SetFavoriteAsync("m1", false);
        });

        await app.OnUiAsync(() =>
        {
            Assert.Null(Line(harness, "Carinite (Pure)"));
            Assert.Equal("0 / 36", Line(harness, "Gold")!.StockLabel);
        });
    }

    [Fact]
    public async Task Pinning_from_the_plan_reaches_the_overlay_and_shows_the_digit_to_press()
    {
        // The step this removes: reading the shortfall here, then going to the Inventory page to
        // find each name again just to pin it.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await Line(harness, "Gold")!.Pin.ToggleCommand.ExecuteAsync(null);
        });

        await app.OnUiAsync(() =>
        {
            Assert.True(Line(harness, "Gold")!.Pin.IsPinned);
            Assert.Equal("1", Line(harness, "Gold")!.Pin.SlotLabel);
            Assert.Equal("Gold", harness.Hud.SlotAt(1)!.Name);
        });
    }

    [Fact]
    public async Task A_pin_made_on_the_inventory_page_shows_up_here_too()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            _ = await harness.Pins.PinAsync("Carinite (Pure)");
        });

        await app.OnUiAsync(() =>
        {
            // Refreshed in place, not rebuilt — the plan itself did not move.
            Assert.True(Line(harness, "Carinite (Pure)")!.Pin.IsPinned);
            Assert.False(Line(harness, "Gold")!.Pin.IsPinned);
        });
    }

    [Fact]
    public async Task A_full_overlay_greys_out_the_remaining_pin_buttons()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);

            for (var i = 1; i <= OverlaySlots.MaxSlots; i++)
            {
                _ = await harness.Pins.PinAsync($"Filler {i}");
            }
        });

        await app.OnUiAsync(() =>
        {
            var gold = Line(harness, "Gold")!;

            Assert.False(gold.Pin.CanPin, "the button must stop inviting a click the overlay will refuse");
            Assert.False(gold.Pin.ToggleCommand.CanExecute(null));
        });
    }

    [Fact]
    public async Task The_budget_counter_is_the_same_object_the_inventory_page_shows()
    {
        // One set of pins, one counter. Two would only be two things to keep in step.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await Line(harness, "Gold")!.Pin.ToggleCommand.ExecuteAsync(null);
        });

        await app.OnUiAsync(() =>
        {
            Assert.Same(harness.Favorited.OverlayPins, harness.Inventoried.OverlayPins);
            Assert.Contains($"1/{OverlaySlots.MaxSlots}", harness.Favorited.OverlayPins.Summary);
            Assert.True(harness.Favorited.OverlayPins.ClearCommand.CanExecute(null));
        });
    }

    [Fact]
    public async Task Unpinning_from_the_plan_frees_the_slot_again()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);

            var gold = Line(harness, "Gold")!;
            await gold.Pin.ToggleCommand.ExecuteAsync(null);
            await gold.Pin.ToggleCommand.ExecuteAsync(null);
        });

        await app.OnUiAsync(() =>
        {
            Assert.False(Line(harness, "Gold")!.Pin.IsPinned);
            Assert.Empty(harness.Hud.Slots);
        });
    }

    [Fact]
    public async Task The_plan_ignores_the_pages_filters()
    {
        // The filters are a way to find a row. The plan answers "what does my whole starred set
        // still need", and a shopping list that changes because a search box has text in it is not one.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            await harness.Favorites.SetFavoriteAsync("m1", true);
            await harness.Favorites.SetFavoriteAsync("m2", true);
        });

        await app.OnUiAsync(() =>
        {
            harness.Favorited.SearchText = "Testudo";

            Assert.Single(harness.Favorited.Contracts!.Cast<object>());
            Assert.Equal("0 / 72", Line(harness, "Gold")!.StockLabel);
            Assert.NotNull(Line(harness, "Carinite (Pure)"));
        });
    }
}
