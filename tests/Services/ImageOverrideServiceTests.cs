using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class ImageOverrideServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    public ImageOverrideServiceTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "img-catalog-overrides.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    [Fact]
    public void Missing_file_yields_no_override_and_creates_an_editable_template()
    {
        var service = new ImageOverrideService(_filePath);

        Assert.Null(service.GetOverride("uuid-1", "Testudo Helmet"));
        Assert.True(File.Exists(_filePath));
        Assert.Contains("\"overrides\"", File.ReadAllText(_filePath));
    }

    [Fact]
    public void Pre_rename_user_file_is_adopted_instead_of_being_overwritten_by_the_template()
    {
        var legacyPath = Path.Combine(_directory, "image-overrides.json");
        File.WriteAllText(legacyPath, """{ "overrides": { "uuid-1": "https://example.test/personal.png" } }""");

        var service = new ImageOverrideService(_filePath);

        Assert.Equal("https://example.test/personal.png", service.GetOverride("uuid-1", "Item"));
        Assert.True(File.Exists(_filePath));
        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void Overrides_match_by_uuid_first_then_by_name_case_insensitively()
    {
        File.WriteAllText(_filePath, """
            {
              "overrides": {
                "uuid-1": "https://example.test/by-uuid.png",
                "testudo helmet": "https://example.test/by-name.png"
              }
            }
            """);
        var service = new ImageOverrideService(_filePath);

        Assert.Equal("https://example.test/by-uuid.png", service.GetOverride("uuid-1", "Testudo Helmet"));
        Assert.Equal("https://example.test/by-name.png", service.GetOverride("uuid-other", "Testudo Helmet"));
        Assert.Equal("https://example.test/by-name.png", service.GetOverride(null, "TESTUDO HELMET"));
        Assert.Null(service.GetOverride(null, "Unknown Item"));
    }

    [Fact]
    public void Edited_file_is_reloaded_on_the_next_lookup()
    {
        File.WriteAllText(_filePath, """{ "overrides": { "uuid-1": "https://example.test/old.png" } }""");
        var service = new ImageOverrideService(_filePath, statInterval: TimeSpan.Zero);
        Assert.Equal("https://example.test/old.png", service.GetOverride("uuid-1", "Item"));

        File.WriteAllText(_filePath, """{ "overrides": { "uuid-1": "https://example.test/new.png" } }""");
        // Force a distinct timestamp: file systems may round write times coarsely.
        File.SetLastWriteTimeUtc(_filePath, DateTime.UtcNow.AddSeconds(2));

        Assert.Equal("https://example.test/new.png", service.GetOverride("uuid-1", "Item"));
    }

    [Fact]
    public void Bundled_defaults_apply_and_user_entries_win_per_key()
    {
        var bundledPath = Path.Combine(_directory, "bundled-overrides.json");
        File.WriteAllText(bundledPath, """
            {
              "overrides": {
                "uuid-1": "https://example.test/bundled-1.png",
                "uuid-2": "https://example.test/bundled-2.png"
              }
            }
            """);
        File.WriteAllText(_filePath, """{ "overrides": { "uuid-1": "https://example.test/user-1.png" } }""");
        var service = new ImageOverrideService(_filePath, bundledPath);

        // The user's file wins for uuid-1; the bundled default still serves uuid-2.
        Assert.Equal("https://example.test/user-1.png", service.GetOverride("uuid-1", "Item"));
        Assert.Equal("https://example.test/bundled-2.png", service.GetOverride("uuid-2", "Item"));
        Assert.Null(service.GetOverride("uuid-3", "Item"));
    }

    [Fact]
    public void Missing_bundled_file_is_not_created_and_yields_no_defaults()
    {
        var bundledPath = Path.Combine(_directory, "bundled-overrides.json");
        var service = new ImageOverrideService(_filePath, bundledPath);

        Assert.Null(service.GetOverride("uuid-1", "Item"));
        Assert.False(File.Exists(bundledPath));
    }

    [Fact]
    public void Malformed_file_keeps_previously_loaded_overrides()
    {
        File.WriteAllText(_filePath, """{ "overrides": { "uuid-1": "https://example.test/ok.png" } }""");
        var service = new ImageOverrideService(_filePath, statInterval: TimeSpan.Zero);
        Assert.Equal("https://example.test/ok.png", service.GetOverride("uuid-1", "Item"));

        File.WriteAllText(_filePath, "{ not json");
        File.SetLastWriteTimeUtc(_filePath, DateTime.UtcNow.AddSeconds(2));

        Assert.Equal("https://example.test/ok.png", service.GetOverride("uuid-1", "Item"));
    }
}
