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
/// another process's memory — a topmost layered window, <c>RegisterHotKey</c> and a Raw Input sink are
/// ordinary windowing APIs that anti-cheat has no reason to object to. Keep it that way: a low-level
/// keyboard hook would look exactly like the thing EAC exists to stop. Raw Input is <em>passive</em> —
/// it observes, it cannot swallow or alter a keystroke, which is precisely what separates it from a
/// hook.
/// </para>
/// </summary>
internal static partial class NativeMethods
{
    private const string _user32 = "user32.dll";

    /// <summary>Posted to the registering window's message queue when a hotkey fires.</summary>
    internal const int WM_HOTKEY = 0x0312;

    /// <summary>
    /// Posted when a Raw Input device we subscribed to produced input. Unlike <see cref="WM_HOTKEY"/>
    /// this must still reach <c>DefWindowProc</c> afterwards so the system can clean the buffer up —
    /// never mark it handled.
    /// </summary>
    internal const int WM_INPUT = 0x00FF;

    /// <summary>
    /// The shell's "the notification area exists again" broadcast, looked up by name because its
    /// number is assigned at runtime. Explorer sends it to every top-level window after it restarts,
    /// and it is the only notice an application gets that its tray icon is gone — see
    /// <c>docs/ui-notes.md</c>, "Notification area".
    /// </summary>
    internal const string TaskbarCreatedMessage = "TaskbarCreated";

    /// <summary>Index of the extended window style, for <c>Get/SetWindowLongPtr</c>.</summary>
    internal const int GWL_EXSTYLE = -20;

    /// <summary>Mouse input passes straight through the window to whatever is behind it.</summary>
    internal const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>Keeps the window out of Alt+Tab.</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>The window never takes focus — so showing the HUD cannot minimise a fullscreen game.</summary>
    internal const int WS_EX_NOACTIVATE = 0x08000000;

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
    /// <summary>
    /// Resolves a system-wide message name to the number it was assigned this boot. Every caller
    /// asking for the same name gets the same value, which is how a broadcast like
    /// <see cref="TaskbarCreatedMessage"/> can be recognised in a window procedure.
    /// </summary>
    [LibraryImport(_user32, EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string lpString);

    [LibraryImport(_user32, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport(_user32, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    // ---------------------------------------------------------------------------------------------
    // Raw Input. The reason it exists here at all: RegisterHotKey delivers through the system's
    // hotkey table, and a foreground application can shut that path down for everyone (Raw Input's
    // own RIDEV_NOHOTKEYS does exactly that) — which is why our hotkeys register cleanly, work on the
    // desktop, and go silent the moment Star Citizen is focused. RIDEV_INPUTSINK does not use that
    // table: the system posts WM_INPUT straight to the window that asked, whoever is in front.
    // ---------------------------------------------------------------------------------------------

    /// <summary>HID usage page for generic desktop controls — keyboards, mice, joysticks.</summary>
    internal const ushort HID_USAGE_PAGE_GENERIC = 0x01;

    /// <summary>HID usage identifying a keyboard within <see cref="HID_USAGE_PAGE_GENERIC"/>.</summary>
    internal const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

    /// <summary>Stops receiving input from a usage page; the target handle must be null with it.</summary>
    internal const uint RIDEV_REMOVE = 0x00000001;

    /// <summary>
    /// Deliver input even while the target window is in the background. The whole point of this
    /// backend — and the reason the target may not be a <em>message-only</em> window, which never
    /// receives <c>WM_INPUT</c>.
    /// </summary>
    internal const uint RIDEV_INPUTSINK = 0x00000100;

    /// <summary><c>GetRawInputData</c> command asking for the input payload rather than the header.</summary>
    internal const uint RID_INPUT = 0x10000003;

    /// <summary><c>RAWINPUTHEADER.Type</c> for keyboard input (mouse is 0, HID is 2).</summary>
    internal const uint RIM_TYPEKEYBOARD = 1;

    /// <summary><c>RAWKEYBOARD.Flags</c> bit meaning the key went <em>up</em>.</summary>
    internal const ushort RI_KEY_BREAK = 0x01;

    /// <summary>
    /// The "no key" virtual code some keyboards emit as part of a multi-key sequence. Never a real
    /// press, and must be dropped before it reaches any lookup.
    /// </summary>
    internal const int VK_NONE = 0xFF;

    internal const int VK_SHIFT = 0x10;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_MENU = 0x12;
    internal const int VK_LWIN = 0x5B;
    internal const int VK_RWIN = 0x5C;

    /// <summary><c>RAWINPUTDEVICE</c>: one usage page to subscribe to, and where to deliver it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        internal ushort UsagePage;
        internal ushort Usage;
        internal uint Flags;
        internal nint Target;
    }

    /// <summary><c>RAWINPUTHEADER</c>: which kind of device produced the payload that follows.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        internal uint Type;
        internal uint Size;
        internal nint Device;
        internal nint WParam;
    }

    /// <summary><c>RAWKEYBOARD</c>: one make/break code and its virtual key.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawKeyboard
    {
        internal ushort MakeCode;
        internal ushort Flags;
        internal ushort Reserved;
        internal ushort VKey;
        internal uint Message;
        internal uint ExtraInformation;
    }

    /// <summary>
    /// <c>RAWINPUT</c> narrowed to its keyboard arm. Only keyboards are subscribed to, so the union's
    /// other members can never arrive and modelling them would only invite reading the wrong one.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputKeyboard
    {
        internal RawInputHeader Header;
        internal RawKeyboard Keyboard;
    }

    /// <summary>
    /// Subscribes this process to a raw device usage page. Passed by <c>ref</c> rather than as an
    /// array because exactly one device is ever registered, and a pointer to a single element is the
    /// same thing to Win32 as a one-element array.
    /// </summary>
    [LibraryImport(_user32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterRawInputDevices(ref RawInputDevice pRawInputDevices, uint uiNumDevices, uint cbSize);

    /// <summary>
    /// Copies the payload behind a <c>WM_INPUT</c> handle. Returns the byte count written, or
    /// <c>(uint)-1</c> on failure — including when the buffer is too small.
    /// </summary>
    [LibraryImport(_user32, SetLastError = true)]
    internal static partial uint GetRawInputData(
        nint hRawInput, uint uiCommand, ref RawInputKeyboard pData, ref uint pcbSize, uint cbSizeHeader);

    /// <summary>
    /// Physical key state, independent of which window has focus. Used to read the modifiers at the
    /// moment a Raw Input key goes down: tracking them from the raw stream ourselves would leave a
    /// modifier stuck "held" whenever it was released while the secure desktop (a UAC prompt) was up.
    /// </summary>
    [LibraryImport(_user32)]
    internal static partial short GetAsyncKeyState(int vKey);
}
