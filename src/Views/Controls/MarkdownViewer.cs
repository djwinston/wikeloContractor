using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using WikeloContractor.Models;
using WikeloContractor.Views.Helpers;

namespace WikeloContractor.Views.Controls;

/// <summary>
/// Renders the <see cref="MarkdownDocument"/> subset as a stack of <see cref="TextBlock"/>s, with
/// each <c>##</c> section wrapped in a collapsible <see cref="Expander"/> and <c>![alt](url)</c>
/// blocks resolved to real pictures.
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

        // Each `##` opens a collapsible section that swallows everything up to the next one. Blocks
        // before the first heading (a guide that opens with prose) stay loose at the top.
        StackPanel? section = null;

        foreach (var block in blocks)
        {
            if (block.Kind == MarkdownBlockKind.Heading)
            {
                section = new StackPanel();
                panel.Children.Add(BuildSection(block, section, first: panel.Children.Count == 0));
                continue;
            }

            (section ?? panel).Children.Add(BuildBlock(block));
        }

        Content = panel;
    }

    /// <summary>
    /// Wraps one <c>##</c> section in an <see cref="Expander"/> so a long step list can be folded
    /// away. Expanded by default — a guide is opened to be read, not to be unfolded first. The stock
    /// control is used deliberately: WPF-UI themes it, so it lands on the same token layer as the
    /// rest of the page (see docs/design-system.md).
    /// </summary>
    private Expander BuildSection(MarkdownBlock heading, StackPanel body, bool first)
    {
        var header = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = Size("FontSizeBodyStrong", 14),
            FontWeight = FontWeights.SemiBold,
        };

        foreach (var inline in heading.Inlines)
        {
            header.Inlines.Add(BuildInline(inline));
        }

        return new Expander
        {
            Header = header,
            Content = body,
            IsExpanded = true,
            Margin = new Thickness(0, first ? 0 : 8, 0, 0),
        };
    }

    private UIElement BuildBlock(MarkdownBlock block)
    {
        if (block.Kind == MarkdownBlockKind.Image)
        {
            return BuildImage(block);
        }

        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };

        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                // Only reached if a heading arrives outside the sectioning above; kept so the block
                // still renders as a heading instead of silently falling through to paragraph.
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
                var marker = new Run($"{block.Number}.  ")
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("TextFillColorSecondaryBrush"),
                };

                ApplyMonoFont(marker);
                text.Inlines.Add(marker);
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

    /// <summary>
    /// Renders <c>![alt](url)</c>. Resolution goes through <see cref="ThumbnailLoader"/>, the same
    /// path the reward and inventory thumbs use, so a remote slide is disk-cached once and a bundled
    /// picture resolves against the install directory. Loading is fire-and-forget: the element is
    /// returned empty and fills in when the bytes arrive, and a missing image simply stays blank.
    /// </summary>
    private static UIElement BuildImage(MarkdownBlock block)
    {
        var image = new Image
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            // Never upscale a small screenshot to the card width — it would only go soft.
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxHeight = 320,
            Margin = new Thickness(0, 4, 0, 10),
        };

        var caption = block.PlainText;
        if (caption.Length > 0)
        {
            image.ToolTip = caption;
        }

        if (block.Url is { Length: > 0 } reference && IsSafeImageReference(reference))
        {
            _ = LoadImageAsync(image, reference);
        }

        return image;
    }

    private static async Task LoadImageAsync(Image target, string reference)
    {
        try
        {
            // 640px: wide enough for a map slide in the card, cheap enough to decode off-thread.
            var source = await ThumbnailLoader.ResolveAsync([reference], decodePixelWidth: 640);
            if (source is not null)
            {
                target.Source = source;
            }
        }
        catch (Exception)
        {
            // A guide is partly user-authored content; a bad picture must never take the page down.
        }
    }

    /// <summary>
    /// Guides are shipped content, but the <c>%AppData%</c> layer is user-writable, so an image
    /// reference is only followed when it is a web image or a plain local path. Anything carrying
    /// another scheme (<c>data:</c>, <c>javascript:</c>, …) is dropped rather than handed onwards.
    /// </summary>
    private static bool IsSafeImageReference(string reference) =>
        !Uri.TryCreate(reference, UriKind.Absolute, out var uri) || uri.Scheme is "http" or "https" or "file";

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
            ApplyMonoFont(run);
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

    /// <summary>
    /// Switches a run to the mono face, but only once the resource actually resolves. A null
    /// <c>Foreground</c> is harmless, a null <c>FontFamily</c> throws — so an unresolved key has to
    /// leave the run as it is instead of taking the whole guide page down with it.
    /// </summary>
    private void ApplyMonoFont(Run run)
    {
        if (Font("MonoFontFamily") is { } family)
        {
            run.FontFamily = family;
        }
    }

    private double Size(string key, double fallback) =>
        TryFindResource(key) is double value ? value : fallback;
}
