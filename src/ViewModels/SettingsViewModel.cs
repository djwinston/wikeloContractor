using System.Reflection;
using WikeloContractor.Models;
using WikeloContractor.Services;
using WikeloContractor.Services.Api;

namespace WikeloContractor.ViewModels;

public partial class SettingsViewModel : ViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IContractCatalogService _catalogService;

    private bool _isInitialized;

    /// <summary>0 = English, 1 = Ukrainian.</summary>
    [ObservableProperty]
    private int _languageIndex;

    /// <summary>0 = System, 1 = Light, 2 = Dark.</summary>
    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private string? _dataGameVersion;

    [ObservableProperty]
    private string? _dataLastSync;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    /// <summary>Manual data refresh failed (API unreachable).</summary>
    [ObservableProperty]
    private bool _updateCheckFailed;

    /// <summary>Shared rate-limit countdown, bound by both the Catalog and Settings pages.</summary>
    public RateLimitWatcher RateLimit { get; }

    /// <summary>App version from the assembly (single source: &lt;Version&gt; in the csproj).</summary>
    public string AppVersion { get; } =
        typeof(SettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public SettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IContractCatalogService catalogService,
        RateLimitWatcher rateLimit)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _catalogService = catalogService;
        RateLimit = rateLimit;
    }

    /// <summary>
    /// Fallback for the version-based cache: force a version check and refetch right now,
    /// e.g. when the player just installed a patch and the 12h timer has not fired yet.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdates)
        {
            return;
        }

        IsCheckingUpdates = true;
        UpdateCheckFailed = false;

        try
        {
            var result = await _catalogService.GetContractsAsync(forceRefresh: true);

            // A 429 surfaces through the shared rate-limit countdown, not this banner.
            UpdateCheckFailed = result.Status == CatalogStatus.Offline;
            ApplyCatalogState(result);
        }
        catch (ApiRateLimitedException)
        {
            // Rate limited and no cache yet — the shared countdown already tells the user to wait.
        }
        catch (Exception)
        {
            // No network and no cache yet.
            UpdateCheckFailed = true;
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private void ApplyCatalogState(CatalogLoadResult? result)
    {
        DataGameVersion = result?.GameVersion;
        DataLastSync = result?.FetchedAt.ToLocalTime().ToString("g");
    }

    public override void OnNavigatedTo()
    {
        ApplyCatalogState(_catalogService.Current);

        if (_isInitialized)
        {
            return;
        }

        // OnChanged hooks save nothing while _isInitialized == false
        LanguageIndex = _settingsService.Current.Language == "uk" ? 1 : 0;
        ThemeIndex = (int)_settingsService.Current.Theme;

        _isInitialized = true;
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (!_isInitialized)
        {
            return;
        }

        var language = value == 1 ? "uk" : "en";
        _localizationService.ApplyLanguage(language);

        _settingsService.Current.Language = language;
        _ = _settingsService.SaveAsync();
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (!_isInitialized)
        {
            return;
        }

        var theme = (AppTheme)value;
        ApplicationHostService.ApplyTheme(theme);

        _settingsService.Current.Theme = theme;
        _ = _settingsService.SaveAsync();
    }
}
