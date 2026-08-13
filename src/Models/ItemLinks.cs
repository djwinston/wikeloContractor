namespace WikeloContractor.Models;

/// <summary>
/// The outbound links a required item can carry, built from the one identifier that unlocks both:
/// the game's item UUID, recorded in the guide's front matter.
/// <para>
/// The single home for these URL shapes. They belong to sites we do not control, so when one of them
/// changes its routing this is the one edit — not a search through ~95 Markdown files, which is
/// exactly what storing whole URLs per guide would have cost.
/// </para>
/// <para>
/// Pure and dependency-free, so the shapes are unit-testable without a window or a network.
/// </para>
/// </summary>
public static class ItemLinks
{
    /// <summary>
    /// The item's page on star-citizen.wiki — description, stats, images and crafting data.
    /// <para>
    /// The API's own <c>web_url</c> field points here as <c>/items/{slug}</c>, but the same page
    /// answers to the UUID, which is why one identifier is enough for both links.
    /// </para>
    /// <para>
    /// The wiki keeps vehicles in their own namespace, and <c>/items/{vehicle-uuid}</c> answers with
    /// a redirect rather than the record — so the three ATLS variants in the corpus set
    /// <c>vehicle: true</c> in their front matter. The finder below needs no such split: it files
    /// items and vehicles under one route.
    /// </para>
    /// </summary>
    public static string? Wiki(string? uuid, bool isVehicle = false) =>
        IsUuid(uuid)
            ? $"https://api.star-citizen.wiki/{(isVehicle ? "vehicles" : "items")}/{uuid}"
            : null;

    /// <summary>
    /// The item's page on the cstone.space Universal Item Finder — a crowdsourced item database
    /// which, <em>for things that are sold</em>, also lists which shops stock them and at what
    /// price. Most of this corpus is mission loot and has no shop entry at all, which is why the
    /// button that opens this is named for the destination rather than for a shop list.
    /// <para>
    /// This is why the UUID is worth recording. The finder has no name search in its URL (its inline
    /// script never reads the query string, and a name in the path redirects to the home page), so
    /// the only per-item address it has is <c>/Search/{uuid}</c> — and that UUID is the game's own,
    /// the same one the wiki API returns. Without it the guides could only send the player to the
    /// site's front door to retype a name the app already knows.
    /// </para>
    /// </summary>
    /// <param name="category">
    /// What the item is. <see cref="InventoryCategory.OreMineral"/> yields no link at all: the
    /// finder is an item database and does not carry mineable ores, so <c>/Search/{uuid}</c> for one
    /// quietly redirects to its home page. Measured across the whole corpus, not assumed — every one
    /// of the ten ores bounced, and every one of the eighty-three non-ores resolved but a single
    /// consumable. A button that lands on a search box the player must now fill in by hand is the
    /// same broken promise as a mislabelled one.
    /// </param>
    public static string? ShopFinder(string? uuid, InventoryCategory category = InventoryCategory.Other) =>
        IsUuid(uuid) && category != InventoryCategory.OreMineral
            ? $"https://finder.cstone.space/Search/{uuid}"
            : null;

    /// <summary>
    /// Whether the front matter's value is a UUID at all. Hand-authored files get typos, and a
    /// malformed value must render no button rather than a link to a 404 — the same instinct as the
    /// guides' "an empty section beats a fabricated one" rule.
    /// </summary>
    public static bool IsUuid(string? value) => Guid.TryParseExact(value, "D", out _);
}
