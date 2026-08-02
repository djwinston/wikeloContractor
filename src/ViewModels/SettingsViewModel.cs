using WikeloContractor.Models;
using WikeloContractor.Services;
using WikeloContractor.Services.Api;

namespace WikeloContractor.ViewModels;

public partial class SettingsViewModel : ViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IContractCatalogService _catalogService;
    private readonly IOverlayService _overlayService;
    private readonly IHotkeyService _hotkeyService;

    private bool _isInitialized;

    /// <summary>0 = English, 1 = Ukrainian.</summary>
    [ObservableProperty]
    private int _languageIndex;

    /// <summary>0 = System, 1 = Light, 2 = Dark.</summary>
    [ObservableProperty]
    private int _themeIndex;

    /// <summary>
    /// The API's full version string, build number included (e.g. "4.9.0-LIVE.12232306"). Shown
    /// only here, labelled as the API version: the build tracks API data revisions rather than game
    /// patches, so the catalog and detail headers drop it (see <see cref="GameVersionDisplay"/>).
    /// </summary>
    [ObservableProperty]
    private string? _dataApiVersion;

    [ObservableProperty]
    private string? _dataLastSync;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    /// <summary>Manual data refresh failed (API unreachable).</summary>
    [ObservableProperty]
    private bool _updateCheckFailed;

    /// <summary>Shared rate-limit countdown, bound by both the Catalog and Settings pages.</summary>
    public RateLimitWatcher RateLimit { get; }

    /// <summary>Modifiers that, plus the slot digit, add one to that overlay slot.</summary>
    [ObservableProperty]
    private string _overlayIncrementPattern = string.Empty;

    /// <summary>Modifiers that, plus the slot digit, subtract one.</summary>
    [ObservableProperty]
    private string _overlayDecrementPattern = string.Empty;

    [ObservableProperty]
    private string _overlayToggleKey = string.Empty;

    [ObservableProperty]
    private string _overlayInteractiveKey = string.Empty;

    [ObservableProperty]
    private bool _overlayShowOnStartup;

    /// <summary>Combinations that could not be claimed, or that collide with each other; empty when clean.</summary>
    [ObservableProperty]
    private string _overlayHotkeyIssue = string.Empty;

    [ObservableProperty]
    private bool _hasOverlayHotkeyIssue;

    /// <summary>
    /// True when the app is NOT elevated — i.e. when hotkeys can silently stop working the moment an
    /// elevated Star Citizen takes the foreground. See <see cref="AppElevation"/>.
    /// </summary>
    public bool ShowElevationHint => !AppElevation.IsElevated;

    public bool IsElevated => AppElevation.IsElevated;

    public SettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IContractCatalogService catalogService,
        IOverlayService overlayService,
        IHotkeyService hotkeyService,
        RateLimitWatcher rateLimit)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _catalogService = catalogService;
        _overlayService = overlayService;
        _hotkeyService = hotkeyService;
        RateLimit = rateLimit;

        // Both are app-lifetime singletons, as is this VM — no teardown needed.
        _hotkeyService.ResultChanged += (_, _) => RefreshHotkeyIssue();
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
        DataApiVersion = result?.GameVersion;
        DataLastSync = result?.FetchedAt.ToLocalTime().ToString("g");
    }

    /// <summary>Brings the HUD up from the Settings page, so it can be checked without a hotkey.</summary>
    [RelayCommand]
    private void ShowOverlay() => _overlayService.Show();

    /// <summary>
    /// Forgets the saved geometry. The escape hatch for an overlay stranded off-screen — the window
    /// is borderless, click-through and absent from Alt+Tab, so there is no other way back.
    /// </summary>
    [RelayCommand]
    private void ResetOverlayPosition() => _overlayService.ResetPlacement();

    /// <summary>
    /// Relaunches elevated so global hotkeys survive an elevated game taking the foreground. The UAC
    /// prompt is the user's confirmation; the current instance exits only once the new one is on its
    /// way, so a declined prompt leaves everything as it was.
    /// </summary>
    [RelayCommand]
    private void RestartElevated()
    {
        if (AppElevation.TryRestartElevated())
        {
            Application.Current.Shutdown();
        }
    }

    public override void OnNavigatedTo()
    {
        ApplyCatalogState(_catalogService.Current);
        RefreshHotkeyIssue();

        if (_isInitialized)
        {
            return;
        }

        // OnChanged hooks save nothing while _isInitialized == false
        LanguageIndex = _settingsService.Current.Language == "uk" ? 1 : 0;
        ThemeIndex = (int)_settingsService.Current.Theme;

        var overlay = _settingsService.Current.Overlay;
        OverlayIncrementPattern = overlay.IncrementPattern;
        OverlayDecrementPattern = overlay.DecrementPattern;
        OverlayToggleKey = overlay.ToggleOverlayKey;
        OverlayInteractiveKey = overlay.ToggleInteractiveKey;
        OverlayShowOnStartup = overlay.ShowOnStartup;

        _isInitialized = true;
    }

    partial void OnOverlayIncrementPatternChanged(string value) =>
        ApplyOverlayHotkey(overlay => overlay.IncrementPattern = value);

    partial void OnOverlayDecrementPatternChanged(string value) =>
        ApplyOverlayHotkey(overlay => overlay.DecrementPattern = value);

    partial void OnOverlayToggleKeyChanged(string value) =>
        ApplyOverlayHotkey(overlay => overlay.ToggleOverlayKey = value);

    partial void OnOverlayInteractiveKeyChanged(string value) =>
        ApplyOverlayHotkey(overlay => overlay.ToggleInteractiveKey = value);

    partial void OnOverlayShowOnStartupChanged(bool value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _settingsService.Current.Overlay.ShowOnStartup = value;
        _ = _settingsService.SaveAsync();
    }

    /// <summary>Writes one hotkey setting, persists, and re-registers the whole plan.</summary>
    private void ApplyOverlayHotkey(Action<OverlaySettings> write)
    {
        if (!_isInitialized)
        {
            return;
        }

        write(_settingsService.Current.Overlay);
        _ = _settingsService.SaveAsync();

        // Re-registering is what makes the new combination live; RefreshHotkeyIssue then runs off
        // the service's ResultChanged, so a combination another application owns is reported here.
        _overlayService.ApplyHotkeys();
    }

    private void RefreshHotkeyIssue()
    {
        var result = _hotkeyService.LastResult;
        var problems = new List<string>(2);

        if (result.Conflicts.Count > 0)
        {
            problems.Add(Localized.Format(
                "Settings_Overlay_Conflict",
                string.Join(", ", result.Conflicts.Select(conflict => conflict.Dropped.Binding.Format()).Distinct())));
        }

        if (result.Failed.Count > 0)
        {
            problems.Add(Localized.Format(
                "Settings_Overlay_Failed",
                string.Join(", ", result.Failed.Select(registration => registration.Binding.Format()).Distinct())));
        }

        OverlayHotkeyIssue = string.Join(" ", problems);
        HasOverlayHotkeyIssue = problems.Count > 0;
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
        _settingsService.Current.Theme = theme;

        ApplicationHostService.ApplyTheme(theme);
        _ = _settingsService.SaveAsync();
    }
}
