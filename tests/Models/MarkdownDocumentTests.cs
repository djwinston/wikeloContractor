using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// The sourcing guides' Markdown subset. Pure by design — no WPF <c>Application</c> needed, which is
/// the point of keeping the parser out of the viewer control.
/// </summary>
public class MarkdownDocumentTests
{
    [Fact]
    public void Front_matter_is_split_from_the_body()
    {
        var (front, body) = MarkdownDocument.SplitFrontMatter("---\nname: Carinite\nsummary: An ore.\n---\n\nBody text.");

        Assert.Equal("Carinite", front["name"]);
        Assert.Equal("An ore.", front["summary"]);
        Assert.Equal("Body text.", body.Trim());
    }

    [Fact]
    public void Front_matter_keys_are_case_insensitive() =>
        Assert.Equal("Carinite", MarkdownDocument.SplitFrontMatter("---\nName: Carinite\n---\n").FrontMatter["name"]);

    [Fact]
    public void Quoted_front_matter_values_lose_their_quotes()
    {
        var (front, _) = MarkdownDocument.SplitFrontMatter("---\nname: \"Carinite (Pure)\"\nsummary: ''\n---\n");

        Assert.Equal("Carinite (Pure)", front["name"]);
        Assert.Equal(string.Empty, front["summary"]);
    }

    [Fact]
    public void A_value_containing_a_colon_keeps_everything_after_the_first_one() =>
        Assert.Equal(
            "See: sc-trade.tools",
            MarkdownDocument.SplitFrontMatter("---\nsummary: See: sc-trade.tools\n---\n").FrontMatter["summary"]);

    [Fact]
    public void A_file_without_front_matter_is_all_body()
    {
        var (front, body) = MarkdownDocument.SplitFrontMatter("## Heading\n\nText.");

        Assert.Empty(front);
        Assert.Equal("## Heading\n\nText.", body);
    }

    [Fact]
    public void Unterminated_front_matter_is_not_swallowed()
    {
        // Losing the whole file to a missing closing --- would be worse than showing the markers.
        var (front, body) = MarkdownDocument.SplitFrontMatter("---\nname: Carinite\n\nBody.");

        Assert.Empty(front);
        Assert.Contains("Body.", body);
    }

    [Theory]
    [InlineData("## Where to find it", MarkdownBlockKind.Heading)]
    [InlineData("### Details", MarkdownBlockKind.SubHeading)]
    [InlineData("- a bullet", MarkdownBlockKind.Bullet)]
    [InlineData("* also a bullet", MarkdownBlockKind.Bullet)]
    [InlineData("1. a step", MarkdownBlockKind.OrderedItem)]
    [InlineData("plain prose", MarkdownBlockKind.Paragraph)]
    public void Block_kinds_are_recognised(string line, MarkdownBlockKind expected) =>
        Assert.Equal(expected, Assert.Single(MarkdownDocument.Parse(line)).Kind);

    [Fact]
    public void A_single_hash_is_not_a_heading()
    {
        // A bare # would compete with the page's own title, so it stays prose.
        var block = Assert.Single(MarkdownDocument.Parse("# Title"));

        Assert.Equal(MarkdownBlockKind.Paragraph, block.Kind);
    }

    [Fact]
    public void Ordered_items_are_renumbered_sequentially()
    {
        // Guides get reordered by hand; "1. 1. 1." must still read 1, 2, 3.
        var numbers = MarkdownDocument.Parse("1. one\n1. two\n1. three")
            .Select(b => b.Number)
            .ToList();

        Assert.Equal([1, 2, 3], numbers);
    }

    [Fact]
    public void A_blank_line_restarts_the_numbering()
    {
        var numbers = MarkdownDocument.Parse("1. one\n2. two\n\n1. fresh")
            .Where(b => b.Kind == MarkdownBlockKind.OrderedItem)
            .Select(b => b.Number)
            .ToList();

        Assert.Equal([1, 2, 1], numbers);
    }

    [Fact]
    public void Consecutive_lines_join_into_one_paragraph()
    {
        var block = Assert.Single(MarkdownDocument.Parse("first line\nsecond line"));

        Assert.Equal("first line second line", block.PlainText);
    }

    [Fact]
    public void Blank_lines_separate_paragraphs() =>
        Assert.Equal(2, MarkdownDocument.Parse("one\n\ntwo").Count);

    [Fact]
    public void Bold_italic_and_code_runs_are_marked()
    {
        var inlines = Assert.Single(MarkdownDocument.Parse("a **b** c *d* e `f`")).Inlines;

        Assert.Contains(inlines, i => i.Text == "b" && i.Bold);
        Assert.Contains(inlines, i => i.Text == "d" && i.Italic);
        Assert.Contains(inlines, i => i.Text == "f" && i.Code);
    }

    [Fact]
    public void Bold_wins_over_italic_for_a_double_marker()
    {
        var inlines = Assert.Single(MarkdownDocument.Parse("**strong**")).Inlines;

        var run = Assert.Single(inlines);
        Assert.True(run.Bold);
        Assert.False(run.Italic);
    }

    [Fact]
    public void Links_carry_their_target()
    {
        var inlines = Assert.Single(MarkdownDocument.Parse("see [the sheet](https://example.com/x) now")).Inlines;

        var link = Assert.Single(inlines, i => i.Link is not null);
        Assert.Equal("the sheet", link.Text);
        Assert.Equal("https://example.com/x", link.Link);
    }

    [Theory]
    [InlineData("an unclosed **marker")]
    [InlineData("empty ** ** markers")]
    [InlineData("a [broken](link")]
    [InlineData("a [broken] link")]
    public void Malformed_inline_syntax_stays_plain_text(string line)
    {
        // A malformed guide must never break the page — it degrades to prose.
        var block = Assert.Single(MarkdownDocument.Parse(line));

        Assert.Equal(line, block.PlainText);
        Assert.All(block.Inlines, i => Assert.Null(i.Link));
    }

    [Fact]
    public void Empty_input_parses_to_nothing()
    {
        Assert.Empty(MarkdownDocument.Parse(null));
        Assert.Empty(MarkdownDocument.Parse("   \n\n  "));
    }

    [Fact]
    public void Comments_are_stripped()
    {
        var stripped = MarkdownDocument.StripComments("before <!-- hidden --> after");

        Assert.Equal("before  after", stripped);
        Assert.DoesNotContain("hidden", stripped);
    }

    [Fact]
    public void A_comments_only_body_strips_to_nothing() =>
        Assert.True(string.IsNullOrWhiteSpace(MarkdownDocument.StripComments("<!--\nauthoring hints\n-->\n")));

    [Fact]
    public void An_unterminated_comment_does_not_leak_markup() =>
        Assert.Equal("visible ", MarkdownDocument.StripComments("visible <!-- never closed"));

    [Fact]
    public void Carriage_returns_do_not_break_block_detection()
    {
        var kinds = MarkdownDocument.Parse("## Heading\r\n\r\n1. step").Select(b => b.Kind).ToList();

        Assert.Equal([MarkdownBlockKind.Heading, MarkdownBlockKind.OrderedItem], kinds);
    }
}
