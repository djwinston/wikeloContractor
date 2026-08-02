using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class InventoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    private readonly List<InventoryStore> _stores = [];

    public InventoryStoreTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "inventory.json");
    }

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    /// <summary>A store with the production debounce — fine for anything that flushes explicitly.</summary>
    private InventoryStore Store() => Track(new InventoryStore(_filePath));

    /// <summary>A store whose debounce is short enough for a test to wait it out.</summary>
    private InventoryStore FastStore(int quietMs = 30, int maxMs = 200) =>
        Track(new InventoryStore(_filePath, TimeSpan.FromMilliseconds(quietMs), TimeSpan.FromMilliseconds(maxMs)));

    private InventoryStore Track(InventoryStore store)
    {
        _stores.Add(store);
        return store;
    }

    /// <summary>
    /// Waits for the file to satisfy a condition, so a debounce test is not a fixed sleep. Returns
    /// false on timeout, which the assertion then reports.
    /// </summary>
    private async Task<bool> WaitForFileAsync(Func<string, bool> predicate, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;

        while (Environment.TickCount64 < deadline)
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    if (predicate(await File.ReadAllTextAsync(_filePath)))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    // The swap is mid-flight; look again.
                }
            }

            await Task.Delay(10);
        }

        return false;
    }

    [Fact]
    public void Missing_file_yields_zero_counts()
    {
        var store = Store();

        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Counts_persist_across_reloads()
    {
        var store = Store();
        await store.SetCountAsync("Gold", 3);
        await store.SetCountAsync("Carinite (Pure)", 5);
        await store.FlushAsync();

        var reloaded = Store();
        await reloaded.LoadAsync();

        Assert.Equal(3, reloaded.GetCount("Gold"));
        Assert.Equal(5, reloaded.GetCount("Carinite (Pure)"));
    }

    [Fact]
    public async Task Counts_are_read_case_insensitively_after_reload()
    {
        var store = Store();
        await store.SetCountAsync("Wikelo Favor", 2);
        await store.FlushAsync();

        var reloaded = Store();
        await reloaded.LoadAsync();

        Assert.Equal(2, reloaded.GetCount("wikelo favor"));
    }

    [Fact]
    public async Task Setting_a_negative_count_clamps_to_zero()
    {
        var store = Store();
        await store.SetCountAsync("Gold", -5);

        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Zero_count_removes_the_key_from_the_file()
    {
        var store = Store();
        await store.SetCountAsync("Gold", 4);
        await store.SetCountAsync("Gold", 0);
        await store.FlushAsync();

        Assert.DoesNotContain("Gold", await File.ReadAllTextAsync(_filePath));
        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Changed_fires_only_on_an_actual_change()
    {
        var store = Store();
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
        var store = Store();
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
        var store = Store();
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
        var store = Store();

        await store.LoadAsync();

        Assert.Equal(0, store.GetCount("Gold"));
    }

    [Fact]
    public async Task Changed_does_not_wait_for_the_disk()
    {
        // The overlay's hotkeys drive this store; a UI update gated on a file write would stutter
        // under key auto-repeat.
        var store = Store(); // full 400 ms debounce on purpose
        var seen = 0;
        store.Changed += (_, _) => seen = store.GetCount("Gold");

        await store.SetCountAsync("Gold", 7);

        Assert.Equal(7, seen);
        Assert.False(File.Exists(_filePath)); // not on disk yet, and that is the point
    }

    [Fact]
    public async Task A_burst_of_edits_coalesces_into_the_final_value()
    {
        var store = FastStore();

        for (var i = 1; i <= 40; i++)
        {
            await store.SetCountAsync("Carinite (Pure)", i);
        }

        Assert.True(
            await WaitForFileAsync(json => json.Contains("\"Carinite (Pure)\": 40")),
            "the debounced write should land with the last value");
    }

    [Fact]
    public async Task Continuous_edits_still_reach_the_disk_before_the_hard_limit()
    {
        // Holding a hotkey re-arms the quiet period forever; the max delay is what stops a long
        // press from leaving everything unwritten.
        var store = FastStore(quietMs: 10_000, maxMs: 150);

        var stop = Environment.TickCount64 + 600;
        var value = 0;
        while (Environment.TickCount64 < stop)
        {
            await store.SetCountAsync("Gold", ++value);
            await Task.Delay(10);
        }

        Assert.True(
            await WaitForFileAsync(json => json.Contains("Gold")),
            "a change must not sit unwritten past the hard flush interval");
    }

    [Fact]
    public async Task A_concurrent_storm_neither_throws_nor_loses_the_last_value()
    {
        var store = FastStore();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(async () =>
        {
            for (var i = 1; i <= 50; i++)
            {
                await store.SetCountAsync($"Item {worker}", i);
            }
        })));

        await store.FlushAsync();

        var reloaded = Store();
        await reloaded.LoadAsync();
        for (var worker = 0; worker < 8; worker++)
        {
            Assert.Equal(50, reloaded.GetCount($"Item {worker}"));
        }
    }

    [Fact]
    public async Task Flush_persists_pending_edits_synchronously()
    {
        var store = Store();
        await store.SetCountAsync("Gold", 12);

        store.Flush();

        var reloaded = Store();
        await reloaded.LoadAsync();
        Assert.Equal(12, reloaded.GetCount("Gold"));
    }

    [Fact]
    public async Task Flushing_with_nothing_pending_is_a_no_op()
    {
        var store = Store();

        await store.FlushAsync();

        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public async Task Disposing_persists_whatever_was_still_pending()
    {
        // Shutdown runs through Dispose on the DI container; the last in-game edits ride on this.
        var store = new InventoryStore(_filePath);
        await store.SetCountAsync("Gold", 5);
        store.Dispose();

        var reloaded = Store();
        await reloaded.LoadAsync();
        Assert.Equal(5, reloaded.GetCount("Gold"));
    }
}
