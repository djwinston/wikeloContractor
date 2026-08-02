using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

/// <summary>
/// Where the log file lands. The rule matters more than it looks: the executable lives in
/// <c>current\</c>, which Velopack replaces wholesale on every update, so a log written beside the
/// exe is deleted at exactly the moment someone needs to read it.
/// </summary>
public class AppLogTests
{
    private static string Resolve(string baseDirectory, params string[] existingFiles) =>
        AppLog.ResolveDirectory(
            baseDirectory,
            path => existingFiles.Contains(path, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void A_velopack_layout_logs_into_the_install_root_beside_Update_exe()
    {
        var root = Path.Combine("C:", "Apps", "WikeloContractor");
        var current = Path.Combine(root, "current");

        Assert.Equal(root, Resolve(current, Path.Combine(root, "Update.exe")));
    }

    [Fact]
    public void A_trailing_separator_does_not_change_the_answer()
    {
        var root = Path.Combine("C:", "Apps", "WikeloContractor");
        var current = Path.Combine(root, "current") + Path.DirectorySeparatorChar;

        // AppContext.BaseDirectory always ends with a separator, so this is the real-world input.
        Assert.Equal(root, Resolve(current, Path.Combine(root, "Update.exe")));
    }

    [Fact]
    public void A_dev_run_logs_beside_the_binaries()
    {
        // bin\Debug\net10.0-windows — no "current" folder, nothing above worth writing to.
        var bin = Path.Combine("D:", "repo", "src", "bin", "Debug", "net10.0-windows");

        Assert.Equal(bin, Resolve(bin));
    }

    [Fact]
    public void A_current_folder_without_Update_exe_is_not_treated_as_an_install()
    {
        // Someone's own folder that happens to be called "current" must not redirect the log
        // outside it — the Update.exe probe is what makes the layout a Velopack one.
        var current = Path.Combine("D:", "stuff", "current");

        Assert.Equal(current, Resolve(current));
    }

    [Fact]
    public void A_malformed_path_falls_back_instead_of_throwing()
    {
        const string broken = "\0not-a-path";

        Assert.Equal(broken, Resolve(broken));
    }
}
