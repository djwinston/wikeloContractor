using System.Reflection;

namespace WikeloContractor.Services;

/// <summary>
/// The running app's version, read from the assembly — whose single source is <c>&lt;Version&gt;</c>
/// in the csproj, injected by the release workflow from the git tag.
/// <para>
/// Lives here rather than on the About page because <see cref="AppLog"/> stamps it on every session
/// and runs before the host exists, so the two would otherwise resolve it separately.
/// </para>
/// </summary>
internal static class AppVersion
{
    /// <summary>Version without the <c>+commit</c> suffix, e.g. <c>0.9.4</c>.</summary>
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
