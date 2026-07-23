namespace WikeloContractor.Services;

/// <summary>One item's knowledge-base entry: the card's short line plus the step-by-step body.</summary>
/// <param name="Summary">Short "where to find it" line; empty when the file has none yet.</param>
/// <param name="Body">Markdown body rendered as the "How to obtain" guide; empty for a stub.</param>
public sealed record SourcingGuide(string Summary, string Body)
{
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool HasBody => !string.IsNullOrWhiteSpace(Body);
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
