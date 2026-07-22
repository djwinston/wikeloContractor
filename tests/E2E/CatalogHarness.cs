using System.IO;
using System.Text.Json.Nodes;
using WikeloContractor.Services;
using WikeloContractor.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// One app instance under test: the real catalog service, completion store, inventory store and
/// ViewModels, wired exactly as <c>App.xaml.cs</c> wires them, but over a per-test temp directory
/// and a <see cref="ScriptedWikiApi"/>. Only navigation is stubbed — it drives page switching,
/// which this tier does not assert.
/// </summary>
public sealed class CatalogHarness : IDisposable
{
    private readonly string _root;

    /// <summary>False for a restarted instance, so the shared directory is deleted only once.</summary>
    private readonly bool _ownsRoot;

    private CatalogHarness(ScriptedWikiApi api, string? root)
    {
        Api = api;
        _ownsRoot = root is null;
        _root = root ?? Path.Combine(Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

        _ = Directory.CreateDirectory(_root);

        // A 50 ms safety margin instead of the production 5 s: the rate-limit scenarios would
        // otherwise spend seconds of wall clock waiting out a window they only need to observe.
        Catalog = new ContractCatalogService(api, Path.Combine(_root, "cache"), TimeSpan.FromMilliseconds(50));
        Completion = new CompletionService(Path.Combine(_root, "completed.json"));
        Inventory = new InventoryStore(Path.Combine(_root, "inventory.json"));
    }

    public ScriptedWikiApi Api { get; }

    public ContractCatalogService Catalog { get; }

    public CompletionService Completion { get; }

    public InventoryStore Inventory { get; }

    public CatalogViewModel Catalogue { get; private set; } = null!;

    public ContractDetailViewModel Detail { get; private set; } = null!;

    /// <summary>The shell: owns the sync overlay and the app-wide navigation lock.</summary>
    public MainWindowViewModel Shell { get; private set; } = null!;

    /// <summary>
    /// Builds the graph on the UI thread — <see cref="RateLimitWatcher"/> owns a
    /// <see cref="System.Windows.Threading.DispatcherTimer"/>, which binds to its creating thread.
    /// </summary>
    /// <param name="root">
    /// Reuse another harness's storage to model an app restart over the same cache: the new
    /// service starts with no in-memory envelope, so it performs a real version check — which is
    /// how "launch the app with no connectivity" actually behaves.
    /// </param>
    public static async Task<CatalogHarness> CreateAsync(WpfAppFixture app, ScriptedWikiApi api, string? root = null)
    {
        var harness = new CatalogHarness(api, root);

        await app.OnUiAsync(() =>
        {
            var interaction = new ContractCompletionInteraction(harness.Completion, harness.Inventory);
            var navigation = new StubNavigationService();

            harness.Shell = new MainWindowViewModel(harness.Catalog);

            harness.Detail = new ContractDetailViewModel(
                navigation, harness.Catalog, harness.Completion, harness.Inventory, interaction);

            harness.Catalogue = new CatalogViewModel(
                harness.Catalog,
                harness.Completion,
                harness.Inventory,
                interaction,
                new RateLimitWatcher(harness.Catalog),
                navigation,
                harness.Detail);
        });

        return harness;
    }

    /// <summary>The storage root, so a follow-up harness can restart over the same cache.</summary>
    public string Root => _root;

    /// <summary>
    /// Backdates the cache's last version check so the next load actually contacts the API.
    /// Without this the service serves the cache untouched for 12 hours, which is correct
    /// behaviour but means an availability scenario would never reach the network at all.
    /// Models "the app is opened again the next day".
    /// </summary>
    public void AgeCache()
    {
        var path = Path.Combine(_root, "cache", "contracts.json");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!;

        envelope["LastVersionCheckAt"] = JsonValue.Create(DateTimeOffset.UtcNow - TimeSpan.FromDays(2));
        File.WriteAllText(path, envelope.ToJsonString());
    }

    public void Dispose()
    {
        if (!_ownsRoot)
        {
            return;
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    /// <summary>Navigation is page-switching only; no scenario here asserts on it.</summary>
    private sealed class StubNavigationService : INavigationService
    {
        public bool Navigate(Type pageType) => true;

        public bool Navigate(Type pageType, object? dataContext) => true;

        public bool Navigate(string pageIdOrTargetTag) => true;

        public bool Navigate(string pageIdOrTargetTag, object? dataContext) => true;

        public bool NavigateWithHierarchy(Type pageType) => true;

        public bool NavigateWithHierarchy(Type pageType, object? dataContext) => true;

        public INavigationView GetNavigationControl() => throw new NotSupportedException();

        public void SetNavigationControl(INavigationView navigation)
        {
        }

        public bool GoBack() => true;
    }
}
