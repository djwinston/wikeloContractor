namespace WikeloContractor.Models;

/// <summary>
/// What actually happened when a <see cref="HotkeyPlan"/> was handed to Win32.
/// <para>
/// Partial failure is normal and is <b>not</b> rolled back: another application may already own one
/// combination, and giving up the other nineteen because of it would be worse than useless. The
/// Settings page reports the losers so the user can rebind them.
/// </para>
/// </summary>
public sealed record HotkeyApplyResult(
    IReadOnlyList<HotkeyRegistration> Registered,
    IReadOnlyList<HotkeyRegistration> Failed,
    IReadOnlyList<HotkeyConflict> Conflicts)
{
    /// <summary>Whether at least one hotkey for an action is live.</summary>
    public bool IsRegistered(HotkeyAction action) =>
        Registered.Any(registration => registration.Action == action);

    /// <summary>An empty result — the state before anything has been applied.</summary>
    public static HotkeyApplyResult None { get; } = new([], [], []);
}
