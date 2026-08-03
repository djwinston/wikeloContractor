namespace WikeloContractor.Models;

/// <summary>
/// Which Win32 mechanism actually delivers a press.
/// <para>
/// The two are not interchangeable, and the difference is the whole reason this choice exists:
/// <c>RegisterHotKey</c> routes through the system's hotkey table, which a foreground application can
/// switch off for everyone — so hotkeys register without error, work on the desktop, and go silent
/// while Star Citizen is focused. Raw Input's <c>RIDEV_INPUTSINK</c> bypasses that table entirely.
/// </para>
/// </summary>
public enum HotkeyBackendKind
{
    /// <summary>Raw Input, falling back to <see cref="RegisterHotKey"/> if the subscription fails.</summary>
    Auto = 0,

    /// <summary>Raw Input only. Does not claim the combination, so the game still sees the keys.</summary>
    RawInput = 1,

    /// <summary>
    /// The classic path. Claims each combination exclusively — nothing else on the machine, Star
    /// Citizen included, sees it again while we hold it.
    /// </summary>
    RegisterHotKey = 2,
}

/// <summary>Reading of the settings string that names a <see cref="HotkeyBackendKind"/>.</summary>
public static class HotkeyBackendKinds
{
    /// <summary>
    /// Total: anything unrecognised means <see cref="HotkeyBackendKind.Auto"/>, because the source is
    /// a hand-editable settings file and a typo there must not leave the user with no hotkeys at all.
    /// </summary>
    public static HotkeyBackendKind Parse(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out HotkeyBackendKind kind) && Enum.IsDefined(kind)
            ? kind
            : HotkeyBackendKind.Auto;
}
