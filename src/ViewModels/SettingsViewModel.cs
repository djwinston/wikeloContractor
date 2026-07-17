using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.ViewModels;

public partial class SettingsViewModel : ViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    private bool _isInitialized;

    /// <summary>0 = English, 1 = Ukrainian.</summary>
    [ObservableProperty]
    private int _languageIndex;

    /// <summary>0 = System, 1 = Light, 2 = Dark.</summary>
    [ObservableProperty]
    private int _themeIndex;

    public SettingsViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
    }

    public override void OnNavigatedTo()
    {
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
