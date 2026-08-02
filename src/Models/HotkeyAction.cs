namespace WikeloContractor.Models;

/// <summary>What a pressed global hotkey should do.</summary>
public enum HotkeyAction
{
    /// <summary>Add one to the slot's item.</summary>
    Increment,

    /// <summary>Subtract one from the slot's item.</summary>
    Decrement,

    /// <summary>Show or hide the overlay window.</summary>
    ToggleOverlay,

    /// <summary>
    /// Switch between click-through HUD and interactive mode. Registering this one matters more than
    /// the others: without it a click-through overlay cannot be reached by mouse at all.
    /// </summary>
    ToggleInteractive,
}
