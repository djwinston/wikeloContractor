namespace WikeloContractor.Models;

/// <summary>One hotkey to register: its Win32 id, what it does, and which slot it belongs to.</summary>
/// <param name="Slot">1-based slot for <see cref="HotkeyAction.Increment"/> /
/// <see cref="HotkeyAction.Decrement"/>; 0 for the toggles.</param>
public sealed record HotkeyRegistration(int Id, HotkeyAction Action, int Slot, HotkeyBinding Binding);

/// <summary>
/// Two of our own hotkeys asking for the same combination. <paramref name="Kept"/> is registered and
/// <paramref name="Dropped"/> is not, so the user is told which of their bindings is doing nothing.
/// </summary>
public sealed record HotkeyConflict(HotkeyRegistration Kept, HotkeyRegistration Dropped);

/// <summary>The full set to register, plus the collisions found while building it.</summary>
public sealed record HotkeyPlan(
    IReadOnlyList<HotkeyRegistration> Registrations,
    IReadOnlyList<HotkeyConflict> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>
    /// Turns settings plus the number of pinned items into the exact set to hand to
    /// <c>RegisterHotKey</c>.
    /// <para>
    /// Only the digits actually pinned are included. Registering all twenty up front would steal
    /// twenty global combinations from the rest of the machine — and from Star Citizen — even when
    /// two items are pinned, and multiply the chance of a conflict the user then cannot diagnose.
    /// </para>
    /// <para>
    /// Collisions between our own bindings are resolved here rather than left to Win32: the toggles
    /// are added first and win, so a slot digit is what gets dropped. That ordering is deliberate —
    /// losing a slot key costs one item's hotkey, whereas losing
    /// <see cref="HotkeyAction.ToggleInteractive"/> can leave a click-through overlay unreachable.
    /// </para>
    /// </summary>
    public static HotkeyPlan Build(OverlaySettings settings, int pinnedCount)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var registrations = new List<HotkeyRegistration>(2 + (OverlaySlots.MaxSlots * 2));
        var conflicts = new List<HotkeyConflict>();
        var claimed = new Dictionary<HotkeyBinding, HotkeyRegistration>();

        void Add(HotkeyRegistration registration)
        {
            if (!registration.Binding.IsValid || registration.Binding.IsPattern)
            {
                // Unparseable, modifier-less, or a bare pattern that never got a digit — nothing to
                // register. Skipped silently: an empty binding is how a user disables one.
                return;
            }

            if (claimed.TryGetValue(registration.Binding, out var kept))
            {
                conflicts.Add(new HotkeyConflict(kept, registration));
                return;
            }

            claimed[registration.Binding] = registration;
            registrations.Add(registration);
        }

        // Toggles first so they win any tie — see the remarks above.
        if (HotkeyBinding.TryParse(settings.ToggleOverlayKey, out var toggleOverlay))
        {
            Add(new HotkeyRegistration(ToggleOverlayId, HotkeyAction.ToggleOverlay, 0, toggleOverlay));
        }

        if (HotkeyBinding.TryParse(settings.ToggleInteractiveKey, out var toggleInteractive))
        {
            Add(new HotkeyRegistration(ToggleInteractiveId, HotkeyAction.ToggleInteractive, 0, toggleInteractive));
        }

        var slots = Math.Clamp(pinnedCount, 0, OverlaySlots.MaxSlots);
        AddSlotRange(settings.IncrementPattern, HotkeyAction.Increment, IncrementIdBase);
        AddSlotRange(settings.DecrementPattern, HotkeyAction.Decrement, DecrementIdBase);

        return new HotkeyPlan(registrations, conflicts);

        void AddSlotRange(string? pattern, HotkeyAction action, int idBase)
        {
            if (!HotkeyBinding.TryParse(pattern, out var parsed) || !parsed.IsPattern)
            {
                // A pattern row must be modifiers only; a stray key there would make every slot the
                // same combination, which the collision check would then report ten times.
                return;
            }

            for (var slot = 1; slot <= slots; slot++)
            {
                Add(new HotkeyRegistration(idBase + slot, action, slot, parsed.WithKey(OverlaySlots.KeyFor(slot))));
            }
        }
    }

    // Win32 reserves 0x0000-0xBFFF for application hotkey ids. Fixed bases keep the ids stable
    // across re-applies, so unregistering by id is exact.
    private const int IncrementIdBase = 0x4B00;
    private const int DecrementIdBase = 0x4B10;

    internal const int ToggleOverlayId = 0x4B20;
    internal const int ToggleInteractiveId = 0x4B21;
}
