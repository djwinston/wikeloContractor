using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using WikeloContractor.Models;
using WikeloContractor.Views;

namespace WikeloContractor.Services;

/// <summary>
/// Manages application startup: loads settings, applies language and theme, shows the main window.
/// </summary>
public sealed class ApplicationHostService(IServiceProvider serviceProvider) : IHostedService
{
    private INavigationWindow? _navigationWindow;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Completed contracts + reputation, so the catalog shows the right standing on first paint.
        await serviceProvider.GetRequiredService<ICompletionService>().LoadAsync();

        // Personal inventory counters, so the inventory page shows the right totals on first open.
        await serviceProvider.GetRequiredService<IInventoryStore>().LoadAsync();

        // Favorited contracts, so the star state is right on the first paint of the catalog.
        await serviceProvider.GetRequiredService<IFavoritesService>().LoadAsync();

        serviceProvider
            .GetRequiredService<ILocalizationService>()
            .ApplyLanguage(settingsService.Current.Language);

        ApplyTheme(settingsService.Current.Theme);

        if (!Application.Current.Windows.OfType<MainWindow>().Any())
        {
            _navigationWindow = serviceProvider.GetRequiredService<INavigationWindow>();
            _navigationWindow.ShowWindow();

            _ = _navigationWindow.Navigate(typeof(Views.Pages.CatalogPage));
        }

        // Learn about a pending app update in the background; never block startup on it. Result
        // surfaces through IAppUpdateService.StatusChanged (the Settings page reflects it). No-op
        // in a dev run.
        _ = serviceProvider.GetRequiredService<IAppUpdateService>().CheckAndDownloadAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private const string BrandDictionaryPathFragment = "/Resources/Theme/Brand.";

    /// <summary>
    /// Applies the WPF-UI theme, swaps the matching brand palette, and sets the accent colour.
    /// The single home for theme application — both startup and the Settings page call it.
    /// The app always uses the Wikelo brand accent rather than the Windows system accent.
    /// </summary>
    /// <param name="theme">The user's choice; <see cref="AppTheme.System"/> resolves to whatever
    /// Windows is currently using.</param>
    internal static void ApplyTheme(AppTheme theme)
    {
        // updateAccent: false so Apply does not overwrite the accent with the system one — we set
        // the brand accent ourselves below, and re-deriving it here would fight that.
        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, updateAccent: false);
                break;
            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, updateAccent: false);
                break;
            case AppTheme.System:
            default:
                ApplicationThemeManager.ApplySystemTheme(updateAccent: false);
                break;
        }

        // System resolves to a concrete theme only after Apply, so read it back rather than
        // switching on the user's choice again.
        ApplyBrandLayer(ApplicationThemeManager.GetAppTheme());
    }

    /// <summary>
    /// Re-applies everything of ours that is theme-dependent: the brand palette dictionary, the
    /// themed app mark, and the brand accent.
    /// <para>
    /// Separate from <see cref="ApplyTheme"/> because the WPF-UI theme can change without us
    /// asking: on <see cref="AppTheme.System"/> the <c>SystemThemeWatcher</c> swaps it when Windows
    /// does, and it knows nothing about our layer. <c>MainWindow.OnThemeChanged</c> calls this so a
    /// Windows light/dark flip does not leave Brand.Dark merged over a light surface.
    /// </para>
    /// <para>
    /// Safe to call from the <see cref="ApplicationThemeManager.Changed"/> handler: neither the
    /// dictionary swap nor <see cref="ApplicationAccentColorManager"/> raises that event, so there
    /// is no recursion.
    /// </para>
    /// </summary>
    internal static void ApplyBrandLayer(ApplicationTheme applied)
    {
        ApplyBrandPalette(applied);
        ApplyThemedMark(applied);

        if (Application.Current.TryFindResource("BrandAccentColor") is System.Windows.Media.Color accent)
        {
            ApplicationAccentColorManager.Apply(accent, applied);
        }
    }

    /// <summary>
    /// Points <c>AppMarkThemed</c> at whichever app-mark artwork suits the current surface, so a
    /// page can just bind <c>{DynamicResource AppMarkThemed}</c>.
    /// <para>
    /// Done here in code rather than as an alias inside the brand dictionaries: a
    /// <c>&lt;StaticResource&gt;</c> entry aliasing another dictionary's key does not survive XAML
    /// compilation (MC1000), and duplicating the geometry per theme would mean new artwork has to
    /// land in three places instead of one.
    /// </para>
    /// <para>
    /// The variant names describe the ARTWORK, not the target theme — the "light" (cyan) mark is
    /// the one drawn for dark surfaces. See <c>docs/brand/icon-spec.md</c>.
    /// </para>
    /// </summary>
    private static void ApplyThemedMark(ApplicationTheme applied)
    {
        var sourceKey = applied == ApplicationTheme.Light ? "AppMarkDark" : "AppMarkLight";

        if (Application.Current.TryFindResource(sourceKey) is { } mark)
        {
            Application.Current.Resources["AppMarkThemed"] = mark;
        }
    }

    /// <summary>
    /// Swaps Brand.Light/Brand.Dark in the merged dictionaries, mirroring how
    /// <see cref="LocalizationService"/> swaps the string dictionary.
    /// </summary>
    private static void ApplyBrandPalette(ApplicationTheme applied)
    {
        var variant = applied == ApplicationTheme.Light ? "Light" : "Dark";
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var current = dictionaries.FirstOrDefault(d =>
            d.Source is not null
            && d.Source.OriginalString.Contains(BrandDictionaryPathFragment, StringComparison.OrdinalIgnoreCase));

        var replacement = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,{BrandDictionaryPathFragment}{variant}.xaml"),
        };

        if (current is null)
        {
            dictionaries.Add(replacement);
            return;
        }

        if (current.Source == replacement.Source)
        {
            return;
        }

        dictionaries[dictionaries.IndexOf(current)] = replacement;
    }
}
