using WikeloContractor.Models;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// State of the sourcing detail page. The list pushes an item via <see cref="Show"/> before
/// navigating here (the page itself is a DI singleton), mirroring
/// <see cref="ContractDetailViewModel"/>.
/// </summary>
public partial class SourcingDetailViewModel(INavigationService navigationService) : ViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(Category))]
    [NotifyPropertyChangedFor(nameof(Note))]
    [NotifyPropertyChangedFor(nameof(Guide))]
    [NotifyPropertyChangedFor(nameof(CategoryLabel))]
    private SourcingItemViewModel? _item;

    public string Name => Item?.Name ?? string.Empty;

    public InventoryCategory Category => Item?.Category ?? InventoryCategory.Other;

    /// <summary>
    /// The same short line the list row shows — repeated here so the page stands alone. Presence
    /// drives the "where to find it" card vs. its placeholder through the visibility converter.
    /// </summary>
    public string? Note => Item?.Note;

    public string CategoryLabel => Item?.CategoryLabel ?? string.Empty;

    /// <summary>
    /// The step-by-step acquisition write-up as Markdown, from <c>docs/sourcing/{item}.md</c>.
    /// Null while an entry is still a stub, which is most of them; presence drives the guide card
    /// vs. its placeholder.
    /// </summary>
    public string? Guide => Item?.Guide;

    /// <summary>Sets the item to display; call right before navigating to the page.</summary>
    public void Show(SourcingItemViewModel item) => Item = item;

    [RelayCommand]
    private void GoBack() => navigationService.GoBack();
}
