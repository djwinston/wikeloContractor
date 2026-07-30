using System.Windows.Controls;
using System.Windows.Documents;
using WikeloContractor.Views.Controls;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The structure <see cref="MarkdownViewer"/> builds from a guide: sections folded into expanders and
/// images turned into real elements. Needs the STA fixture because these are WPF controls, but it
/// asserts the visual *tree*, not its appearance — the fixture deliberately does not merge WPF-UI's
/// control dictionaries, so theming stays covered by the manual smoke run.
/// </summary>
[Collection("WpfApp")]
public sealed class MarkdownViewerScenarios
{
    private readonly WpfAppFixture _app;

    public MarkdownViewerScenarios(WpfAppFixture app) => _app = app;

    /// <summary>
    /// Renders and inspects on the UI thread. WPF objects have thread affinity, so the assertions
    /// have to run there too — handing the tree back to the test thread throws on first touch.
    /// </summary>
    private Task InspectAsync(string markdown, Action<StackPanel> assert) =>
        _app.OnUiAsync(() => assert(Assert.IsType<StackPanel>(new MarkdownViewer { Markdown = markdown }.Content)));

    [Fact]
    public Task Each_heading_becomes_its_own_expanded_section() =>
        InspectAsync("## Where to find it\n\nProse.\n\n## Step by step\n\n1. Go there.", root =>
        {
            var sections = root.Children.OfType<Expander>().ToList();

            Assert.Equal(2, sections.Count);
            Assert.All(sections, section => Assert.True(section.IsExpanded));
            Assert.Equal("Step by step", HeaderText(sections[1]));
        });

    /// <summary>The header is built from inline runs, so <c>TextBlock.Text</c> reads back empty.</summary>
    private static string HeaderText(Expander section) =>
        string.Concat(((TextBlock)section.Header).Inlines.OfType<Run>().Select(run => run.Text));

    [Fact]
    public Task A_sections_blocks_live_inside_that_section() =>
        InspectAsync("## Step by step\n\n1. First.\n2. Second.", root =>
        {
            var body = Assert.IsType<StackPanel>(Assert.Single(root.Children.OfType<Expander>()).Content);

            Assert.Equal(2, body.Children.Count);
            Assert.Empty(root.Children.OfType<TextBlock>());
        });

    [Fact]
    public Task Prose_before_the_first_heading_stays_outside_any_section() =>
        InspectAsync("Intro line.\n\n## Step by step\n\n1. Go.", root =>
        {
            Assert.Single(root.Children.OfType<TextBlock>());
            Assert.Single(root.Children.OfType<Expander>());
        });

    [Fact]
    public Task An_image_block_becomes_an_image_element() =>
        InspectAsync("## Maps\n\n![Site B](https://example.com/map.png)", root =>
        {
            var body = Assert.IsType<StackPanel>(Assert.Single(root.Children.OfType<Expander>()).Content);
            var image = Assert.IsType<Image>(Assert.Single(body.Children));

            // The alt text survives as the tooltip; the bitmap itself arrives asynchronously (or
            // never, for an unreachable host) and is not what this asserts.
            Assert.Equal("Site B", image.ToolTip);
        });

    [Fact]
    public Task A_guide_with_no_body_renders_nothing() =>
        _app.OnUiAsync(() => Assert.Null(new MarkdownViewer { Markdown = "   " }.Content));
}
