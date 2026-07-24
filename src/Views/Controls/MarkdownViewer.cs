using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using WikeloContractor.Models;

namespace WikeloContractor.Views.Controls;

/// <summary>
/// Renders the <see cref="MarkdownDocument"/> subset as a stack of <see cref="TextBlock"/>s.
/// <para>
/// Deliberately not a <c>FlowDocumentScrollViewer</c>: WPF-UI does not theme it, so it would arrive
/// with its own fonts, its own scrollbar and a white page, fighting the token layer the whole design
/// system rests on (see docs/design-system.md). Building TextBlocks means every heading, step and
/// link picks up the same brushes and type ramp as the rest of the app.
/// </para>
/// </summary>
public sealed class MarkdownViewer : ContentControl
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownViewer),
        new PropertyMetadata(null, OnMarkdownChanged));

    /// <summary>The document body. Setting it re-renders; null or blank renders nothing.</summary>
    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownViewer)d).Render();

    private void Render()
    {
        var blocks = MarkdownDocument.Parse(Markdown);
        if (blocks.Count == 0)
        {
            Content = null;
            return;
        }

        var panel = new StackPanel();

        foreach (var block in blocks)
        {
            panel.Children.Add(BuildBlock(block));
        }

        Content = panel;
    }

    private TextBlock BuildBlock(MarkdownBlock block)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };

        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                text.FontSize = Size("FontSizeBodyStrong", 14);
                text.FontWeight = FontWeights.SemiBold;
                text.Margin = new Thickness(0, 16, 0, 6);
                break;

            case MarkdownBlockKind.SubHeading:
                text.FontSize = Size("FontSizeBody", 13);
                text.FontWeight = FontWeights.SemiBold;
                text.Foreground = Brush("TextFillColorSecondaryBrush");
                text.Margin = new Thickness(0, 12, 0, 4);
                break;

            case MarkdownBlockKind.Bullet:
                text.Margin = new Thickness(2, 0, 0, 5);
                text.Inlines.Add(new Run("•  ") { Foreground = Brush("TextFillColorTertiaryBrush") });
                break;

            case MarkdownBlockKind.OrderedItem:
                text.Margin = new Thickness(2, 0, 0, 5);
                // Mono, so multi-step guides keep their numbers in a column.
                text.Inlines.Add(new Run($"{block.Number}.  ")
                {
                    FontFamily = Font("MonoFontFamily"),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("TextFillColorSecondaryBrush"),
                });
                break;

            default:
                text.Margin = new Thickness(0, 0, 0, 8);
                break;
        }

        foreach (var inline in block.Inlines)
        {
            text.Inlines.Add(BuildInline(inline));
        }

        return text;
    }

    private Inline BuildInline(MarkdownInline inline)
    {
        if (inline.Link is { Length: > 0 } url)
        {
            var link = new Hyperlink(new Run(inline.Text)) { NavigateUri = SafeUri(url) };
            link.RequestNavigate += OnRequestNavigate;
            return link;
        }

        var run = new Run(inline.Text);

        if (inline.Bold)
        {
            run.FontWeight = FontWeights.SemiBold;
        }

        if (inline.Italic)
        {
            run.FontStyle = FontStyles.Italic;
        }

        if (inline.Code)
        {
            run.FontFamily = Font("MonoFontFamily");
            run.Foreground = Brush("TextFillColorSecondaryBrush");
        }

        return run;
    }

    /// <summary>
    /// Opens the target in the user's browser. Only http(s) links are ever launched — a guide is
    /// shipped content, but the %AppData% layer is user-writable, so a <c>file:</c> or custom-scheme
    /// URI must not be handed to the shell.
    /// </summary>
    private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        if (e.Uri is not { IsAbsoluteUri: true } uri || uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser association, or the shell refused — a dead link must not crash the page.
        }
    }

    /// <summary>A malformed URL in a guide should render as inert text, not throw during layout.</summary>
    private static Uri? SafeUri(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;

    private System.Windows.Media.Brush? Brush(string key) =>
        TryFindResource(key) as System.Windows.Media.Brush;

    private System.Windows.Media.FontFamily? Font(string key) =>
        TryFindResource(key) as System.Windows.Media.FontFamily;

    private double Size(string key, double fallback) =>
        TryFindResource(key) is double value ? value : fallback;
}
