using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WikeloContractor.Views;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// Coming back from the tray, against a real <see cref="Window"/> — the one place in this suite that
/// needs one, because the bug this pins is invisible from managed state.
/// <para>
/// Regression: minimizing to the tray and then clicking the tray icon showed a taskbar button and no
/// window. WPF reported <c>WindowState.Normal</c> and <c>IsVisible == true</c> throughout; only
/// <c>IsIconic</c> disagreed. So these assert on the HWND, not on the properties — asserting on the
/// properties is exactly what let the bug ship.
/// </para>
/// <para>
/// The windows are 120 × 90 and parked off-screen: this runs on a developer's desktop, and a test
/// suite that flashes windows over their work is one they will stop running.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class WindowRestoreTests(WpfAppFixture app)
{
    /// <summary>
    /// Deliberately not in <c>Interop/NativeMethods</c>: production never asks Win32 what the window
    /// is doing, and adding an import nothing calls to satisfy a test would be the wrong trade. This
    /// is not a second copy of anything — no other declaration of it exists.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    private static bool IsMinimizedForReal(Window window) =>
        IsIconic(new WindowInteropHelper(window).Handle);

    /// <summary>
    /// A window that has been shown once and then minimized — and hidden too unless
    /// <paramref name="hide"/> says otherwise, which is the difference between minimize-to-tray
    /// being on and off.
    /// </summary>
    private static Window Minimized(WindowState from = WindowState.Normal, bool hide = true)
    {
        var window = new Window
        {
            Width = 120,
            Height = 90,
            Left = -30000,
            Top = -30000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };

        window.Show();
        window.WindowState = from;
        window.WindowState = WindowState.Minimized;

        if (hide)
        {
            window.Hide();
        }

        return window;
    }

    [Fact]
    public async Task A_window_hidden_while_minimized_really_comes_back()
    {
        await app.OnUiAsync(() =>
        {
            Window? window = null;

            try
            {
                window = Minimized();

                WindowRestore.Restore(window, WindowState.Normal);

                Assert.True(window.IsVisible);
                Assert.False(
                    IsMinimizedForReal(window),
                    "the HWND is still iconic — a taskbar button appears and no window does");
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public async Task A_window_minimized_from_maximized_comes_back_maximized()
    {
        await app.OnUiAsync(() =>
        {
            Window? window = null;

            try
            {
                window = Minimized(from: WindowState.Maximized);

                WindowRestore.Restore(window, WindowState.Maximized);

                Assert.False(IsMinimizedForReal(window));
                Assert.Equal(WindowState.Maximized, window.WindowState);
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public async Task Restoring_a_window_that_is_merely_minimized_works_the_same_way()
    {
        // Minimize-to-tray off: the window is in the taskbar, not hidden. Same call, same outcome —
        // the tray must not need to know which of the two states it is looking at.
        await app.OnUiAsync(() =>
        {
            Window? window = null;

            try
            {
                window = Minimized(hide: false);

                WindowRestore.Restore(window, WindowState.Normal);

                Assert.False(IsMinimizedForReal(window));
            }
            finally
            {
                window?.Close();
            }
        });
    }
}
