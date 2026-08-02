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
    /// In-game overlay preferences. Never null: a settings file written before the overlay existed
    /// has no such member, and the initializer supplies the defaults.
    /// </summary>
    public OverlaySettings Overlay { get; set; } = new();
}
