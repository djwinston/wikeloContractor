using System.IO;
using WikeloContractor.Models;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

/// <summary>
/// Settings persistence, focused on the case that breaks in the field: a settings.json written by an
/// older build, or edited by hand, must still load. Every user upgrading into the overlay release has
/// a file with no <c>Overlay</c> member.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _file;

    public SettingsServiceTests()
    {
        _ = Directory.CreateDirectory(_root);
        _file = Path.Combine(_root, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    private SettingsService Service() => new(_file);

    private void Write(string json) => File.WriteAllText(_file, json);

    [Fact]
    public async Task A_missing_file_yields_defaults()
    {
        var service = Service();

        await service.LoadAsync();

        Assert.Equal("en", service.Current.Language);
        Assert.NotNull(service.Current.Overlay);
    }

    [Fact]
    public async Task A_settings_file_from_before_the_overlay_still_loads()
    {
        Write("""{ "Language": "uk", "Theme": "Dark" }""");
        var service = Service();

        await service.LoadAsync();

        Assert.Equal("uk", service.Current.Language);
        Assert.Equal(AppTheme.Dark, service.Current.Theme);
        Assert.NotNull(service.Current.Overlay);
        Assert.Equal("Ctrl+Alt", service.Current.Overlay.IncrementPattern);
    }

    [Fact]
    public async Task An_explicitly_null_overlay_does_not_survive_the_load()
    {
        // A property initializer only covers a missing member, not an explicit null.
        Write("""{ "Language": "en", "Overlay": null }""");
        var service = Service();

        await service.LoadAsync();

        Assert.NotNull(service.Current.Overlay);
    }

    [Fact]
    public async Task Overlay_settings_round_trip()
    {
        var service = Service();
        await service.LoadAsync();
        service.Current.Overlay.IncrementPattern = "Alt+Shift";
        service.Current.Overlay.Left = 120;
        await service.SaveAsync();

        var reloaded = Service();
        await reloaded.LoadAsync();

        Assert.Equal("Alt+Shift", reloaded.Current.Overlay.IncrementPattern);
        Assert.Equal(120, reloaded.Current.Overlay.Left);
    }

    [Fact]
    public async Task A_corrupted_file_falls_back_to_defaults()
    {
        Write("{ not json");
        var service = Service();

        await service.LoadAsync();

        Assert.Equal("en", service.Current.Language);
        Assert.NotNull(service.Current.Overlay);
    }
}
