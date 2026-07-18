namespace WikeloContractor.Services;

/// <summary>Shared HTTP identity for every outgoing client (API and image CDNs).</summary>
internal static class AppHttp
{
    /// <summary>Canonical repository URL — single source for the User-Agent and the update feed.</summary>
    public const string RepoUrl = "https://github.com/djwinston/wikeloContractor";

    /// <summary>Per API terms of use: identify public projects politely.</summary>
    public const string UserAgent = $"WikeloContractor/0.1 (+{RepoUrl})";
}
