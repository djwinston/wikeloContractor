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
}
