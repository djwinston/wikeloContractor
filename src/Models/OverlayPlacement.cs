using System.Windows;

namespace WikeloContractor.Models;

/// <summary>
/// Keeps a restored overlay reachable. Saved geometry outlives the monitor it was saved on: unplug a
/// second screen or drop the resolution and the stored coordinates point at nothing, leaving a
/// borderless click-through window somewhere no mouse can reach and no <c>Alt+Tab</c> lists.
/// <para>
/// Pure — the caller supplies the virtual screen, so this is testable without WPF.
/// </para>
/// </summary>
public static class OverlayPlacement
{
    /// <summary>How much of the window must stay inside the virtual screen to count as reachable.</summary>
    private const double _minimumVisible = 48;

    /// <summary>
    /// Clamps saved geometry into the virtual screen. Returns null when nothing was saved, or when the
    /// stored numbers are unusable — the caller then picks a default position.
    /// </summary>
    public static Rect? Clamp(double? left, double? top, double? width, double? height, Rect virtualScreen, Size minimum)
    {
        if (left is not { } l || top is not { } t || width is not { } w || height is not { } h)
        {
            return null;
        }

        // A hand-edited settings file can still contain these; treat them as "never placed" rather
        // than clamping nonsense to an edge.
        if (double.IsNaN(l) || double.IsNaN(t) || double.IsNaN(w) || double.IsNaN(h)
            || double.IsInfinity(l) || double.IsInfinity(t) || double.IsInfinity(w) || double.IsInfinity(h))
        {
            return null;
        }

        // Never larger than the screen, never smaller than usable.
        var clampedWidth = Math.Clamp(w, minimum.Width, Math.Max(minimum.Width, virtualScreen.Width));
        var clampedHeight = Math.Clamp(h, minimum.Height, Math.Max(minimum.Height, virtualScreen.Height));

        // Leave at least a grabbable strip on screen rather than forcing the whole window inside —
        // a HUD deliberately parked half off the edge is a legitimate choice.
        var visible = Math.Min(_minimumVisible, Math.Min(clampedWidth, clampedHeight));

        var clampedLeft = Math.Clamp(
            l,
            virtualScreen.Left - clampedWidth + visible,
            virtualScreen.Right - visible);

        var clampedTop = Math.Clamp(
            t,
            virtualScreen.Top,
            virtualScreen.Bottom - visible);

        return new Rect(clampedLeft, clampedTop, clampedWidth, clampedHeight);
    }
}
