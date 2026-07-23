namespace WikeloContractor.Services;

/// <summary>
/// Tracks which contracts the user flagged as favorites. Backed by
/// <c>%AppData%\WikeloContractor\favorites.json</c>; state is keyed by contract UUID so it survives
/// the enrichment cycle that rebuilds contract records.
/// </summary>
public interface IFavoritesService
{
    /// <summary>Loads the persisted store; call once at startup before the catalog is shown.</summary>
    Task LoadAsync();

    bool IsFavorite(string uuid);

    /// <summary>Flags or unflags a contract, then persists. A no-op change raises nothing.</summary>
    Task SetFavoriteAsync(string uuid, bool favorite);

    /// <summary>How many contracts are flagged.</summary>
    int Count { get; }

    /// <summary>Raised after the set of favorites changes.</summary>
    event EventHandler? Changed;
}
