using WikeloContractor.Models;
using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// The full contract catalog. Owns loading, freshness/sync reporting, the rate-limit countdown and
/// the reputation banner; the card list and its filters come from <see cref="ContractListViewModel"/>.
/// </summary>
public partial class CatalogViewModel : ContractListViewModel
{
    /// <summary>How many times in a row an elapsed rate-limit window may trigger an automatic refetch.</summary>
    private const int _maxRateLimitRetries = 3;

    /// <summary>Consecutive automatic retries so far; reset by any load that is not rate-limited.</summary>
    private int _rateLimitRetries;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Load failed and there is no cached data to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSynced))]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    private bool _hasLoadError;

    /// <summary>Freshness of the shown data, decided once by the service.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSynced))]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    private CatalogStatus _status;

    /// <summary>Progress of background enrichment; here it only decides whether the data is trustworthy.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSynced))]
    [NotifyPropertyChangedFor(nameof(IsSyncing))]
    private CatalogSyncState _syncState = CatalogSyncState.Idle;

    /// <summary>
    /// Enrichment is running, so rewards, categories and full requirement lists are still missing.
    /// Two things read this: the sync badge must stop claiming "synced", and the cards must
    /// withhold completion — completing now would deduct the wrong amounts from the inventory.
    /// The visible lock and the progress readout belong to the shell
    /// (<see cref="MainWindowViewModel"/>), which blocks the whole app for the duration.
    /// </summary>
    public bool IsSyncing => SyncState.IsSyncing;

    /// <summary>
    /// Data is current for the live game version (drives the green sync badge). Mid-sync this is
    /// false: the contracts on screen are missing their rewards, categories and full requirement
    /// lists, so claiming "synced" would be a lie until enrichment lands.
    /// </summary>
    public bool IsSynced => !HasLoadError && Status == CatalogStatus.Online && !IsSyncing;

    /// <summary>API is unreachable, showing cached data (drives the offline InfoBar).</summary>
    public bool IsOffline => !HasLoadError && Status == CatalogStatus.Offline;

    [ObservableProperty]
    private string? _gameVersion;

    /// <summary>Wikelo standing shown as a progress bar above the contract list.</summary>
    [ObservableProperty]
    private ReputationSummary? _reputation;

    /// <summary>Shared rate-limit countdown, bound by both the Catalog and Settings pages.</summary>
    public RateLimitWatcher RateLimit { get; }

    public CatalogViewModel(
        IContractCatalogService catalogService,
        ICompletionService completionService,
        IFavoritesService favoritesService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction,
        RateLimitWatcher rateLimit,
        INavigationService navigationService,
        ContractDetailViewModel detailViewModel)
        : base(catalogService, completionService, favoritesService, inventoryStore,
               completionInteraction, navigationService, detailViewModel)
    {
        RateLimit = rateLimit;
        RateLimit.WindowElapsed += OnRateLimitWindowElapsed;
        RecomputeReputation();
    }

    public override async Task OnNavigatedToAsync()
    {
        // Cheap after the first call: served from memory unless a version check is due.
        await LoadAsync();
    }

    /// <summary>The catalog shows every contract the service holds.</summary>
    protected override void RebuildFromCatalog()
    {
        if (CatalogService.Current is { } result)
        {
            SetContracts(result.Contracts);
        }
    }

    /// <summary>Completing a contract changes the earned standing shown in the banner.</summary>
    protected override void OnCompletionChangedCore() => RecomputeReputation();

    /// <summary>A fresh card list can carry a different completed set (e.g. after enrichment).</summary>
    protected override void OnContractsSet() => RecomputeReputation();

    /// <summary>Mirror the service's sync state into the observable the page's badge binds to.</summary>
    protected override void OnSyncStateChangedCore() => SyncState = CatalogService.SyncState;

    /// <summary>
    /// The rate-limit wait elapsed, so honour the countdown's promise and refetch.
    /// <para>
    /// Capped at <see cref="_maxRateLimitRetries"/> consecutive attempts. Retrying at the cadence
    /// the server itself dictated via Retry-After is polite; retrying forever while the app sits
    /// open overnight is polling, which the API terms rule out. After the cap the bar tells the
    /// user to refresh manually. The counter resets on any load that is not rate-limited.
    /// </para>
    /// </summary>
    private void OnRateLimitWindowElapsed(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_rateLimitRetries >= _maxRateLimitRetries)
            {
                RateLimit.ReportRetriesExhausted();
                return;
            }

            _rateLimitRetries++;
            _ = LoadAsync();
        });

    private void RecomputeReputation() =>
        Reputation = ReputationSummary.From(ReputationLevels.Compute(CompletionService.TotalReputation));

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasLoadError = false;

        try
        {
            var result = await CatalogService.GetContractsAsync();

            // Read before publishing the cards: a sync started by this very call (or already in
            // flight from Settings' update check) must be reflected the moment they appear.
            SyncState = CatalogService.SyncState;
            SetContracts(result.Contracts);
            // Header shows the version without the API build number — see GameVersionDisplay.
            GameVersion = GameVersionDisplay.WithoutBuild(result.GameVersion);
            Status = result.Status;

            if (result.Status != CatalogStatus.RateLimited)
            {
                // We got through. Close the countdown ourselves: CatalogUpdated only fires at the
                // end of enrichment, so a clean load over an enriched cache would otherwise leave
                // the bar sitting on "Resuming loading..." over perfectly healthy data.
                _rateLimitRetries = 0;
                RateLimit.Dismiss();
            }
        }
        catch (Exception)
        {
            // No network and no cache — nothing to show.
            HasLoadError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
