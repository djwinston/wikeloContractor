using System.Windows;
using System.Windows.Input;
using WikeloContractor.Models;

namespace WikeloContractor.Views.Controls;

/// <summary>
/// A read-only text box that captures a key combination instead of text. One control for both kinds
/// of row the overlay settings need: a full binding (<c>Ctrl+Alt+O</c>) and a modifier-only
/// <see cref="PatternOnly"/> pattern (<c>Ctrl+Alt</c>), which the slot digit is appended to.
/// <para>
/// Subclasses WPF-UI's TextBox rather than hand-rolling a control so it inherits the themed chrome —
/// per <c>docs/design-system.md</c>, the Fluent theme is the token layer.
/// </para>
/// </summary>
public class HotkeyBox : Wpf.Ui.Controls.TextBox
{
    /// <summary>The captured combination in <see cref="HotkeyBinding.Format"/> form.</summary>
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyChanged));

    /// <summary>Capture modifiers only — the two slot-digit rows.</summary>
    public static readonly DependencyProperty PatternOnlyProperty = DependencyProperty.Register(
        nameof(PatternOnly),
        typeof(bool),
        typeof(HotkeyBox),
        new PropertyMetadata(false));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
    }

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public bool PatternOnly
    {
        get => (bool)GetValue(PatternOnlyProperty);
        set => SetValue(PatternOnlyProperty, value);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Everything is swallowed while focused, including Tab: a capture box that lets Tab escape
        // cannot bind Tab, and the user has no way to know why.
        e.Handled = true;

        // Alt-combinations arrive as Key.System with the real key in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Escape or Key.Delete or Key.Back)
        {
            // Clearing a row is how a binding is disabled; HotkeyPlan skips an unparseable entry.
            Hotkey = string.Empty;
            return;
        }

        var modifiers = Current(Keyboard.Modifiers);

        if (IsModifier(key))
        {
            // A pattern row is nothing but modifiers, so each press updates the shown combination;
            // whatever is held when the user stops is what sticks.
            if (PatternOnly && modifiers != HotkeyModifiers.None)
            {
                Hotkey = new HotkeyBinding(modifiers, Key.None).Format();
            }

            return;
        }

        if (PatternOnly)
        {
            // A stray key in a pattern row would make every slot the same combination.
            return;
        }

        var candidate = new HotkeyBinding(modifiers, key);
        if (candidate.IsValid)
        {
            // Modifier-less bindings are rejected: owning a bare "O" globally would swallow the key
            // in every other application on the machine.
            Hotkey = candidate.Format();
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SelectAll();
    }

    private static void OnHotkeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((HotkeyBox)sender).Text = (string?)e.NewValue ?? string.Empty;

    private static HotkeyModifiers Current(ModifierKeys pressed)
    {
        var modifiers = HotkeyModifiers.None;

        if (pressed.HasFlag(ModifierKeys.Control)) { modifiers |= HotkeyModifiers.Control; }
        if (pressed.HasFlag(ModifierKeys.Alt)) { modifiers |= HotkeyModifiers.Alt; }
        if (pressed.HasFlag(ModifierKeys.Shift)) { modifiers |= HotkeyModifiers.Shift; }
        if (pressed.HasFlag(ModifierKeys.Windows)) { modifiers |= HotkeyModifiers.Win; }

        return modifiers;
    }

    private static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin
        or Key.System;
}
