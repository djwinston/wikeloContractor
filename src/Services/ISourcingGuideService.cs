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
public sealed record SourcingGuide(string Summary, string Body, string Contract = "", string Faction = "")
{
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool HasBody => !string.IsNullOrWhiteSpace(Body);

    public bool HasContract => !string.IsNullOrWhiteSpace(Contract);

    public bool HasFaction => !string.IsNullOrWhiteSpace(Faction);
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
