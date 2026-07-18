namespace WikeloContractor.Services;

/// <summary>Shared HTTP identity for every outgoing client (API and image CDNs).</summary>
internal static class AppHttp
{
    /// <summary>Per API terms of use: identify public projects politely.</summary>
    public const string UserAgent = "WikeloContractor/0.1 (+https://github.com/djwinston/wikeloContractor)";
}
