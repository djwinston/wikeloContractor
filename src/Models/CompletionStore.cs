namespace WikeloContractor.Models;

/// <summary>
/// Persisted record of contracts the user has marked completed, saved to
/// <c>%AppData%\WikeloContractor\completed.json</c>. The reputation earned is stored alongside each
/// UUID so the running total stays correct even when a contract rotates out of the catalog across
/// patches (earned Wikelo standing is permanent in game).
/// </summary>
public sealed class CompletionStore
{
    /// <summary>Contract UUID → the reputation amount earned when it was marked completed.</summary>
    public Dictionary<string, int> Completed { get; set; } = new();
}
