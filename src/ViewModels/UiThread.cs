namespace WikeloContractor.ViewModels;

/// <summary>
/// Marshalling for service events that a view model turns into UI state.
/// <para>
/// Services raise <c>Changed</c> from whichever thread did the work — catalog enrichment finishes on
/// a background thread, the inventory store can be written from either side — so every fan-out onto
/// observable properties has to land on the dispatcher. One home for that, rather than the rule being
/// re-derived (and the null case re-forgotten) in each view model.
/// </para>
/// </summary>
internal static class UiThread
{
    /// <summary>
    /// Runs <paramref name="action"/> on the dispatcher, or inline when there is no
    /// <see cref="Application"/> — which is the case in the pure unit-test tiers, where a view model
    /// under test must not need a window.
    /// </summary>
    public static void Invoke(Action action)
    {
        if (Application.Current is { } app)
        {
            app.Dispatcher.Invoke(action);
            return;
        }

        action();
    }
}
