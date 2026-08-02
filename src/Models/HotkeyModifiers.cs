namespace WikeloContractor.Models;

/// <summary>
/// Modifier keys of a global hotkey.
/// <para>
/// The values deliberately mirror the Win32 <c>MOD_*</c> constants (<c>MOD_ALT</c> 1,
/// <c>MOD_CONTROL</c> 2, <c>MOD_SHIFT</c> 4, <c>MOD_WIN</c> 8), so converting to what
/// <c>RegisterHotKey</c> wants is a cast rather than a mapping table. The enum itself stays free of
/// any interop reference so the hotkey model remains unit-testable without a window.
/// </para>
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}
