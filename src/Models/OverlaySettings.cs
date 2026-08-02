namespace WikeloContractor.Models;

/// <summary>
/// Overlay preferences, persisted inside <see cref="AppSettings"/>. These are user settings, not a
/// data store, so they ride in <c>settings.json</c> rather than earning a file of their own.
/// <para>
/// Hotkeys are stored as the readable strings <see cref="HotkeyBinding.Format"/> produces, so a
/// hand-edited file stays understandable and <see cref="HotkeyBinding.TryParse"/> is the single
/// reader. An older settings file without this object deserializes to these defaults.
/// </para>
/// </summary>
public sealed class OverlaySettings
{
    /// <summary>
    /// Modifiers that, plus the slot digit, add one. Default <c>Ctrl+Alt</c> — rarely bound in Star
    /// Citizen. Note that <c>RegisterHotKey</c> is greedy: once taken, the game never sees the
    /// combination again.
    /// </summary>
    public string IncrementPattern { get; set; } = "Ctrl+Alt";

    /// <summary>Modifiers that, plus the slot digit, subtract one.</summary>
    public string DecrementPattern { get; set; } = "Ctrl+Shift";

    /// <summary>Shows or hides the overlay.</summary>
    public string ToggleOverlayKey { get; set; } = "Ctrl+Alt+O";

    /// <summary>
    /// Switches between click-through HUD and interactive mode. The most important one to keep
    /// working: without it, a click-through overlay cannot be reached with the mouse at all.
    /// </summary>
    public string ToggleInteractiveKey { get; set; } = "Ctrl+Alt+I";

    /// <summary>Whether the overlay appears on launch.</summary>
    public bool ShowOnStartup { get; set; }

    /// <summary>
    /// Last position and size in device-independent pixels; null means "never placed" and the overlay
    /// picks its own spot on first show. Restored through <see cref="OverlayPlacement.Clamp"/> so a
    /// since-unplugged monitor cannot strand it off-screen.
    /// <para>
    /// Nullable rather than a <see cref="double.NaN"/> sentinel on purpose: <c>System.Text.Json</c>
    /// throws on NaN, so a sentinel here would break saving <em>all</em> settings the first time the
    /// overlay was never placed.
    /// </para>
    /// </summary>
    public double? Left { get; set; }

    /// <inheritdoc cref="Left" />
    public double? Top { get; set; }

    /// <inheritdoc cref="Left" />
    public double? Width { get; set; }

    /// <inheritdoc cref="Left" />
    public double? Height { get; set; }
}
