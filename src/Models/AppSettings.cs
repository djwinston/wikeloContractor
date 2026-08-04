namespace WikeloContractor.Models;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed class AppSettings
{
    /// <summary>UI language code: "en" or "uk".</summary>
    public string Language { get; set; } = "en";

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>
    /// Minimizing puts the window in the notification area instead of the taskbar.
    /// <para>
    /// Off by default: silently changing what the minimize button does is the kind of surprise that
    /// reads as the app having crashed. The tray icon itself is always registered — it is how the
    /// overlay is toggled without alt-tabbing through the shell — so this only decides where the
    /// window goes.
    /// </para>
    /// </summary>
    public bool MinimizeToTray { get; set; }

    /// <summary>
    /// In-game overlay preferences. Never null: a settings file written before the overlay existed
    /// has no such member, and the initializer supplies the defaults.
    /// </summary>
    public OverlaySettings Overlay { get; set; } = new();
}
