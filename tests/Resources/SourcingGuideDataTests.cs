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

    /// <summary>The shared fragments guides pull in with <c>{{include: key}}</c>, keyed by name.</summary>
    private static Dictionary<string, string> LoadFragments()
    {
        var directory = Path.Combine(SourcingDirectory, "_shared");
        var fragments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directory))
        {
            return fragments;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.md"))
        {
            var (front, body) = MarkdownDocument.SplitFrontMatter(File.ReadAllText(path));
            if (front.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            {
                fragments[name.Trim()] = body;
            }
        }

        return fragments;
    }

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
        // Scoped to real guides: README.md is the format doc and its examples are not shipped links.
        var offenders = new List<string>();

        foreach (var entry in LoadEntries())
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

    [Fact]
    public void Image_references_are_web_urls_or_bundled_paths()
    {
        // MarkdownViewer drops anything carrying another scheme, so such a picture would never show.
        var offenders = new List<string>();

        foreach (var entry in LoadEntries())
        {
            var images = MarkdownDocument.Parse(MarkdownDocument.StripComments(entry.Body))
                .Where(b => b.Kind == MarkdownBlockKind.Image)
                .Select(b => b.Url)
                .OfType<string>()
                // A relative path is the bundled case and is fine; anything absolute must be web.
                // A rooted local path would only exist on the author's machine.
                .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                              && uri.Scheme is not ("http" or "https"));

            offenders.AddRange(images.Select(url => $"{entry.File}: {url}"));
        }

        Assert.True(offenders.Count == 0, $"Unusable image references: [{string.Join("; ", offenders)}]");
    }

    [Fact]
    public void Only_known_front_matter_keys_are_used()
    {
        // The service ignores anything it does not recognise, so a typo like "factin:" would simply
        // never render — silently, and only on the one page nobody thought to re-check.
        string[] known = ["name", "summary", "contract", "faction"];

        var unknown = LoadEntries()
            .SelectMany(entry => entry.Front.Keys.Select(key => (entry.File, Key: key)))
            .Where(pair => !known.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .Select(pair => $"{pair.File}: {pair.Key}")
            .ToList();

        Assert.True(unknown.Count == 0, $"Unknown front matter keys: [{string.Join("; ", unknown)}]");
    }

    [Fact]
    public void A_faction_or_contract_is_never_blank_when_present()
    {
        // An empty value is worse than an absent key: the row would render with a label and nothing
        // after it. Drop the key instead.
        var blanks = LoadEntries()
            .SelectMany(entry => new[] { "contract", "faction" }
                .Where(key => entry.Front.ContainsKey(key) && string.IsNullOrWhiteSpace(entry.Front[key]))
                .Select(key => $"{entry.File}: {key}"))
            .ToList();

        Assert.True(blanks.Count == 0, $"Blank optional keys: [{string.Join("; ", blanks)}]");
    }

    [Fact]
    public void Every_include_names_a_fragment_that_exists()
    {
        // An unknown key is dropped silently at render time so no markup ever reaches a reader —
        // which is exactly why the typo has to be caught here instead.
        var fragments = LoadFragments();

        // Comments come off first, exactly as the service does it: a fragment documents its own key
        // inside a comment, and a commented-out include is not a real reference.
        var dangling = LoadEntries()
            .SelectMany(entry => MarkdownDocument
                .IncludeKeys(MarkdownDocument.StripComments(entry.Body))
                .Select(key => (entry.File, Key: key)))
            .Where(reference => !fragments.ContainsKey(reference.Key))
            .Select(reference => $"{reference.File}: {reference.Key}")
            .ToList();

        Assert.True(dangling.Count == 0, $"Includes with no fragment: [{string.Join("; ", dangling)}]");
    }

    [Fact]
    public void Every_shared_fragment_is_used_by_a_guide()
    {
        var used = LoadEntries()
            .SelectMany(entry => MarkdownDocument.IncludeKeys(MarkdownDocument.StripComments(entry.Body)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = LoadFragments().Keys.Where(name => !used.Contains(name)).ToList();

        Assert.True(orphans.Count == 0, $"Fragments nothing includes: [{string.Join("; ", orphans)}]");
    }

    [Fact]
    public void A_resolved_guide_carries_the_shared_text()
    {
        var fragments = LoadFragments();
        var entry = LoadEntries().Single(e => e.Front["name"] == "RCMBNT-XTL-3");

        // The service's own order: splice the fragments in, then strip comments — which is why a
        // fragment documenting its key inside a comment never reaches the page.
        var resolved = MarkdownDocument.StripComments(MarkdownDocument.ResolveIncludes(entry.Body, fragments));

        Assert.Contains("Site B", resolved);
        Assert.Contains("gives the `XTL` series", resolved);
        Assert.DoesNotContain("{{include", resolved);
    }
}
