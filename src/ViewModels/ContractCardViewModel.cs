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
    private readonly IFavoritesService _favoritesService;
    private readonly IInventoryStore _inventoryStore;
    private readonly ContractCompletionInteraction _completionInteraction;

    /// <summary>
    /// True while enrichment is running, i.e. this contract's requirement list is still the
    /// summary-based fallback. Completing against it deducts the wrong amounts, so the toggle
    /// is withheld until the real hauling orders have arrived.
    /// </summary>
    private bool _isSyncing;

    public ContractCardViewModel(
        WikeloContract contract,
        ICompletionService completionService,
        IFavoritesService favoritesService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction,
        bool isSyncing = false)
    {
        _completionService = completionService;
        _favoritesService = favoritesService;
        _inventoryStore = inventoryStore;
        _completionInteraction = completionInteraction;
        _contract = contract;
        _isSyncing = isSyncing;
        _readiness = ContractReadiness.From(contract.Requirements, inventoryStore, IsCompleted);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryLabel))]
    private WikeloContract _contract;

    private ContractReadiness _readiness;

    /// <summary>Requirement chips carrying per-item availability vs the inventory (drives chip color).</summary>
    public IReadOnlyList<RequirementChip> RequirementChips => _readiness.Chips;

    public bool IsCompleted => _completionService.IsCompleted(Contract.Uuid);

    /// <summary>Flagged for the Favorites page. Unlike completion this has no gate — a contract can
    /// be starred at any time, including mid-sync and after it is completed.</summary>
    public bool IsFavorite => _favoritesService.IsFavorite(Contract.Uuid);

    /// <summary>
    /// "+250 XP" — what the contract awards, shown on every row regardless of completion.
    /// The UI says XP where the model says reputation; see docs/design-system.md.
    /// </summary>
    public string XpLabel => Localized.Format("Catalog_XpBadge", Contract.ReputationAmount);

    /// <summary>
    /// Localized category name for the row's tag chip; empty before the contract is classified
    /// (still <see cref="ContractCategory.Unknown"/> mid-sync), so the chip collapses to nothing.
    /// The key mapping is the shared <see cref="ContractCategoryDisplay"/> home.
    /// </summary>
    public string CategoryLabel =>
        ContractCategoryDisplay.LabelKey(Contract.Category) is { } key
            ? Localized.String(key) ?? string.Empty
            : string.Empty;

    /// <summary>All requirements are fully covered by the inventory — the contract can be turned in.</summary>
    public bool IsReady => _readiness.IsReady;

    /// <summary>Readiness (progress bar + count) is only meaningful before the contract is completed.</summary>
    public bool ShowReadiness => _readiness.ShowReadiness;

    /// <summary>Show the yellow "READY" badge: everything gathered, not yet turned in.</summary>
    public bool ShowReadyBadge => IsReady && !IsCompleted;

    /// <summary>Show the completion toggle at all — only when it can act (ready to complete, or reopen).</summary>
    public bool ShowCompletionToggle => !_isSyncing && (IsReady || IsCompleted);

    /// <summary>Updates the sync gate after enrichment started or finished.</summary>
    public void SetSyncing(bool value)
    {
        if (_isSyncing == value)
        {
            return;
        }

        _isSyncing = value;
        Recompute();
    }

    /// <summary>"3 / 5" — satisfied requirements out of total.</summary>
    public string ReadinessLabel => _readiness.ReadinessLabel;

    /// <summary>Satisfied share in [0, 1] driving the row's progress bar.</summary>
    public double ReadinessFraction => _readiness.Fraction;

    /// <summary>Re-reads completion after the store changed elsewhere (e.g. the detail page toggle).</summary>
    public void RefreshCompleted() => Recompute(nameof(IsCompleted));

    /// <summary>
    /// Re-reads the favorite flag after the store changed elsewhere (the detail page, or the other
    /// list page showing the same contract). Readiness does not depend on it, so this notifies the
    /// one property rather than going through <see cref="Recompute"/>.
    /// </summary>
    public void RefreshFavorite() => OnPropertyChanged(nameof(IsFavorite));

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
        OnPropertyChanged(nameof(ShowReadyBadge));
        OnPropertyChanged(nameof(ShowCompletionToggle));
        OnPropertyChanged(nameof(ReadinessLabel));
        OnPropertyChanged(nameof(ReadinessFraction));
        ToggleCompletedCommand.NotifyCanExecuteChanged();
    }

    partial void OnContractChanged(WikeloContract value) => Recompute();

    // The command's gate is exactly "is the toggle shown" — completing needs a ready (or already
    // completed) contract, and both are withheld mid-sync. One source so the two cannot drift.
    private bool CanToggleCompleted() => ShowCompletionToggle;

    // Refresh comes from CatalogViewModel (via the services' Changed events), which calls
    // RefreshCompleted / RefreshReadiness on every card — no self-notify needed here.
    [RelayCommand(CanExecute = nameof(CanToggleCompleted))]
    private Task ToggleCompleted() => _completionInteraction.ToggleAsync(Contract);

    // Same story: IFavoritesService.Changed fans back out to RefreshFavorite on every card, so the
    // star updates here and on any other page showing this contract from the one event.
    [RelayCommand]
    private Task ToggleFavorite() => _favoritesService.SetFavoriteAsync(Contract.Uuid, !IsFavorite);
}
