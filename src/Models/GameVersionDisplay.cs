namespace WikeloContractor.Models;

/// <summary>
/// Display forms of the API's version string (e.g. <c>"4.9.0-LIVE.12232306"</c>) — the single home
/// for that formatting, so the catalog and contract-detail headers cannot drift apart.
/// </summary>
public static class GameVersionDisplay
{
    /// <summary>
    /// Drops the trailing build number: <c>"4.9.0-LIVE.12232306"</c> → <c>"4.9.0-LIVE"</c>.
    /// <para>
    /// That build counts API data revisions rather than game patches, so showing it next to a game
    /// version reads as a patch number it is not. The Settings page keeps the full string, labelled
    /// as the API version.
    /// </para>
    /// </summary>
    public static string? WithoutBuild(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        // Only cut after the channel separator: the dots inside the semver part ("4.9.0") have to
        // survive, and the build is the first dot-segment following "-LIVE".
        var channel = version.IndexOf('-');
        if (channel < 0)
        {
            return version;
        }

        var build = version.IndexOf('.', channel);
        return build < 0 ? version : version[..build];
    }
}
