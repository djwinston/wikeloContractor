using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class InventoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    public InventoryStoreTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "inventory.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    [Fact]
    public void Missing_file_yields_zero_counts()
    {
        var store = new InventoryStore(_filePath);

        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Counts_persist_across_reloads()
    {
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Gold", 3);
        await store.SetCountAsync("Carinite (Pure)", 5);

        var reloaded = new InventoryStore(_filePath);
        await reloaded.LoadAsync();

        Assert.Equal(3, reloaded.GetCount("Gold"));
        Assert.Equal(5, reloaded.GetCount("Carinite (Pure)"));
    }

    [Fact]
    public async Task Counts_are_read_case_insensitively_after_reload()
    {
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Wikelo Favor", 2);

        var reloaded = new InventoryStore(_filePath);
        await reloaded.LoadAsync();

        Assert.Equal(2, reloaded.GetCount("wikelo favor"));
    }

    [Fact]
    public async Task Setting_a_negative_count_clamps_to_zero()
    {
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Gold", -5);

        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Zero_count_removes_the_key_from_the_file()
    {
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Gold", 4);
        await store.SetCountAsync("Gold", 0);

        Assert.DoesNotContain("Gold", File.ReadAllText(_filePath));
        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Changed_fires_only_on_an_actual_change()
    {
        var store = new InventoryStore(_filePath);
        var raised = 0;
        store.Changed += (_, _) => raised++;

        await store.SetCountAsync("Gold", 1); // change
        await store.SetCountAsync("Gold", 1); // no-op (same value)
        await store.SetCountAsync("Silver", 0); // no-op (already absent)

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task SetCounts_applies_all_and_fires_changed_once()
    {
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Gold", 10);
        var raised = 0;
        store.Changed += (_, _) => raised++;

        await store.SetCountsAsync(new Dictionary<string, int>
        {
            ["Gold"] = 4,    // update
            ["Silver"] = 2,  // add
            ["Copper"] = 0,  // no-op (already absent)
        });

        Assert.Equal(4, store.GetCount("Gold"));
        Assert.Equal(2, store.GetCount("Silver"));
        Assert.Equal(1, raised); // one batched event, not one per key
    }

    [Fact]
    public async Task SetCounts_with_no_effective_change_does_not_fire()
    {
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Gold", 3);
        var raised = 0;
        store.Changed += (_, _) => raised++;

        await store.SetCountsAsync(new Dictionary<string, int>
        {
            ["Gold"] = 3,    // unchanged
            ["Silver"] = 0,  // already absent
        });

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Corrupted_file_falls_back_to_empty()
    {
        await File.WriteAllTextAsync(_filePath, "{ not json");
        var store = new InventoryStore(_filePath);

        await store.LoadAsync();

        Assert.Equal(0, store.GetCount("Gold"));
    }
}
