using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <summary>A global hotkey fired.</summary>
/// <param name="Action">What it means.</param>
/// <param name="Slot">1-based slot for the digit hotkeys; 0 for the toggles.</param>
public sealed record HotkeyPressed(HotkeyAction Action, int Slot);

/// <summary>
/// Owns the Win32 global-hotkey surface and nothing about the domain: it registers what a
/// <see cref="HotkeyPlan"/> asks for and reports presses as events. Deciding what a press <em>means</em>
/// is <c>IOverlayService</c>'s job, which is also what lets the overlay be tested by raising
/// <see cref="Pressed"/> instead of going through the OS.
/// </summary>
public interface IHotkeyService
{
    /// <summary>
    /// Creates the message sink. Must be called on the UI thread — the sink is a window, and
    /// <see cref="Pressed"/> is therefore raised on that thread too, so handlers may touch the UI.
    /// </summary>
    void Start();

    /// <summary>
    /// Replaces the live registrations with the plan's. Combinations another application already owns
    /// simply fail; the rest still register.
    /// </summary>
    HotkeyApplyResult Apply(HotkeyPlan plan);

    /// <summary>Outcome of the most recent <see cref="Apply"/>; drives the Settings conflict notice.</summary>
    HotkeyApplyResult LastResult { get; }

    /// <summary>Raised after <see cref="LastResult"/> changes.</summary>
    event EventHandler? ResultChanged;

    /// <summary>Raised on the UI thread when a registered hotkey fires.</summary>
    event EventHandler<HotkeyPressed>? Pressed;

    /// <summary>Releases every hotkey and destroys the sink. Safe to call twice.</summary>
    void Stop();
}
