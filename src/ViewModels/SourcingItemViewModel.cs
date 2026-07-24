using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// One row of the sourcing reference: a required item, its category, and its knowledge-base entry.
/// Deliberately count-free — this page is a reference, the quantity the player holds belongs to the
/// inventory.
/// </summary>
/// <remarks>
/// Implements <see cref="IRequirementItem"/> so the shared grid, filter and <c>ItemThumbTemplate</c>
/// treat it exactly like an inventory row. Presence of <see cref="Note"/> / <see cref="Guide"/> is
/// read directly by the pages through <c>PresenceToVisibilityConverter</c> — no derived bool needed.
/// </remarks>
public sealed class SourcingItemViewModel(string name, InventoryCategory category, SourcingGuide? guide) : IRequirementItem
{
    public string Name { get; } = name;

    public InventoryCategory Category { get; } = category;

    /// <summary>Short "where to source it" line; null when no file names this item, or it has no summary.</summary>
    public string? Note { get; } = guide is { HasSummary: true } ? guide.Summary : null;

    /// <summary>The step-by-step body, as Markdown; null when the entry is still a stub.</summary>
    public string? Guide { get; } = guide is { HasBody: true } ? guide.Body : null;

    /// <summary>Localized category name; also the grouping key for the page's section headers.</summary>
    public string CategoryLabel => Localized.String(InventoryCategoryDisplay.LabelKey(Category)) ?? Name;
}
