using System.Windows.Input;

namespace WikeloContractor.Models;

/// <summary>
/// One global hotkey: a set of modifiers and, optionally, a key.
/// <para>
/// <see cref="Key.None"/> means <em>modifier pattern only</em> — that is how the two digit rows are
/// stored ("Ctrl+Alt", then the slot digit supplies the key). One type covers both cases so there is
/// a single parser and a single display format instead of two that drift.
/// </para>
/// <para>
/// Pure: <see cref="Key"/> is an enum and needs no WPF <c>Application</c>, so the whole hotkey model
/// is unit-testable without a window.
/// </para>
/// </summary>
public readonly record struct HotkeyBinding(HotkeyModifiers Modifiers, Key Key)
{
    /// <summary>Modifiers with no key — a template a slot digit is appended to.</summary>
    public bool IsPattern => Key == Key.None;

    /// <summary>
    /// Whether this is safe to register. A binding with no modifier is rejected on purpose: owning a
    /// bare "1" globally would swallow the digit in every other application on the machine.
    /// </summary>
    public bool IsValid => Modifiers != HotkeyModifiers.None;

    /// <summary>Expands a pattern into a concrete binding for one slot.</summary>
    public HotkeyBinding WithKey(Key key) => this with { Key = key };

    /// <summary>
    /// Display and persistence form, e.g. <c>"Ctrl+Alt"</c> or <c>"Ctrl+Alt+O"</c>. Modifier order is
    /// fixed so a round trip is stable and settings.json does not churn.
    /// </summary>
    public string Format()
    {
        var parts = new List<string>(5);

        if (Modifiers.HasFlag(HotkeyModifiers.Control)) { parts.Add("Ctrl"); }
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) { parts.Add("Alt"); }
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) { parts.Add("Shift"); }
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) { parts.Add("Win"); }
        if (!IsPattern) { parts.Add(Key.ToString()); }

        return string.Join('+', parts);
    }

    /// <summary>
    /// Reads the form <see cref="Format"/> writes. Total: any garbage returns false rather than
    /// throwing, because the source is a hand-editable settings file.
    /// </summary>
    public static bool TryParse(string? text, out HotkeyBinding binding)
    {
        binding = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        var key = Key.None;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= HotkeyModifiers.Control;
                    continue;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    continue;
                case "win" or "windows":
                    modifiers |= HotkeyModifiers.Win;
                    continue;
            }

            // Anything else must be the single key, and there can only be one.
            if (key != Key.None || !Enum.TryParse(raw, ignoreCase: true, out Key parsed) || parsed == Key.None)
            {
                return false;
            }

            key = parsed;
        }

        var candidate = new HotkeyBinding(modifiers, key);
        if (!candidate.IsValid)
        {
            return false;
        }

        binding = candidate;
        return true;
    }
}
