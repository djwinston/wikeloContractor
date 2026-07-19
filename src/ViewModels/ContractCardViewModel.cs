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
    private readonly IInventoryStore _inventoryStore;
    private readonly ContractCompletionInteraction _completionInteraction;

    public ContractCardViewModel(
        WikeloContract contract,
        ICompletionService completionService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction)
    {
        _completionService = completionService;
        _inventoryStore = inventoryStore;
        _completionInteraction = completionInteraction;
        _contract = contract;
        _readiness = ContractReadiness.From(contract.Requirements, inventoryStore, IsCompleted);
    }

    [ObservableProperty]
    private WikeloContract _contract;

    private ContractReadiness _readiness;

    /// <summary>Requirement chips carrying per-item availability vs the inventory (drives chip color).</summary>
    public IReadOnlyList<RequirementChip> RequirementChips => _readiness.Chips;

    public bool IsCompleted => _completionService.IsCompleted(Contract.Uuid);

    /// <summary>All requirements are fully covered by the inventory — the contract can be turned in.</summary>
    public bool IsReady => _readiness.IsReady;

    /// <summary>Readiness (badge + count) is only meaningful before the contract is completed.</summary>
    public bool ShowReadiness => _readiness.ShowReadiness;

    /// <summary>"3 / 5" — satisfied requirements out of total.</summary>
    public string ReadinessLabel => _readiness.ReadinessLabel;

    /// <summary>Re-reads completion after the store changed elsewhere (e.g. the detail page toggle).</summary>
    public void RefreshCompleted() => Recompute(nameof(IsCompleted));

    /// <summary>Recomputes requirement availability after the inventory changed.</summary>
    public void RefreshReadiness() => Recompute();

    private void Recompute(string? alsoNotify = null)
    {
        if (alsoNotify is not null)
        {
            OnPropertyChanged(alsoNotify);
        }

        _readiness = ContractReadiness.From(Contract.Requirements, _inventoryStore, IsCompleted);
        OnPropertyChanged(nameof(RequirementChips));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(ShowReadiness));
        OnPropertyChanged(nameof(ReadinessLabel));
        ToggleCompletedCommand.NotifyCanExecuteChanged();
    }

    partial void OnContractChanged(WikeloContract value) => Recompute();

    /// <summary>Completing is allowed only once the inventory covers the requirements; un-completing always.</summary>
    private bool CanToggleCompleted() => IsCompleted || IsReady;

    // Refresh comes from CatalogViewModel (via the services' Changed events), which calls
    // RefreshCompleted / RefreshReadiness on every card — no self-notify needed here.
    [RelayCommand(CanExecute = nameof(CanToggleCompleted))]
    private Task ToggleCompleted() => _completionInteraction.ToggleAsync(Contract);
}
