namespace WikeloContractor.Models;

/// <summary>
/// The catalog's filter criteria and the single home for the "does this contract match?" decision,
/// shared by every contract list page (see <c>ViewModels/ContractListViewModel</c>).
/// <para>
/// Deliberately free of any UI notion: the pages hold combo box <em>indices</em> and translate them
/// into the values below, so this type stays a pure predicate that can be unit-tested without a
/// WPF <c>Application</c>. A second copy of this matching logic is a review finding.
/// </para>
/// </summary>
/// <param name="Search">Free text matched against title, description and reward names; null or blank matches everything.</param>
/// <param name="Category">Required reward category; null matches every category.</param>
/// <param name="ResourceName">Required item the contract must ask for; null matches every contract.</param>
public sealed record ContractFilter(string? Search, ContractCategory? Category, string? ResourceName)
{
    /// <summary>No criteria at all — every contract matches.</summary>
    public static readonly ContractFilter None = new(null, null, null);

    public bool Matches(WikeloContract contract)
    {
        if (!string.IsNullOrWhiteSpace(Search)
            && !contract.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)
            && contract.Description?.Contains(Search, StringComparison.OrdinalIgnoreCase) != true
            && !contract.Rewards.Any(r => r.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Any reward category matches (a ship contract with bonus armor shows under both).
        if (Category is { } category && !contract.EffectiveCategories.Contains(category))
        {
            return false;
        }

        if (ResourceName is { } resource
            && !contract.Requirements.Any(r => string.Equals(r.Name, resource, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
