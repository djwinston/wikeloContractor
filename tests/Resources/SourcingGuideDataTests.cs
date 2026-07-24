using System.IO;
using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Resources;

/// <summary>
/// Guards the shipped knowledge base in <c>docs/sourcing/</c> as data. The files are hand-authored,
/// so a missing <c>name</c> or a duplicated key would otherwise only surface as "the guide silently
/// stopped showing" at runtime.
/// </summary>
public class SourcingGuideDataTests
{
    private static string SourcingDirectory
    {
        get
        {
            // Walk up from the test bin directory to the repo root (same idiom as the
            // localization parity tests).
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "docs", "sourcing");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate docs/sourcing above the test bin directory.");
        }
    }

    /// <summary>Every guide file paired with its parsed front matter. README.md has no name by design.</summary>
    private static List<(string File, Dictionary<string, string> Front, string Body)> LoadAll() =>
        [.. Directory.EnumerateFiles(SourcingDirectory, "*.md")
            .Select(path =>
            {
                var (front, body) = MarkdownDocument.SplitFrontMatter(File.ReadAllText(path));
                return (File: Path.GetFileName(path), Front: front, Body: body);
            })];

    private static List<(string File, Dictionary<string, string> Front, string Body)> LoadEntries() =>
        [.. LoadAll().Where(e => e.Front.ContainsKey("name"))];

    [Fact]
    public void The_knowledge_base_is_not_empty() =>
        Assert.NotEmpty(LoadEntries());

    [Fact]
    public void Every_file_except_the_readme_declares_a_name()
    {
        var missing = LoadAll()
            .Where(e => !e.Front.ContainsKey("name"))
            .Select(e => e.File)
            .Where(f => !f.Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(missing.Count == 0, $"Files with no `name` front matter: [{string.Join(", ", missing)}]");
    }

    [Fact]
    public void No_name_is_blank_or_untrimmed()
    {
        var bad = LoadEntries()
            .Where(e => string.IsNullOrWhiteSpace(e.Front["name"]) || e.Front["name"] != e.Front["name"].Trim())
            .Select(e => e.File)
            .ToList();

        Assert.True(bad.Count == 0, $"Blank or untrimmed `name` in: [{string.Join(", ", bad)}]");
    }

    [Fact]
    public void Names_are_unique_ignoring_case()
    {
        // The lookup is case-insensitive, so two files sharing a name would make one unreachable.
        var duplicates = LoadEntries()
            .GroupBy(e => e.Front["name"], StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({string.Join(", ", g.Select(e => e.File))})")
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate names: [{string.Join("; ", duplicates)}]");
    }

    [Fact]
    public void Every_body_parses_without_throwing()
    {
        // The parser is total by design; this pins that promise against the real content.
        foreach (var entry in LoadAll())
        {
            var blocks = MarkdownDocument.Parse(MarkdownDocument.StripComments(entry.Body));
            Assert.All(blocks, b => Assert.NotEmpty(b.Inlines));
        }
    }

    [Fact]
    public void No_body_leaks_an_unterminated_comment()
    {
        // StripComments drops everything after an unclosed "<!--", which would silently delete a
        // guide's content — catch the typo here instead.
        var leaking = LoadAll()
            .Where(e => CountOccurrences(e.Body, "<!--") != CountOccurrences(e.Body, "-->"))
            .Select(e => e.File)
            .ToList();

        Assert.True(leaking.Count == 0, $"Unbalanced comment markers in: [{string.Join(", ", leaking)}]");

        static int CountOccurrences(string text, string token)
        {
            var count = 0;
            var index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return count;
        }
    }

    [Fact]
    public void Only_http_links_are_used()
    {
        // MarkdownViewer refuses to launch anything else, so such a link would render as dead text.
        var offenders = new List<string>();

        foreach (var entry in LoadAll())
        {
            var links = MarkdownDocument.Parse(MarkdownDocument.StripComments(entry.Body))
                .SelectMany(b => b.Inlines)
                .Select(i => i.Link)
                .OfType<string>()
                .Where(url => !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                              && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            offenders.AddRange(links.Select(url => $"{entry.File}: {url}"));
        }

        Assert.True(offenders.Count == 0, $"Non-http links: [{string.Join("; ", offenders)}]");
    }
}
