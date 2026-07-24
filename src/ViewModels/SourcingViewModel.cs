using WikeloContractor.Models;
using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The sourcing reference ("where to find"): the same required-item grid as
/// <see cref="InventoryViewModel"/>, but each row carries a short authored note and opens
/// <see cref="SourcingDetailViewModel"/> instead of editing a counter. The grid, filters and image
/// preview come from <see cref="RequirementListViewModel"/>.
/// </summary>
public partial class SourcingViewModel : RequirementListViewModel
{
    private readonly ISourcingGuideService _guides;
    private readonly INavigationService _navigationService;
    private readonly SourcingDetailViewModel _detailViewModel;

    public SourcingViewModel(
        IContractCatalogService catalogService,
        ISourcingGuideService guides,
        INavigationService navigationService,
        SourcingDetailViewModel detailViewModel)
        : base(catalogService)
    {
        _guides = guides;
        _navigationService = navigationService;
        _detailViewModel = detailViewModel;
    }

    protected override IRequirementItem CreateItem(string name, InventoryCategory category) =>
        new SourcingItemViewModel(name, category, _guides.GetGuide(name));

    // Search covers the note too: "where do I get anything from Kareah?" is a real question.
    protected override bool MatchesSearch(IRequirementItem item, string search) =>
        base.MatchesSearch(item, search)
        || (item is SourcingItemViewModel sourcing
            && sourcing.Note?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

    [RelayCommand]
    private void OpenDetails(SourcingItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _detailViewModel.Show(item);
        // The detail page is not a nav menu item — navigate with back-stack support.
        _ = _navigationService.NavigateWithHierarchy(typeof(Views.Pages.SourcingDetailPage));
    }
}
