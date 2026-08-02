using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WikeloContractor.Interop;
using WikeloContractor.Models;
using WikeloContractor.Services;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views;

/// <summary>
/// The in-game HUD. Everything here is about the window itself — extended styles, placement, drag —
/// because every decision about <em>what</em> the overlay does lives in <see cref="OverlayService"/>
/// and <see cref="OverlayViewModel"/>, which is what keeps those testable without a window.
/// </summary>
public partial class OverlayWindow : Window, IOverlayWindow
{
    private readonly IOverlayService _service;

    /// <summary>Set before the handle exists; applied in <see cref="OnSourceInitialized"/>.</summary>
    private bool _clickThrough = true;

    /// <summary>Last on-screen bounds, tracked as they change rather than read at shutdown.</summary>
    private Rect? _bounds;

    public OverlayWindow(OverlayViewModel viewModel, IOverlayService service)
    {
        _service = service;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Where the window last was, or null before it has ever been laid out.
    /// <para>
    /// Tracked as it moves rather than read on demand, and deliberately not <c>RestoreBounds</c>:
    /// <c>Application.Shutdown</c> closes every window <em>before</em> raising <c>Exit</c>, so by the
    /// time the host's <c>StopAsync</c> asks, the window is already gone — and the geometry the user
    /// just dragged into place would be lost every single time.
    /// </para>
    /// </summary>
    public Rect? Placement => _bounds;

    /// <inheritdoc />
    public void ShowOverlay(double? left, double? top, double? width, double? height)
    {
        var placement = OverlayPlacement.Clamp(
            left,
            top,
            width,
            height,
            new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight),
            new Size(MinWidth, MinHeight));

        // The height ALWAYS follows the content, and the saved one is deliberately ignored.
        // Restoring it made the window a fixed size, so pinning a tenth item simply clipped the
        // tenth row — no scrollbar, no sign anything was missing, and the slot counter said 10/10
        // while the overlay showed nine. A HUD that hides a row it was asked to show is worse than
        // one that grows; with ten rows maximum, growing is bounded anyway.
        SizeToContent = SizeToContent.Height;

        if (placement is { } saved)
        {
            Left = saved.Left;
            Top = saved.Top;
            Width = saved.Width;
        }
        else
        {
            // Never placed (or the saved monitor is gone): sit near the top-right of the primary
            // work area, out of the way of Star Citizen's own HUD elements.
            Left = SystemParameters.WorkArea.Right - Width - 24;
            Top = SystemParameters.WorkArea.Top + 24;
        }

        Show();
    }

    /// <inheritdoc />
    public void HideOverlay() => Hide();

    /// <inheritdoc />
    public void CloseOverlay() => Close();

    /// <inheritdoc />
    public void SetClickThrough(bool clickThrough)
    {
        _clickThrough = clickThrough;

        if (new WindowInteropHelper(this).Handle is var handle && handle != nint.Zero)
        {
            ApplyExtendedStyle(handle);
        }

        // The border is the only affordance telling the player whether clicks land here or in the
        // game, so it must change with the state, not with the mode toggle's own animation.
        Root.BorderBrush = FindResource(clickThrough ? "OverlayBorderBrush" : "OverlayInteractiveBorderBrush") as Brush;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyExtendedStyle(new WindowInteropHelper(this).Handle);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        RememberBounds();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        RememberBounds();
    }

    private void RememberBounds()
    {
        // Guard against the pre-layout state, where ActualWidth is 0 and Left/Top are NaN.
        if (!IsVisible || ActualWidth <= 0 || double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        _bounds = new Rect(Left, Top, ActualWidth, ActualHeight);
    }

    /// <summary>
    /// Keeps the window out of Alt+Tab, stops it ever taking focus, and applies click-through.
    /// <para>
    /// WS_EX_NOACTIVATE is the one that matters in game: without it, showing the HUD activates it,
    /// and a fullscreen Star Citizen minimises.
    /// </para>
    /// </summary>
    private void ApplyExtendedStyle(nint handle)
    {
        if (handle == nint.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;

        style = _clickThrough
            ? style | NativeMethods.WS_EX_TRANSPARENT
            : style & ~NativeMethods.WS_EX_TRANSPARENT;

        _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, style);
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        // Only reachable in interactive mode: the header is collapsed otherwise, and a click-through
        // window never receives the press at all.
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnLockClick(object sender, RoutedEventArgs e) => _service.SetInteractive(false);

    private void OnHideClick(object sender, RoutedEventArgs e) => _service.Hide();
}
