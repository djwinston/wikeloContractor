using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// Shell state. The title and navigation items are defined in MainWindow.xaml via DynamicResource
/// so that language switching works without a restart; what lives here is the one piece of shell
/// behaviour that is not static — the navigation lock held during a catalog sync.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IContractCatalogService _catalogService;

    public MainWindowViewModel(IContractCatalogService catalogService)
    {
        _catalogService = catalogService;

        // App-lifetime singletons on both sides — no unsubscription needed.
        _catalogService.SyncStateChanged += OnSyncStateChanged;
    }

    /// <summary>
    /// Navigation is disabled app-wide while catalog data is being fetched.
    /// <para>
    /// The genuinely unsafe action — completing a contract against a half-loaded requirement list
    /// — is already blocked on the pages themselves. This lock covers the rest: stocking the
    /// inventory against requirements that are about to be replaced, an app update that would
    /// restart the process mid-sync and discard the work, or a language/theme swap during a
    /// multi-minute operation. Until the data lands there is nothing useful to do with it.
    /// </para>
    /// <para>
    /// Bounded by construction: every API call carries a 30 s timeout and any failure aborts
    /// enrichment, which resets the sync state in a <c>finally</c> — so the lock always lifts.
    /// The title bar sits outside the NavigationView, so the window stays closable throughout.
    /// </para>
    /// </summary>
    public bool IsNavigationEnabled => !IsSyncing;

    /// <summary>
    /// Drives the shell-wide sync overlay. It lives here rather than on the Catalog page because
    /// the lock is shell-wide: disabling the NavigationView also disables the page inside it, so
    /// an overlay hosted by a page would render greyed out on top of the very thing it explains.
    /// </summary>
    public bool IsSyncing => _catalogService.SyncState.IsSyncing;

    /// <summary>Progress within the current sync phase, in [0, 1].</summary>
    public double SyncFraction => _catalogService.SyncState.Fraction;

    /// <summary>"Loading contract details — 12 of 67".</summary>
    public string? SyncProgressText => _catalogService.SyncState switch
    {
        { Phase: CatalogSyncPhase.Contracts } s => Localized.Format("Catalog_Syncing_Contracts", s.Completed, s.Total),
        { Phase: CatalogSyncPhase.Rewards } s => Localized.Format("Catalog_Syncing_Rewards", s.Completed, s.Total),
        _ => null,
    };

    private void OnSyncStateChanged(object? sender, EventArgs e) =>
        // Enrichment reports progress from a background thread.
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(IsSyncing));
            OnPropertyChanged(nameof(IsNavigationEnabled));
            OnPropertyChanged(nameof(SyncFraction));
            OnPropertyChanged(nameof(SyncProgressText));
        });
}
