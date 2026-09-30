using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// When opening a list page rebuilds its rows. A rebuild is a new collection view, and a new view is
/// every row container torn down and recreated — ~0.7 s on the catalog — so a visit over unchanged
/// data must keep what is there, and anything that did change must still come through. The view
/// instance is the observable: a rebuild is exactly "a different view object".
/// </summary>
[Collection("WpfApp")]
public sealed class ListRebuildScenarios
{
    private readonly WpfAppFixture _app;

    public ListRebuildScenarios(WpfAppFixture app) => _app = app;

    private static ScriptedWikiApi TwoContracts() => new()
    {
        Missions =
        [
            ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special"),
            ScriptedWikiApi.Mission("m2", "Scorpius Wikelo Sneak Special"),
        ],
    };

    [Fact]
    public async Task Reopening_the_catalog_over_unchanged_data_keeps_its_cards()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, TwoContracts());
        await harness.LoadAndEnrichAsync();
        await _app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());
        var shown = await _app.OnUiAsync(() => harness.Catalogue.Contracts);

        await _app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());

        await _app.OnUiAsync(() => Assert.Same(shown, harness.Catalogue.Contracts));
    }

    // Cards bake localized text in when they are built (category tag, XP badge, "All resources"), so
    // the unchanged-data shortcut must not survive a language switch made on the Settings page.
    [Fact]
    public async Task A_language_switch_rebuilds_the_catalog_on_the_next_visit()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, TwoContracts());
        await harness.LoadAndEnrichAsync();
        await _app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());
        var shown = await _app.OnUiAsync(() => harness.Catalogue.Contracts);

        harness.Localization.ApplyLanguage("uk");
        await _app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());

        await _app.OnUiAsync(() =>
        {
            Assert.NotSame(shown, harness.Catalogue.Contracts);
            Assert.Equal(2, harness.Catalogue.Contracts!.Cast<object>().Count());
        });
    }

    // Favorites builds a fresh filtered list on every visit, so "unchanged" has to mean the same
    // contract instances in the same order — not the same list object.
    [Fact]
    public async Task Reopening_favorites_keeps_its_cards_and_a_new_star_still_shows()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, TwoContracts());
        await harness.LoadAndEnrichAsync();
        await harness.Favorites.SetFavoriteAsync("m1", true);

        await _app.OnUiAsync(() => harness.Favorited.OnNavigatedTo());
        var shown = await _app.OnUiAsync(() => harness.Favorited.Contracts);
        await _app.OnUiAsync(() => harness.Favorited.OnNavigatedTo());
        await _app.OnUiAsync(() => Assert.Same(shown, harness.Favorited.Contracts));

        await harness.Favorites.SetFavoriteAsync("m2", true);

        await _app.WaitUntilAsync(
            () => harness.Favorited.Contracts!.Cast<object>().Count() == 2,
            "the second starred contract to appear");
    }

    [Fact]
    public async Task Reopening_the_inventory_keeps_its_rows_until_the_language_changes()
    {
        using var harness = await CatalogHarness.CreateAsync(_app, TwoContracts());
        await harness.LoadAndEnrichAsync();
        await _app.OnUiAsync(() => harness.Inventoried.OnNavigatedToAsync());
        var shown = await _app.OnUiAsync(() => harness.Inventoried.Items);

        await _app.OnUiAsync(() => harness.Inventoried.OnNavigatedToAsync());
        await _app.OnUiAsync(() => Assert.Same(shown, harness.Inventoried.Items));

        // The group headers are the rows' localized category labels.
        harness.Localization.ApplyLanguage("uk");
        await _app.OnUiAsync(() => harness.Inventoried.OnNavigatedToAsync());
        await _app.OnUiAsync(() => Assert.NotSame(shown, harness.Inventoried.Items));
    }
}
