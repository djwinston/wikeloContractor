using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace WikeloContractor.Views.Controls;

/// <summary>Colour role of a <see cref="StatusBadge"/>; picks the chip brush set to paint it with.</summary>
public enum StatusBadgeRole
{
    /// <summary>Green — the contract is completed.</summary>
    Success,

    /// <summary>Yellow — everything is gathered and the contract is ready to turn in.</summary>
    Caution,
}

/// <summary>
/// The small icon + label badge shown beside a contract title ("COMPLETED", "READY").
/// <para>
/// A control rather than repeated markup: the same Border/icon/text composition is needed by both
/// the catalog row and the detail header, and the two copies it replaced had already started to
/// drift. The look lives in <c>Resources/Chips.xaml</c> as the default style, so the badge follows
/// runtime theme and brand-palette swaps like every other chip.
/// </para>
/// </summary>
public sealed class StatusBadge : Control
{
    static StatusBadge() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(StatusBadge),
            new FrameworkPropertyMetadata(typeof(StatusBadge)));

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(SymbolRegular),
        typeof(StatusBadge),
        new PropertyMetadata(SymbolRegular.Empty));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusBadge),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RoleProperty = DependencyProperty.Register(
        nameof(Role),
        typeof(StatusBadgeRole),
        typeof(StatusBadge),
        new PropertyMetadata(StatusBadgeRole.Success));

    /// <summary>Glyph shown before the label.</summary>
    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Badge label; bind it to a localization key, never a literal.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Which brush set paints the badge.</summary>
    public StatusBadgeRole Role
    {
        get => (StatusBadgeRole)GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }
}
