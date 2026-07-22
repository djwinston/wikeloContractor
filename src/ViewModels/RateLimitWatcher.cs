using System.Windows.Threading;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

/// <summary>
/// Shared live countdown for the API rate-limit window. It subscribes to the catalog
/// service's authoritative <see cref="IContractCatalogService.RateLimitedUntil"/> deadline
/// and exposes a ticking, localized message that every page binds to — so the Catalog and
/// Settings pages present exactly the same rate-limit state instead of each reconstructing it.
/// Registered as a singleton and injected into the pages' ViewModels.
/// </summary>
public partial class RateLimitWatcher : ObservableObject
{
    private readonly IContractCatalogService _catalogService;
    private readonly DispatcherTimer _timer;

    /// <summary>True while the countdown is shown (until fresh data resumes loading).</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Localized text with a live countdown, or the "resuming" message past zero.</summary>
    [ObservableProperty]
    private string? _message;

    public RateLimitWatcher(IContractCatalogService catalogService)
    {
        _catalogService = catalogService;
        _catalogService.RateLimitChanged += OnRateLimitChanged;
        _catalogService.CatalogUpdated += OnCatalogUpdated;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Guards against re-announcing the same elapsed window on a later tick.</summary>
    private bool _elapsedAnnounced;

    /// <summary>
    /// The wait is over and the gate is open again. Whoever owns loading (the catalog) subscribes
    /// and actually refetches — the countdown promises "loading resumes", so something has to
    /// resume it. Without this the message was a promise the app never kept: nothing retried, and
    /// the bar sat on "Resuming loading..." until the user navigated away and back.
    /// </summary>
    public event EventHandler? WindowElapsed;

    /// <summary>
    /// Closes the countdown after a load that came back clean.
    /// <para>
    /// Needed because <see cref="IContractCatalogService.CatalogUpdated"/> — the only other thing
    /// that closes this bar — fires at the *end of enrichment*. A successful retry over an
    /// already-enriched cache never raises it, so the bar would stay open over healthy data.
    /// </para>
    /// </summary>
    public void Dismiss()
    {
        _timer.Stop();
        IsActive = false;
    }

    /// <summary>Auto-retrying gave up; the bar stays, pointing the user at the manual refresh.</summary>
    public void ReportRetriesExhausted()
    {
        _timer.Stop();
        IsActive = true;
        Message = Localized.String("Catalog_RateLimited_Exhausted");
    }

    private void OnRateLimitChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_catalogService.RateLimitedUntil is null)
            {
                return;
            }

            _elapsedAnnounced = false;
            IsActive = true;
            Tick();
            _timer.Start();
        });

    private void OnCatalogUpdated(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Fresh data arrived — the pause (if any) is over.
            _timer.Stop();
            IsActive = false;
        });

    /// <summary>Refreshes the countdown text; past zero switches to "resuming" and asks for a retry.</summary>
    private void Tick()
    {
        var until = _catalogService.RateLimitedUntil;
        var secondsLeft = until is null
            ? 0
            : (int)Math.Ceiling((until.Value - DateTimeOffset.UtcNow).TotalSeconds);

        if (secondsLeft > 0)
        {
            Message = Localized.Format("Catalog_RateLimited_Retry", secondsLeft);
            return;
        }

        _timer.Stop();
        Message = Localized.String("Catalog_RateLimited_Resuming");

        if (!_elapsedAnnounced)
        {
            _elapsedAnnounced = true;
            WindowElapsed?.Invoke(this, EventArgs.Empty);
        }
    }
}
