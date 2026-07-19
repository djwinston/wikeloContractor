using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// A catalog card wrapping a <see cref="WikeloContract"/> with its observable completion state.
/// The record itself is immutable and replaced wholesale by enrichment, so completion lives here,
/// derived from <see cref="ICompletionService"/> and keyed by UUID.
/// </summary>
public partial class ContractCardViewModel : ObservableObject
{
    private readonly ICompletionService _completionService;

    public ContractCardViewModel(WikeloContract contract, ICompletionService completionService)
    {
        _completionService = completionService;
        _contract = contract;
    }

    [ObservableProperty]
    private WikeloContract _contract;

    public bool IsCompleted => _completionService.IsCompleted(Contract.Uuid);

    /// <summary>Re-reads completion after the store changed elsewhere (e.g. the detail page toggle).</summary>
    public void RefreshCompleted() => OnPropertyChanged(nameof(IsCompleted));

    // Refresh comes from CatalogViewModel.OnCompletionChanged (via the service's Changed event),
    // which calls RefreshCompleted on every card — no self-notify needed here.
    [RelayCommand]
    private Task ToggleCompleted() => _completionService.SetCompletedAsync(Contract, !IsCompleted);
}
