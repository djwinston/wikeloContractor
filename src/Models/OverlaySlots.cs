using System.Windows.Input;

namespace WikeloContractor.Models;

/// <summary>
/// The overlay's fixed slot grid: how many there are, and how a slot maps onto the digit that drives
/// it. One home for the mapping — the settings hint, the overlay badge and the hotkey plan must all
/// agree that slot 10 is the "0" key.
/// </summary>
public static class OverlaySlots
{
    /// <summary>
    /// How many items the overlay holds. Ten is not arbitrary: it is exactly the main-row digits, so
    /// every slot gets a hotkey without a second modifier tier.
    /// </summary>
    public const int MaxSlots = 10;

    /// <summary>
    /// The digit key for a 1-based slot; <see cref="Key.None"/> when out of range. Slot 10 is "0",
    /// matching how the digits sit on the keyboard.
    /// <para>
    /// Main-row digits only. The numpad is deliberately not registered — Star Citizen binds it
    /// heavily, and stealing those globally would break the game's own controls.
    /// </para>
    /// </summary>
    public static Key KeyFor(int slot) => slot switch
    {
        >= 1 and <= 9 => Key.D1 + (slot - 1),
        MaxSlots => Key.D0,
        _ => Key.None,
    };

    /// <summary>The badge text for a slot: "1".."9", then "0".</summary>
    public static string DigitLabel(int slot) => slot == MaxSlots ? "0" : slot.ToString();

    /// <summary>Whether a 1-based slot number is within the grid.</summary>
    public static bool IsValidSlot(int slot) => slot is >= 1 and <= MaxSlots;
}
