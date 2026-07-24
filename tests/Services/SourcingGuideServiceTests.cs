using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class SourcingGuideServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _bundled;
    private readonly string _user;

    public SourcingGuideServiceTests()
    {
        _bundled = Path.Combine(_root, "bundled");
        _user = Path.Combine(_root, "user");
        _ = Directory.CreateDirectory(_bundled);
        _ = Directory.CreateDirectory(_user);
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

    private void WriteBundled(string file, string content) =>
        File.WriteAllText(Path.Combine(_bundled, file), content);

    private void WriteUser(string file, string content) =>
        File.WriteAllText(Path.Combine(_user, file), content);

    private SourcingGuideService Service() => new(_user, _bundled, TimeSpan.Zero);

    [Fact]
    public void Missing_directories_yield_no_guides()
    {
        var service = new SourcingGuideService(
            Path.Combine(_root, "nope"), Path.Combine(_root, "also-nope"), TimeSpan.Zero);

        Assert.Null(service.GetGuide("Carinite"));
    }

    [Fact]
    public void A_bundled_file_is_found_by_its_front_matter_name()
    {
        WriteBundled("anything.md", "---\nname: Carinite\nsummary: An ore.\n---\n\n## Steps\n\n1. Mine it.");

        var guide = Service().GetGuide("Carinite");

        Assert.NotNull(guide);
        Assert.Equal("An ore.", guide.Summary);
        Assert.Contains("Mine it.", guide.Body);
    }

    [Fact]
    public void The_file_name_is_irrelevant_to_the_lookup()
    {
        // Renaming a file must never orphan an entry — only `name` is the key.
        WriteBundled("totally-unrelated-filename.md", "---\nname: Carinite\n---\n");

        Assert.NotNull(Service().GetGuide("Carinite"));
    }

    [Fact]
    public void Item_names_are_matched_case_insensitively()
    {
        WriteBundled("c.md", "---\nname: \"Carinite (Pure)\"\n---\n");

        Assert.NotNull(Service().GetGuide("carinite (pure)"));
    }

    [Fact]
    public void A_user_file_wins_over_the_bundled_one()
    {
        WriteBundled("c.md", "---\nname: Carinite\nsummary: Shipped.\n---\n");
        WriteUser("c.md", "---\nname: Carinite\nsummary: Mine.\n---\n");

        Assert.Equal("Mine.", Service().GetGuide("Carinite")!.Summary);
    }

    [Fact]
    public void A_user_file_only_replaces_the_item_it_names()
    {
        WriteBundled("c.md", "---\nname: Carinite\nsummary: Shipped.\n---\n");
        WriteBundled("j.md", "---\nname: Janalite\nsummary: Rare ore.\n---\n");
        WriteUser("c.md", "---\nname: Carinite\nsummary: Mine.\n---\n");

        var service = Service();

        Assert.Equal("Mine.", service.GetGuide("Carinite")!.Summary);
        Assert.Equal("Rare ore.", service.GetGuide("Janalite")!.Summary);
    }

    [Fact]
    public void A_file_without_a_name_is_ignored()
    {
        // README.md ships alongside the guides and must not become an entry.
        WriteBundled("README.md", "# How to author guides\n\nSome prose.");

        Assert.Null(Service().GetGuide("README"));
        Assert.Null(Service().GetGuide("How to author guides"));
    }

    [Fact]
    public void A_stub_reports_no_summary_and_no_body()
    {
        WriteBundled("s.md", "---\nname: Antium Arms\nsummary: ''\n---\n\n<!-- authoring hints only -->\n");

        var guide = Service().GetGuide("Antium Arms");

        Assert.NotNull(guide);
        Assert.False(guide.HasSummary);
        Assert.False(guide.HasBody);
    }

    [Fact]
    public void Comments_never_reach_the_body()
    {
        WriteBundled("c.md", "---\nname: Carinite\n---\n\nReal text.\n\n<!-- hidden note -->\n");

        var guide = Service().GetGuide("Carinite");

        Assert.DoesNotContain("hidden", guide!.Body);
        Assert.Contains("Real text.", guide.Body);
    }

    [Fact]
    public void An_unknown_item_has_no_guide()
    {
        WriteBundled("c.md", "---\nname: Carinite\n---\n");

        Assert.Null(Service().GetGuide("Nonexistent Widget"));
    }

    [Fact]
    public void Non_markdown_files_are_not_read()
    {
        WriteBundled("notes.txt", "---\nname: Carinite\n---\n");

        Assert.Null(Service().GetGuide("Carinite"));
    }
}
