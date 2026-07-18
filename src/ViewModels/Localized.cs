namespace WikeloContractor.ViewModels;

/// <summary>
/// Code-side lookups of localized resources. XAML binds static text via
/// <c>{DynamicResource}</c>; ViewModels use this for strings composed in code
/// (formatted messages, labels picked by enum value).
/// </summary>
internal static class Localized
{
    public static string? String(string key) => Application.Current.TryFindResource(key) as string;

    /// <summary>Formats a localized format string; falls back to the raw key when missing.</summary>
    public static string Format(string key, params object?[] args) =>
        String(key) is { } format ? string.Format(format, args) : key;
}
