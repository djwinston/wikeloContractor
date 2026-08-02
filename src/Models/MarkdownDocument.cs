using System.Text;

namespace WikeloContractor.Models;

/// <summary>What a parsed Markdown block is, which decides how the viewer styles it.</summary>
public enum MarkdownBlockKind
{
    Paragraph,

    /// <summary><c>##</c> — the section headings a sourcing guide is built from.</summary>
    Heading,

    /// <summary><c>###</c> — one level down; rendered smaller than <see cref="Heading"/>.</summary>
    SubHeading,

    /// <summary><c>-</c> / <c>*</c> item.</summary>
    Bullet,

    /// <summary><c>1.</c> item — the step-by-step case, which is what a guide mostly is.</summary>
    OrderedItem,

    /// <summary>
    /// <c>![alt](url)</c> alone on a line — a map slide or screenshot. The target lives in
    /// <see cref="MarkdownBlock.Url"/>; the alt text stays in the inlines as the fallback caption.
    /// </summary>
    Image,
}

/// <summary>A run of text inside a block, carrying the inline formatting that applies to it.</summary>
/// <param name="Text">The visible text, with the markers already stripped.</param>
/// <param name="Link">Target of a <c>[text](url)</c> run; null for ordinary text.</param>
public sealed record MarkdownInline(string Text, bool Bold = false, bool Italic = false, bool Code = false, string? Link = null);

/// <summary>One block of a parsed document: its kind, its inline runs, and its list position.</summary>
/// <param name="Number">1-based position within the current ordered list; 0 for every other kind.</param>
/// <param name="Url">Image target of an <see cref="MarkdownBlockKind.Image"/> block; null otherwise.</param>
public sealed record MarkdownBlock(MarkdownBlockKind Kind, IReadOnlyList<MarkdownInline> Inlines, int Number = 0, string? Url = null)
{
    /// <summary>The block's text with all formatting dropped — handy for tests and tooltips.</summary>
    public string PlainText => string.Concat(Inlines.Select(i => i.Text));
}

/// <summary>
/// A deliberately small Markdown subset, enough for the sourcing guides in <c>docs/sourcing/*.md</c>:
/// <c>##</c>/<c>###</c> headings, paragraphs, <c>-</c>/<c>*</c> bullets, <c>1.</c> ordered steps,
/// <c>![alt](url)</c> images, and inline <c>**bold**</c>, <c>*italic*</c>, <c>`code`</c> and
/// <c>[text](url)</c>.
/// <para>
/// Not a CommonMark implementation and not meant to become one — anything unrecognised falls through
/// as plain text rather than throwing, so a guide can never break the page. Pure by design (no WPF),
/// so it is unit-testable on its own; <c>Views/Controls/MarkdownViewer</c> renders the result.
/// </para>
/// </summary>
public static class MarkdownDocument
{
    /// <summary>Splits front matter from the body. Returns the raw body when there is no front matter.</summary>
    /// <remarks>
    /// Front matter is the leading <c>---</c>-delimited block. Only simple <c>key: value</c> lines are
    /// read; anything richer (nested YAML, lists) is out of scope and ignored.
    /// </remarks>
    public static (Dictionary<string, string> FrontMatter, string Body) SplitFrontMatter(string text)
    {
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return (empty, string.Empty);
        }

        var lines = SplitLines(text);
        if (lines.Count == 0 || lines[0].Trim() != "---")
        {
            return (empty, text);
        }

        var closing = -1;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].Trim() == "---")
            {
                closing = i;
                break;
            }
        }

        if (closing < 0)
        {
            // Unterminated front matter — treat the whole file as body rather than losing it.
            return (empty, text);
        }

        for (var i = 1; i < closing; i++)
        {
            var separator = lines[i].IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = lines[i][..separator].Trim();
            var value = lines[i][(separator + 1)..].Trim();

            // Quoted values are common in YAML; the quotes are not part of the value.
            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
            {
                value = value[1..^1];
            }

            if (key.Length > 0)
            {
                empty[key] = value;
            }
        }

        return (empty, string.Join("\n", lines.Skip(closing + 1)));
    }

    /// <summary>
    /// Removes <c>&lt;!-- … --&gt;</c> comments. Skeleton guide files carry their authoring hints this
    /// way, and those must never reach the screen — the viewer would otherwise render them as prose.
    /// Applied at load time so a comments-only body correctly counts as empty.
    /// </summary>
    public static string StripComments(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf("<!--", index, StringComparison.Ordinal);
            if (open < 0)
            {
                _ = result.Append(text, index, text.Length - index);
                break;
            }

            _ = result.Append(text, index, open - index);

            var close = text.IndexOf("-->", open + 4, StringComparison.Ordinal);
            if (close < 0)
            {
                // Unterminated comment — drop the remainder rather than leaking markup.
                break;
            }

            index = close + 3;
        }

        return result.ToString();
    }

    /// <summary>
    /// Splices shared fragments into a body, replacing every <c>{{include: key}}</c> line with the
    /// fragment of that name. Guides for one location repeat the same mechanic verbatim — nine RCMBNT
    /// samples share one Site B description — and this keeps that text in a single file.
    /// <para>
    /// Expansion is **one level deep on purpose**: a marker inside a fragment is dropped rather than
    /// expanded, so two fragments referencing each other cannot loop. An unknown key also leaves
    /// nothing behind, because a reader must never be shown raw markup; the data tests fail on the
    /// dangling reference instead, which surfaces the typo at review time.
    /// </para>
    /// </summary>
    public static string ResolveIncludes(string? body, IReadOnlyDictionary<string, string> fragments)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var result = new StringBuilder(body.Length);

        foreach (var line in SplitLines(body))
        {
            if (!TryIncludeKey(line, out var key))
            {
                _ = result.AppendLine(line);
                continue;
            }

            if (!fragments.TryGetValue(key, out var fragment))
            {
                continue;
            }

            foreach (var fragmentLine in SplitLines(fragment))
            {
                if (!TryIncludeKey(fragmentLine, out _))
                {
                    _ = result.AppendLine(fragmentLine);
                }
            }
        }

        return result.ToString();
    }

    /// <summary>Every fragment name a body references, in order — the seam the data tests check.</summary>
    public static IReadOnlyList<string> IncludeKeys(string? body) =>
        string.IsNullOrEmpty(body)
            ? []
            : [.. SplitLines(body).Select(line => TryIncludeKey(line, out var key) ? key : null).OfType<string>()];

    private static bool TryIncludeKey(string line, out string key)
    {
        key = string.Empty;
        var trimmed = line.Trim();

        if (!trimmed.StartsWith("{{include:", StringComparison.OrdinalIgnoreCase)
            || !trimmed.EndsWith("}}", StringComparison.Ordinal)
            || trimmed.Length <= "{{include:".Length + 2)
        {
            return false;
        }

        key = trimmed["{{include:".Length..^2].Trim();
        return key.Length > 0;
    }

    /// <summary>Parses body text into blocks. Blank lines separate blocks; unknown syntax stays text.</summary>
    public static IReadOnlyList<MarkdownBlock> Parse(string? body)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrWhiteSpace(body))
        {
            return blocks;
        }

        var paragraph = new List<string>();
        var orderedNumber = 0;

        foreach (var raw in SplitLines(body))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                FlushParagraph();
                orderedNumber = 0;
                continue;
            }

            if (TryHeading(line, out var level, out var headingText))
            {
                FlushParagraph();
                orderedNumber = 0;
                blocks.Add(new MarkdownBlock(
                    level == 2 ? MarkdownBlockKind.Heading : MarkdownBlockKind.SubHeading,
                    ParseInlines(headingText)));
                continue;
            }

            if (TryImage(line, out var altText, out var imageUrl))
            {
                FlushParagraph();
                // Deliberately does not reset the counter: a screenshot placed between two steps
                // illustrates them, so "1. / picture / 2." must keep numbering rather than restart.
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Image, ParseInlines(altText), Url: imageUrl));
                continue;
            }

            if (TryBullet(line, out var bulletText))
            {
                FlushParagraph();
                orderedNumber = 0;
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Bullet, ParseInlines(bulletText)));
                continue;
            }

            if (TryOrdered(line, out var orderedText))
            {
                FlushParagraph();
                // Renumber sequentially rather than trusting the source: guides get reordered and
                // "1. 2. 2." should still read 1, 2, 3.
                orderedNumber++;
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.OrderedItem, ParseInlines(orderedText), orderedNumber));
                continue;
            }

            orderedNumber = 0;
            paragraph.Add(line);
        }

        FlushParagraph();
        return blocks;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            // Consecutive non-blank lines are one paragraph (soft wraps), as in Markdown.
            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, ParseInlines(string.Join(" ", paragraph))));
            paragraph.Clear();
        }
    }

    private static List<string> SplitLines(string text) =>
        [.. text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')];

    private static bool TryHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        var hashes = 0;
        while (hashes < line.Length && line[hashes] == '#')
        {
            hashes++;
        }

        // Only ## and ### are meaningful here; a bare # would compete with the page title.
        if (hashes is < 2 or > 3 || hashes >= line.Length || line[hashes] != ' ')
        {
            return false;
        }

        level = hashes;
        text = line[(hashes + 1)..].Trim();
        return text.Length > 0;
    }

    /// <summary>
    /// Matches <c>![alt](url)</c> filling the whole line. Block-level only: an image sitting inside a
    /// sentence has nowhere sensible to go in a TextBlock stack, so it stays literal text instead.
    /// </summary>
    private static bool TryImage(string line, out string alt, out string url)
    {
        alt = string.Empty;
        url = string.Empty;

        if (line.Length < 5 || !line.StartsWith("![", StringComparison.Ordinal) || line[^1] != ')')
        {
            return false;
        }

        var separator = line.IndexOf("](", 2, StringComparison.Ordinal);
        if (separator < 0)
        {
            return false;
        }

        alt = line[2..separator].Trim();
        url = line[(separator + 2)..^1].Trim();
        return url.Length > 0;
    }

    private static bool TryBullet(string line, out string text)
    {
        text = string.Empty;

        if (line.Length < 3 || line[0] is not ('-' or '*') || line[1] != ' ')
        {
            return false;
        }

        text = line[2..].Trim();
        return text.Length > 0;
    }

    private static bool TryOrdered(string line, out string text)
    {
        text = string.Empty;

        var digits = 0;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]))
        {
            digits++;
        }

        if (digits == 0 || digits + 1 >= line.Length || line[digits] != '.' || line[digits + 1] != ' ')
        {
            return false;
        }

        text = line[(digits + 2)..].Trim();
        return text.Length > 0;
    }

    /// <summary>
    /// Splits a line into formatted runs. Single pass, no nesting: <c>**bold `code`**</c> renders the
    /// code run without the bold. That is a knowing simplification — guides do not need nesting, and
    /// a real inline parser is far more machinery than this content justifies.
    /// </summary>
    private static IReadOnlyList<MarkdownInline> ParseInlines(string text)
    {
        var runs = new List<MarkdownInline>();
        var pending = new StringBuilder();
        var index = 0;

        while (index < text.Length)
        {
            if (TryMarker(text, index, "**", out var boldEnd, out var boldText))
            {
                Flush();
                runs.Add(new MarkdownInline(boldText, Bold: true));
                index = boldEnd;
                continue;
            }

            if (TryMarker(text, index, "*", out var italicEnd, out var italicText))
            {
                Flush();
                runs.Add(new MarkdownInline(italicText, Italic: true));
                index = italicEnd;
                continue;
            }

            if (TryMarker(text, index, "`", out var codeEnd, out var codeText))
            {
                Flush();
                runs.Add(new MarkdownInline(codeText, Code: true));
                index = codeEnd;
                continue;
            }

            if (TryLink(text, index, out var linkEnd, out var label, out var url))
            {
                Flush();
                runs.Add(new MarkdownInline(label, Link: url));
                index = linkEnd;
                continue;
            }

            pending.Append(text[index]);
            index++;
        }

        Flush();

        // A blank line would otherwise produce no runs at all; callers expect at least one.
        return runs.Count > 0 ? runs : [new MarkdownInline(text)];

        void Flush()
        {
            if (pending.Length > 0)
            {
                runs.Add(new MarkdownInline(pending.ToString()));
                pending.Clear();
            }
        }
    }

    private static bool TryMarker(string text, int start, string marker, out int end, out string inner)
    {
        end = start;
        inner = string.Empty;

        if (!text.AsSpan(start).StartsWith(marker))
        {
            return false;
        }

        var contentStart = start + marker.Length;
        var close = text.IndexOf(marker, contentStart, StringComparison.Ordinal);
        if (close < 0)
        {
            // Unclosed — treat the marker as literal text.
            return false;
        }

        var content = text[contentStart..close];
        if (string.IsNullOrWhiteSpace(content))
        {
            // `**` or `** **`: emphasis needs something to emphasise. Whitespace-only content is
            // not a run, so the markers stay literal rather than becoming a bold space.
            return false;
        }

        inner = content;
        end = close + marker.Length;
        return true;
    }

    private static bool TryLink(string text, int start, out int end, out string label, out string url)
    {
        end = start;
        label = string.Empty;
        url = string.Empty;

        if (text[start] != '[')
        {
            return false;
        }

        var labelEnd = text.IndexOf(']', start + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0)
        {
            return false;
        }

        label = text[(start + 1)..labelEnd];
        url = text[(labelEnd + 2)..urlEnd];
        end = urlEnd + 1;
        return label.Length > 0 && url.Length > 0;
    }
}
