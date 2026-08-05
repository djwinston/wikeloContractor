using System.Windows;
using WikeloContractor.Models.Api;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The notification-area icon, from the menu item to what it does to the shell window and the HUD.
/// <para>
/// The seam is <see cref="WikeloContractor.Services.ITrayHost"/> — a fake window, exactly as
/// <see cref="OverlayScenarios"/> uses a fake overlay window. What is worth proving here is not that
/// Win32 draws an icon, but the two rules a user notices when they are wrong: a minimize that
/// vanishes the app when it was not asked to, and an Exit that skips the shutdown which flushes
/// their counters.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class TrayScenarios(WpfAppFixture app)
{
    /// <summary>One contract is enough: nothing here depends on the catalog's contents.</summary>
    private static ScriptedWikiApi Catalog()
    {
        var api = new ScriptedWikiApi
        {
            Missions = [ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special")],
        };

        api.MissionDetails["m1"] = new MissionDetailDto
        {
            Uuid = "m1",
            HaulingOrders = [new HaulingOrderDto { Name = "Gold", MaxAmount = 2 }],
        };

        return api;
    }

    private Task<CatalogHarness> ReadyAsync() => CatalogHarness.CreateAsync(app, Catalog());

    [Fact]
    public async Task Minimizing_leaves_the_window_in_the_taskbar_by_default()
    {
        // The default has to be the unsurprising one: an app that disappears from the taskbar
        // without being asked to reads as a crash.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            Assert.False(harness.Settings.Current.MinimizeToTray);

            harness.Tray.OnWindowStateChanged(WindowState.Minimized);

            Assert.Equal(0, harness.TrayHost.HideCount);
            Assert.True(harness.TrayHost.IsVisible);
        });
    }

    [Fact]
    public async Task Turning_the_setting_on_makes_the_next_minimize_hide_the_window()
    {
        // Also pins that the flag is read at the moment of the minimize: settings are edited while
        // the app runs and nothing re-creates the tray view model, so caching it at construction
        // would make the switch take a restart.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Tray.OnWindowStateChanged(WindowState.Minimized);
            Assert.Equal(0, harness.TrayHost.HideCount);

            harness.Settings.Current.MinimizeToTray = true;
            harness.Tray.OnWindowStateChanged(WindowState.Minimized);

            Assert.Equal(1, harness.TrayHost.HideCount);
            Assert.False(harness.TrayHost.IsVisible);
        });
    }

    [Fact]
    public async Task A_window_is_never_hidden_when_there_is_no_icon_to_come_back_from()
    {
        // The worst outcome this feature can produce: Hide() takes the window out of the taskbar and
        // out of Alt+Tab, so hiding into a notification area that has no icon of ours leaves Task
        // Manager as the only way back. Minimizing normally is the honest fallback.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Settings.Current.MinimizeToTray = true;
            harness.TrayHost.IsTrayAvailable = false;

            harness.Tray.OnWindowStateChanged(WindowState.Minimized);

            Assert.Equal(0, harness.TrayHost.HideCount);
            Assert.True(harness.TrayHost.IsVisible);
        });
    }

    [Fact]
    public async Task Restoring_or_maximizing_never_hides_anything()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Settings.Current.MinimizeToTray = true;

            harness.Tray.OnWindowStateChanged(WindowState.Normal);
            harness.Tray.OnWindowStateChanged(WindowState.Maximized);

            Assert.Equal(0, harness.TrayHost.HideCount);
        });
    }

    [Fact]
    public async Task The_menu_brings_a_hidden_window_back()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Settings.Current.MinimizeToTray = true;
            harness.Tray.OnWindowStateChanged(WindowState.Minimized);

            harness.Tray.ShowAppCommand.Execute(null);

            Assert.True(harness.TrayHost.IsVisible);
            Assert.Equal(1, harness.TrayHost.RestoreCount);
        });
    }

    [Fact]
    public async Task The_menu_toggles_the_overlay_both_ways()
    {
        // What is the tray's own: the command reaches IOverlayService, and the menu's check mark
        // reads the HUD's state so the two cannot disagree. That the overlay window then shows and
        // hides is OverlayScenarios' claim, not this one's — asserting it here twice would mean two
        // tests to update the day the overlay changes.
        using var harness = await ReadyAsync();
        await app.OnUiAsync(harness.Overlay.Initialize);

        await app.OnUiAsync(() =>
        {
            harness.Tray.ToggleOverlayCommand.Execute(null);

            Assert.True(harness.Overlay.IsShown);
            Assert.True(harness.Tray.Hud.IsShown);

            harness.Tray.ToggleOverlayCommand.Execute(null);

            Assert.False(harness.Overlay.IsShown);
            Assert.False(harness.Tray.Hud.IsShown);
        });
    }

    [Fact]
    public async Task Exit_closes_the_shell_rather_than_quitting_behind_its_back()
    {
        // Application.Shutdown() straight from the menu would skip MainWindow.OnClosed, and with it
        // the teardown that flushes the inventory store — the last counts edited in game.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Tray.ExitCommand.Execute(null);

            Assert.Equal(1, harness.TrayHost.CloseCount);
        });
    }

    [Fact]
    public async Task A_detached_tray_touches_nothing()
    {
        // Detach runs from OnClosed. A closed WPF window is permanently dead, so a menu click that
        // arrives late — the menu is a popup and outlives the click that opened it — must be inert
        // rather than throw from a background dispatcher frame.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Settings.Current.MinimizeToTray = true;
            harness.Tray.Detach();

            harness.Tray.ShowAppCommand.Execute(null);
            harness.Tray.ExitCommand.Execute(null);
            harness.Tray.OnWindowStateChanged(WindowState.Minimized);

            Assert.Equal(0, harness.TrayHost.RestoreCount);
            Assert.Equal(0, harness.TrayHost.CloseCount);
            Assert.Equal(0, harness.TrayHost.HideCount);
        });
    }
}
