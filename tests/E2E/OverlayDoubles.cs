using System.Windows;
using WikeloContractor.Models;
using WikeloContractor.Services;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The <b>only</b> <see cref="IHotkeyService"/> fake — a second one is a review finding, same rule as
/// <see cref="ScriptedWikiApi"/>.
/// <para>
/// Standing in for the real service is the whole point of the seam: registering global combinations
/// from a test would steal <c>Ctrl+Alt+O</c> from whoever is at the keyboard, and whether Win32 grants
/// it depends on what else is running. <see cref="Press"/> raises the same event the real one raises
/// from <c>WM_HOTKEY</c>, and the decoding of that message is covered separately by
/// <see cref="HotkeyServiceTests"/>.
/// </para>
/// </summary>
public sealed class FakeHotkeyService : IHotkeyService
{
    /// <summary>Combinations pretend-owned by another application, so <c>Apply</c> reports a failure.</summary>
    public HashSet<HotkeyAction> RefuseToRegister { get; } = [];

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public int ApplyCount { get; private set; }

    /// <summary>The most recently applied plan, for asserting how many slot digits were requested.</summary>
    public HotkeyPlan? LastPlan { get; private set; }

    public HotkeyApplyResult LastResult { get; private set; } = HotkeyApplyResult.None;

    /// <summary>Which backend the host asked for; null until <see cref="Start"/>.</summary>
    public HotkeyBackendKind? StartedWith { get; private set; }

    public string BackendName => StartedWith?.ToString() ?? "none";

    public event EventHandler? ResultChanged;

    public event EventHandler<HotkeyPressed>? Pressed;

    public void Start(HotkeyBackendKind kind)
    {
        StartCount++;
        StartedWith = kind;
    }

    public HotkeyApplyResult Apply(HotkeyPlan plan)
    {
        ApplyCount++;
        LastPlan = plan;

        var registered = plan.Registrations.Where(r => !RefuseToRegister.Contains(r.Action)).ToList();
        var failed = plan.Registrations.Where(r => RefuseToRegister.Contains(r.Action)).ToList();

        LastResult = new HotkeyApplyResult(registered, failed, plan.Conflicts);
        ResultChanged?.Invoke(this, EventArgs.Empty);
        return LastResult;
    }

    public void Stop()
    {
        StopCount++;
        LastResult = HotkeyApplyResult.None;
    }

    /// <summary>Fires a hotkey exactly as the real service does once Windows delivers it.</summary>
    public void Press(HotkeyAction action, int slot = 0) =>
        Pressed?.Invoke(this, new HotkeyPressed(action, slot));
}

/// <summary>
/// Stands in for <c>OverlayWindow</c>. The coordinator's decisions — when to show, when to make the
/// window click-through, what geometry to save — are all observable here, without a real
/// <c>Window</c> and the render stack behind it.
/// </summary>
public sealed class FakeOverlayWindow : IOverlayWindow
{
    public int ShowCount { get; private set; }

    public int HideCount { get; private set; }

    public bool IsClosed { get; private set; }

    public bool IsVisible { get; private set; }

    /// <summary>Last value passed to <see cref="SetClickThrough"/>; null until it is first set.</summary>
    public bool? ClickThrough { get; private set; }

    /// <summary>What the last <see cref="ShowOverlay"/> was asked to restore.</summary>
    public (double? Left, double? Top, double? Width, double? Height) Restored { get; private set; }

    /// <summary>Where the window "is". Null models a window that has never been shown.</summary>
    public Rect? Placement { get; set; }

    public void ShowOverlay(double? left, double? top, double? width, double? height)
    {
        ShowCount++;
        IsVisible = true;
        Restored = (left, top, width, height);
    }

    public void HideOverlay()
    {
        HideCount++;
        IsVisible = false;
    }

    public void SetClickThrough(bool clickThrough) => ClickThrough = clickThrough;

    public void CloseOverlay()
    {
        IsClosed = true;
        IsVisible = false;
    }
}
