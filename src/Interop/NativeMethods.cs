using System.Runtime.InteropServices;

namespace WikeloContractor.Interop;

/// <summary>
/// The app's only P/Invoke surface. Everything Win32 lives here — a second declaration elsewhere is a
/// review finding, because a duplicated signature with a subtly different marshalling attribute is
/// the classic way this kind of bug hides.
/// <para>
/// <c>[LibraryImport]</c> rather than <c>[DllImport]</c>: the marshalling is source-generated and
/// verifiable at build time instead of resolved at runtime.
/// </para>
/// <para>
/// <b>Nothing here touches Star Citizen.</b> No injection, no <c>SetWindowsHookEx</c>, no reading
/// another process's memory — a topmost layered window plus <c>RegisterHotKey</c> are ordinary
/// windowing APIs that anti-cheat has no reason to object to. Keep it that way: a low-level keyboard
/// hook would look exactly like the thing EAC exists to stop.
/// </para>
/// </summary>
internal static partial class NativeMethods
{
    private const string _user32 = "user32.dll";

    /// <summary>Posted to the registering window's message queue when a hotkey fires.</summary>
    internal const int WM_HOTKEY = 0x0312;

    /// <summary>Index of the extended window style, for <c>Get/SetWindowLongPtr</c>.</summary>
    internal const int GWL_EXSTYLE = -20;

    /// <summary>Mouse input passes straight through the window to whatever is behind it.</summary>
    internal const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>Keeps the window out of Alt+Tab.</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>The window never takes focus — so showing the HUD cannot minimise a fullscreen game.</summary>
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// Parent handle that makes a window <em>message-only</em>: it is never displayed, never enumerated
    /// and has no z-order, but it still receives posted messages. Exactly what a hotkey sink wants.
    /// </summary>
    internal static readonly nint HWND_MESSAGE = -3;

    /// <summary>
    /// Claims a system-wide hotkey. Greedy: once this succeeds, no other application — including Star
    /// Citizen — sees the combination again until it is released.
    /// </summary>
    [LibraryImport(_user32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport(_user32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    // The 64-bit entry points. The release ships x64 only (vpk pack --framework net10.0-x64-desktop),
    // so the 32-bit GetWindowLongW/SetWindowLongW pair is deliberately absent rather than present as
    // an untested dead branch.
    [LibraryImport(_user32, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport(_user32, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
