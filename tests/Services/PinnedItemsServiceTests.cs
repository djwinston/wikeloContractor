using System.IO;
using WikeloContractor.Models;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

/// <summary>
/// The pinned-items store. The rules worth pinning down are the cap (the overlay has exactly ten
/// slots and the hotkey digits assume it) and compaction (unpinning must not leave a hole, because a
/// slot number is what a hotkey selects).
/// </summary>
public sealed class PinnedItemsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    public PinnedItemsServiceTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "pinned.json");
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

    private PinnedItemsService Service() => new(_filePath);

    private static async Task FillAsync(PinnedItemsService service, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            _ = await service.PinAsync($"Item {i}");
        }
    }

    [Fact]
    public void Missing_file_yields_an_empty_grid()
    {
        var service = Service();

        Assert.Equal(0, service.Count);
        Assert.True(service.HasRoom);
        Assert.False(service.IsPinned("Gold"));
        Assert.Equal(0, service.SlotOf("Gold"));
        Assert.Null(service.ItemAt(1));
    }

    [Fact]
    public async Task Pinning_assigns_slots_in_order()
    {
        var service = Service();

        await FillAsync(service, 3);

        Assert.Equal(1, service.SlotOf("Item 1"));
        Assert.Equal(3, service.SlotOf("Item 3"));
        Assert.Equal("Item 2", service.ItemAt(2));
    }

    [Fact]
    public async Task Pinning_the_same_item_twice_is_refused()
    {
        var service = Service();

        Assert.True(await service.PinAsync("Gold"));
        Assert.False(await service.PinAsync("gold")); // same item, different casing
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public async Task The_eleventh_pin_is_refused()
    {
        var service = Service();
        await FillAsync(service, OverlaySlots.MaxSlots);

        Assert.False(service.HasRoom);
        Assert.False(await service.PinAsync("One too many"));
        Assert.Equal(OverlaySlots.MaxSlots, service.Count);
    }

    [Fact]
    public async Task Unpinning_compacts_the_slots_below()
    {
        var service = Service();
        await FillAsync(service, 4);

        await service.UnpinAsync("Item 2");

        Assert.Equal(2, service.SlotOf("Item 3"));
        Assert.Equal(3, service.SlotOf("Item 4"));
        Assert.Null(service.ItemAt(4));
    }

    [Fact]
    public async Task Unpinning_frees_room_for_a_new_item()
    {
        var service = Service();
        await FillAsync(service, OverlaySlots.MaxSlots);

        await service.UnpinAsync("Item 5");

        Assert.True(service.HasRoom);
        Assert.True(await service.PinAsync("Latecomer"));
        Assert.Equal(OverlaySlots.MaxSlots, service.SlotOf("Latecomer")); // appended last, not into the hole
    }

    [Fact]
    public async Task Clearing_empties_the_grid_and_persists()
    {
        var service = Service();
        await FillAsync(service, 4);

        await service.ClearAsync();

        Assert.Equal(0, service.Count);
        Assert.True(service.HasRoom);

        var reloaded = Service();
        await reloaded.LoadAsync();
        Assert.Empty(reloaded.Pinned);
    }

    [Fact]
    public async Task Clearing_an_empty_grid_raises_nothing()
    {
        var service = Service();
        var raised = 0;
        service.Changed += (_, _) => raised++;

        await service.ClearAsync();

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Pins_persist_across_reloads_in_slot_order()
    {
        var service = Service();
        await FillAsync(service, 3);

        var reloaded = Service();
        await reloaded.LoadAsync();

        Assert.Equal(["Item 1", "Item 2", "Item 3"], reloaded.Pinned);
    }

    [Fact]
    public async Task Changed_fires_only_on_an_actual_change()
    {
        var service = Service();
        var raised = 0;
        service.Changed += (_, _) => raised++;

        _ = await service.PinAsync("Gold");   // change
        _ = await service.PinAsync("Gold");   // no-op (already pinned)
        await service.UnpinAsync("Silver");   // no-op (never pinned)

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_blank_name_is_refused()
    {
        var service = Service();

        Assert.False(await service.PinAsync("   "));
        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task A_hand_edited_file_is_de_duplicated_and_truncated_on_load()
    {
        // The file is meant to be readable and editable, so it can arrive in any shape.
        await File.WriteAllTextAsync(_filePath, """
            { "Pinned": ["Gold", "gold", "", "  ", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K"] }
            """);
        var service = Service();

        await service.LoadAsync();

        Assert.Equal(OverlaySlots.MaxSlots, service.Count);
        Assert.Equal(1, service.SlotOf("Gold"));
        Assert.Equal(2, service.SlotOf("A"));       // the blanks and the duplicate took no slot
        Assert.False(service.IsPinned("K"));        // past the cap
    }

    [Fact]
    public async Task Corrupted_file_falls_back_to_empty()
    {
        await File.WriteAllTextAsync(_filePath, "{ not json");
        var service = Service();

        await service.LoadAsync();

        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task A_corrupted_file_is_replaced_by_the_next_write()
    {
        await File.WriteAllTextAsync(_filePath, "{ not json");
        var service = Service();
        await service.LoadAsync();

        _ = await service.PinAsync("Gold");

        var reloaded = Service();
        await reloaded.LoadAsync();
        Assert.True(reloaded.IsPinned("Gold"));
    }

    [Fact]
    public async Task ItemAt_rejects_slots_outside_the_grid()
    {
        var service = Service();
        await FillAsync(service, 2);

        Assert.Null(service.ItemAt(0));
        Assert.Null(service.ItemAt(OverlaySlots.MaxSlots + 1));
    }
}
