using System.Windows;
using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// Restoring the overlay's saved geometry. The failure this guards against is nasty: a borderless,
/// click-through, Alt-Tab-invisible window restored onto a monitor that is no longer there cannot be
/// reached by any means short of editing settings.json.
/// </summary>
public class OverlayPlacementTests
{
    private static readonly Rect _singleScreen = new(0, 0, 1920, 1080);
    private static readonly Size _minimum = new(220, 120);

    private static Rect? Clamp(double left, double top, double width = 300, double height = 400) =>
        OverlayPlacement.Clamp(left, top, width, height, _singleScreen, _minimum);

    [Fact]
    public void Geometry_already_on_screen_is_left_alone()
    {
        var placed = Clamp(100, 200);

        Assert.Equal(new Rect(100, 200, 300, 400), placed);
    }

    [Fact]
    public void Never_placed_returns_null_so_the_caller_picks_a_default() =>
        Assert.Null(OverlayPlacement.Clamp(null, null, null, null, _singleScreen, _minimum));

    [Fact]
    public void A_partially_written_geometry_is_treated_as_never_placed() =>
        Assert.Null(OverlayPlacement.Clamp(100, 100, null, 400, _singleScreen, _minimum));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Nonsense_from_a_hand_edited_file_is_treated_as_never_placed(double left) =>
        // Not clamped to an edge — we cannot tell what the user meant, so let the default win.
        Assert.Null(OverlayPlacement.Clamp(left, 0, 300, 400, _singleScreen, _minimum));

    [Fact]
    public void A_window_saved_on_a_monitor_that_is_gone_is_pulled_back()
    {
        // Second screen at x=1920 was unplugged.
        var placed = Clamp(2400, 300);

        Assert.NotNull(placed);
        Assert.True(placed!.Value.Left < _singleScreen.Right, "must not sit past the right edge");
        Assert.True(placed.Value.Right > _singleScreen.Left, "must still be reachable");
    }

    [Fact]
    public void Negative_coordinates_from_a_removed_left_hand_monitor_are_pulled_back()
    {
        var placed = Clamp(-2000, -1500);

        Assert.NotNull(placed);
        Assert.True(placed!.Value.Right > _singleScreen.Left);
        Assert.True(placed.Value.Top >= _singleScreen.Top);
    }

    [Fact]
    public void A_window_larger_than_the_screen_is_shrunk_to_fit()
    {
        var placed = OverlayPlacement.Clamp(0, 0, 5000, 4000, _singleScreen, _minimum);

        Assert.NotNull(placed);
        Assert.Equal(_singleScreen.Width, placed!.Value.Width);
        Assert.Equal(_singleScreen.Height, placed.Value.Height);
    }

    [Fact]
    public void A_window_smaller_than_usable_is_grown_to_the_minimum()
    {
        var placed = OverlayPlacement.Clamp(10, 10, 5, 5, _singleScreen, _minimum);

        Assert.NotNull(placed);
        Assert.Equal(_minimum.Width, placed!.Value.Width);
        Assert.Equal(_minimum.Height, placed.Value.Height);
    }

    [Fact]
    public void A_deliberately_half_off_screen_hud_keeps_its_position()
    {
        // Parking the overlay against an edge is a legitimate choice, so only a grabbable strip is
        // required to stay on screen — the whole window is not forced inside.
        var placed = Clamp(-100, 50);

        Assert.NotNull(placed);
        Assert.Equal(-100, placed!.Value.Left);
    }

    [Fact]
    public void Multi_monitor_coordinates_are_accepted_when_the_screen_is_still_there()
    {
        var dualScreen = new Rect(-1920, 0, 3840, 1080);

        var placed = OverlayPlacement.Clamp(-1800, 100, 300, 400, dualScreen, _minimum);

        Assert.Equal(new Rect(-1800, 100, 300, 400), placed);
    }
}
