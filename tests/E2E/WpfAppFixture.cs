using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// Hosts one real WPF <see cref="Application"/> on an STA thread so ViewModels can be exercised
/// end to end. The ViewModels marshal background service events through
/// <c>Application.Current.Dispatcher</c> — the pitfall <c>docs/data-pipeline.md</c> calls out —
/// so a fake dispatcher would test the wrong thing. This runs the real one.
/// <para>
/// Only <c>Strings.en.xaml</c> is merged: <see cref="WikeloContractor.ViewModels.Localized"/> is
/// the sole resource lookup the ViewModel layer performs. Brushes, geometry and control templates
/// are read by XAML at render time, which this tier does not exercise — merging WPF-UI's control
/// dictionaries into a test host would buy load time and fragility, nothing else. Rendering stays
/// covered by the manual smoke run, and localization key parity by LocalizationParityTests.
/// </para>
/// </summary>
public sealed class WpfAppFixture : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher _dispatcher = null!;

    public WpfAppFixture()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _thread = new Thread(() =>
        {
            // Constructing Application sets Application.Current and binds it to this dispatcher.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // Long-form pack URI: the short form resolves against the entry assembly, which here
            // is the test host, not WikeloContractor.
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/WikeloContractor;component/Resources/Localization/Strings.en.xaml"),
            });

            _dispatcher = Dispatcher.CurrentDispatcher;
            ready.SetResult();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Task.Wait(TimeSpan.FromSeconds(30));
    }

    /// <summary>Runs a synchronous action on the UI thread.</summary>
    public Task OnUiAsync(Action action) => _dispatcher.InvokeAsync(action).Task;

    /// <summary>Reads a value from the UI thread.</summary>
    public Task<T> OnUiAsync<T>(Func<T> read) => _dispatcher.InvokeAsync(read).Task;

    /// <summary>
    /// Starts an async operation on the UI thread and awaits it from the caller.
    /// Never block the UI thread on the returned task: background enrichment marshals into this
    /// same dispatcher, so a blocking wait here deadlocks the app under test.
    /// </summary>
    public Task OnUiAsync(Func<Task> operation) => _dispatcher.InvokeAsync(operation).Task.Unwrap();

    /// <summary>
    /// Polls a UI-thread condition until it holds, or fails the test on timeout. Used for state
    /// that has no event of its own; prefer a TaskCompletionSource on a service event where one
    /// exists (see docs/testing.md — never "delay and hope").
    /// </summary>
    public async Task WaitUntilAsync(Func<bool> condition, string because, int timeoutMs = 15_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;

        while (Environment.TickCount64 < deadline)
        {
            if (await OnUiAsync(condition))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"Timed out after {timeoutMs} ms waiting for: {because}");
    }

    public void Dispose() => _dispatcher.InvokeShutdown();
}

/// <summary>
/// <see cref="Application.Current"/> is a per-process singleton, so every test touching the WPF
/// application must share this one fixture and must not run concurrently with another.
/// </summary>
[CollectionDefinition("WpfApp", DisableParallelization = true)]
public sealed class WpfAppCollection : ICollectionFixture<WpfAppFixture>;
