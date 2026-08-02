using System.Windows;

namespace WikeloContractor.Services;

/// <summary>
/// The overlay window, as much of it as the coordinator needs. An interface only so
/// <see cref="IOverlayService"/> — the piece with all the decisions in it — can be tested without a
/// real <c>Window</c>, which would drag the whole WPF render stack into the E2E tier.
/// </summary>
public interface IOverlayWindow
{
    /// <summary>
    /// Shows the HUD, restoring the saved placement. Nulls mean "never placed"; the implementation
    /// clamps through <c>OverlayPlacement</c>, because only it knows the current virtual screen.
    /// </summary>
    void ShowOverlay(double? left, double? top, double? width, double? height);

    void HideOverlay();

    /// <summary>Where the window is now, or null before it has ever been shown.</summary>
    Rect? Placement { get; }

    /// <summary>Turns mouse click-through on (HUD mode) or off (interactive mode).</summary>
    void SetClickThrough(bool clickThrough);

    /// <summary>Destroys the window. Nothing may be shown afterwards.</summary>
    void CloseOverlay();
}

/// <summary>
/// Coordinates the overlay: turns hotkey presses into inventory writes and chrome changes, keeps the
/// registrations in step with the pinned items, and owns the window's lifetime and geometry.
/// </summary>
public interface IOverlayService
{
    /// <summary>
    /// Wires the hotkeys up and restores the configured startup state. Call once, after MainWindow
    /// exists — WPF assigns <c>Application.MainWindow</c> to the first window created, and the
    /// completion dialogs centre on it.
    /// </summary>
    void Initialize();

    /// <summary>Re-reads the hotkey settings and the pinned count, then re-registers.</summary>
    void ApplyHotkeys();

    void Show();

    void Hide();

    void Toggle();

    void SetInteractive(bool interactive);

    void ToggleInteractive();

    /// <summary>Puts the overlay back at a known-good position, for the Settings "Reset position" row.</summary>
    void ResetPlacement();

    bool IsShown { get; }

    bool IsInteractive { get; }

    /// <summary>Saves the geometry and closes the window. Called from the host's <c>StopAsync</c>.</summary>
    void Shutdown();
}
