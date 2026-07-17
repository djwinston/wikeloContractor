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

    private void OnRateLimitChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_catalogService.RateLimitedUntil is null)
            {
                return;
            }

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

    /// <summary>Refreshes the countdown text; past zero switches to "resuming" until data arrives.</summary>
    private void Tick()
    {
        var until = _catalogService.RateLimitedUntil;
        var secondsLeft = until is null
            ? 0
            : (int)Math.Ceiling((until.Value - DateTimeOffset.UtcNow).TotalSeconds);

        if (secondsLeft > 0)
        {
            var format = Application.Current.TryFindResource("Catalog_RateLimited_Retry") as string
                ?? "Retrying in {0} s.";
            Message = string.Format(format, secondsLeft);
        }
        else
        {
            _timer.Stop();
            Message = Application.Current.TryFindResource("Catalog_RateLimited_Resuming") as string;
        }
    }
}
