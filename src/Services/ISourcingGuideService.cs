namespace WikeloContractor.Services;

/// <summary>One item's knowledge-base entry: the card's short line plus the step-by-step body.</summary>
/// <param name="Summary">Short "where to find it" line; empty when the file has none yet.</param>
/// <param name="Body">Markdown body rendered as the "How to obtain" guide; empty for a stub.</param>
/// <param name="Contract">
/// Name of the mission that yields the item, when one does. Empty for anything simply bought or
/// mined — most of the corpus — so the page hides the row rather than showing a blank label.
/// </param>
/// <param name="Faction">Who hands out that contract, when it is known. Empty far more often than
/// <paramref name="Contract"/>: a mission name is usually recorded while its client is not.</param>
/// <param name="Uuid">
/// The game's item UUID, which both outbound links are built from (<see cref="Models.ItemLinks"/>).
/// Recorded as the identifier rather than as two ready-made URLs so the shapes stay in one place.
/// </param>
/// <param name="IsVehicle">
/// The record lives in the wiki's vehicle namespace rather than its item one — true for the three
/// ATLS variants and nothing else in the corpus. Only the wiki link cares; the shop finder files
/// both under one route.
/// </param>
public sealed record SourcingGuide(
    string Summary,
    string Body,
    string Contract = "",
    string Faction = "",
    string Uuid = "",
    bool IsVehicle = false)
{
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool HasBody => !string.IsNullOrWhiteSpace(Body);

    public bool HasContract => !string.IsNullOrWhiteSpace(Contract);

    public bool HasFaction => !string.IsNullOrWhiteSpace(Faction);

    /// <summary>
    /// The game's item UUID, from the guide's front matter — the key to both outbound links
    /// (<see cref="Models.ItemLinks"/>). Empty for anything the wiki API does not know by name,
    /// which is a normal state, not a defect.
    /// </summary>
    public bool HasUuid => Models.ItemLinks.IsUuid(Uuid);
}

/// <summary>
/// The sourcing knowledge base: one Markdown file per required item, authored in
/// <c>docs/sourcing/</c> and shipped in the install directory. Two layers — the bundled files and the
/// user's own in <c>%AppData%\WikeloContractor\sourcing\</c>, which win per item and survive updates.
/// </summary>
public interface ISourcingGuideService
{
    /// <summary>The entry for an item, or null when no file names it.</summary>
    SourcingGuide? GetGuide(string itemName);
}
