using System.Windows.Input;

namespace WikeloContractor.Models;

/// <summary>
/// A <see cref="HotkeyPlan"/> turned into the question the Raw Input backend actually asks of every
/// keystroke: <em>is this combination one of ours, and which one?</em>
/// <para>
/// It exists as a model rather than as private state inside the backend for two reasons. The matching
/// rule is the part worth proving — and it is provable here without a window, a message pump or a
/// keyboard. And <see cref="IsTrigger"/> gives the backend a way to discard input in one set lookup:
/// a Raw Input sink sees <b>everything</b> typed on the machine, so keystrokes that are none of our
/// business must be dropped before anything is read, stored or logged.
/// </para>
/// </summary>
public sealed class HotkeyLookup
{
    private readonly Dictionary<HotkeyBinding, HotkeyRegistration> _byBinding;
    private readonly HashSet<Key> _triggers;

    private HotkeyLookup(
        Dictionary<HotkeyBinding, HotkeyRegistration> byBinding, HashSet<Key> triggers)
    {
        _byBinding = byBinding;
        _triggers = triggers;
    }

    /// <summary>Nothing matches — the state before a plan has been applied.</summary>
    public static HotkeyLookup Empty { get; } = new([], []);

    /// <summary>
    /// Indexes a plan. The plan has already resolved collisions between our own bindings, so the last
    /// writer would never differ from the first; assigning is enough.
    /// </summary>
    public static HotkeyLookup From(HotkeyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var byBinding = new Dictionary<HotkeyBinding, HotkeyRegistration>(plan.Registrations.Count);
        var triggers = new HashSet<Key>(plan.Registrations.Count);

        foreach (var registration in plan.Registrations)
        {
            byBinding[registration.Binding] = registration;
            _ = triggers.Add(registration.Binding.Key);
        }

        return new HotkeyLookup(byBinding, triggers);
    }

    /// <summary>
    /// Whether any of our bindings ends in this key — regardless of modifiers. Deliberately cheap and
    /// deliberately first: it is the filter that keeps unrelated typing from being looked at.
    /// </summary>
    public bool IsTrigger(Key key) => _triggers.Contains(key);

    /// <summary>
    /// The registration for an exact combination, or null.
    /// <para>
    /// Modifiers must match <b>exactly</b>, mirroring <c>RegisterHotKey</c>: with <c>Ctrl+Alt+3</c>
    /// bound, pressing <c>Ctrl+Alt+Shift+3</c> does nothing. Subset matching would make the increment
    /// pattern fire on its own decrement combination the moment one is a superset of the other.
    /// </para>
    /// </summary>
    public HotkeyRegistration? Match(HotkeyModifiers modifiers, Key key) =>
        _byBinding.GetValueOrDefault(new HotkeyBinding(modifiers, key));
}
