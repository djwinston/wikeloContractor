namespace WikeloContractor.Models;

/// <summary>
/// Persisted set of contracts the user flagged as favorites, saved to
/// <c>%AppData%\WikeloContractor\favorites.json</c>. Keyed by contract UUID (not title) so the flag
/// survives the enrichment cycle that rebuilds contract records, exactly like
/// <see cref="CompletionStore"/>.
/// </summary>
public sealed class FavoritesStore
{
    /// <summary>UUIDs of the flagged contracts.</summary>
    public HashSet<string> Favorites { get; set; } = [];
}
