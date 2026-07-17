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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static void ApplyTheme(AppTheme theme)
    {
        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            case AppTheme.System:
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }
}
