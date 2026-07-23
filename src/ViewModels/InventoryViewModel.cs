using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The personal inventory: every distinct required item across the catalog, grouped by category, each
/// with a persisted count. The grid, filters and image preview come from
/// <see cref="RequirementListViewModel"/>; this adds only the count store the row VM writes through.
/// </summary>
public sealed class InventoryViewModel(IContractCatalogService catalogService, IInventoryStore store)
    : RequirementListViewModel(catalogService)
{
    protected override IRequirementItem CreateItem(string name, InventoryCategory category) =>
        new InventoryItemViewModel(name, category, store);
}
