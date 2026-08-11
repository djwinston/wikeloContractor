using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// The two outbound URL shapes. They belong to sites we do not control, so the value of pinning them
/// here is not the string itself — it is that a change to either one has exactly one place to land.
/// </summary>
public sealed class ItemLinksTests
{
    private const string _killshotRifle = "c098e722-902a-435b-83f8-a96cec36a012";

    [Fact]
    public void An_item_links_to_its_wiki_page()
    {
        Assert.Equal(
            $"https://api.star-citizen.wiki/items/{_killshotRifle}",
            ItemLinks.Wiki(_killshotRifle));
    }

    [Fact]
    public void A_vehicle_links_into_the_wikis_other_namespace()
    {
        // /items/{vehicle-uuid} answers with a redirect rather than the record, which is why the
        // three ATLS guides carry `vehicle: true`.
        Assert.Equal(
            $"https://api.star-citizen.wiki/vehicles/{_killshotRifle}",
            ItemLinks.Wiki(_killshotRifle, isVehicle: true));
    }

    [Fact]
    public void The_shop_finder_files_items_and_vehicles_under_one_route()
    {
        // The reason a UUID is recorded at all: the finder has no name search in its URL, so this is
        // the only per-item address it has.
        Assert.Equal(
            $"https://finder.cstone.space/Search/{_killshotRifle}",
            ItemLinks.ShopFinder(_killshotRifle));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uuid")]
    [InlineData("c098e722902a435b83f8a96cec36a012")]      // no dashes: not the "D" format
    [InlineData("{c098e722-902a-435b-83f8-a96cec36a012}")] // braces: the "B" format
    public void A_value_that_is_not_a_plain_uuid_yields_no_link_at_all(string? value)
    {
        // Hand-authored files get typos, and a malformed value must render no button rather than one
        // that opens a 404 — the same instinct as the guides' "an empty section beats a made-up one".
        Assert.Null(ItemLinks.Wiki(value));
        Assert.Null(ItemLinks.ShopFinder(value));
        Assert.False(ItemLinks.IsUuid(value));
    }

    [Fact]
    public void Casing_and_surrounding_whitespace_are_the_authors_business_not_the_readers()
    {
        // The service trims before this is reached; uppercase is a legitimate way to write a UUID.
        Assert.True(ItemLinks.IsUuid(_killshotRifle.ToUpperInvariant()));
        Assert.NotNull(ItemLinks.ShopFinder(_killshotRifle.ToUpperInvariant()));
    }
}
