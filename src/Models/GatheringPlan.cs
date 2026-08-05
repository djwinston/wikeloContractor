namespace WikeloContractor.Models;

/// <summary>One line of the gathering plan: an item still missing, and by how much.</summary>
public sealed record GatheringItem
{
    public required string Name { get; init; }

    /// <summary>Summed across every contract in the plan that still asks for this item.</summary>
    public required int Required { get; init; }

    /// <summary>What the inventory holds right now — one pool, shared by every contract.</summary>
    public required int Have { get; init; }

    /// <summary>What is left to gather. Never negative: a surplus is not a negative shortfall.</summary>
    public int Outstanding => Math.Max(0, Required - Have);

    /// <summary>
    /// How much of the requirement the inventory already covers, in [0, 1] — the progress meter's
    /// value. Clamped at 1 for the same reason <see cref="Outstanding"/> floors at 0: holding double
    /// what the plan asks for is still just "done", not 200% done.
    /// </summary>
    public double CoveredFraction => Required <= 0 ? 0 : Math.Min(1, (double)Have / Required);
}

/// <summary>
/// The combined "what to still gather" list across a set of contracts — the single home for that
/// arithmetic.
/// <para>
/// Two things make this more than a sum. The inventory is <b>one pool</b>: two contracts each asking
/// for 36 SCU of Gold need 72 between them, not 36 twice, which is exactly what a per-contract
/// readiness chip cannot tell you. And the amounts are the same whole units
/// <see cref="InventoryReadiness.RequiredCount"/> deducts on completion, so the list a player mines
/// against is the list completing will actually consume.
/// </para>
/// <para>
/// Deciding <em>which</em> contracts belong in the plan is the caller's job — this sums whatever it
/// is handed. Pure, so the arithmetic is provable without a window or a store.
/// </para>
/// </summary>
public static class GatheringPlan
{
    /// <summary>
    /// Items still missing across <paramref name="contracts"/>, ordered by name.
    /// <para>
    /// Fully covered items are left out: the list answers "what do I still need", and an item that
    /// needs nothing is not an answer to it. Alphabetical rather than by size, so the list does not
    /// reshuffle itself under the player while they are working through it.
    /// </para>
    /// </summary>
    /// <param name="have">How many of an item the player holds, by name.</param>
    public static IReadOnlyList<GatheringItem> Build(
        IEnumerable<WikeloContract> contracts,
        Func<string, int> have) =>
        contracts
            .SelectMany(contract => contract.Requirements)
            .GroupBy(requirement => requirement.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GatheringItem
            {
                Name = group.Key,
                Required = group.Sum(InventoryReadiness.RequiredCount),
                Have = have(group.Key),
            })
            .Where(item => item.Outstanding > 0)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
