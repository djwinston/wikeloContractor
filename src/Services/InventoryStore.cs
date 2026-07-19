using System.IO;
using System.Text.Json;
using WikeloContractor.Models;
using StoreModel = WikeloContractor.Models.InventoryStore;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IInventoryStore" />
public sealed class InventoryStore : IInventoryStore
{
    private readonly string _filePath;

    private StoreModel _store = new();

    public InventoryStore()
        : this(Path.Combine(AppStorage.Root, "inventory.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the store at a temp file.</summary>
    internal InventoryStore(string filePath) => _filePath = filePath;

    public event EventHandler? Changed;

    public int GetCount(string name) => _store.Counts.TryGetValue(name, out var count) ? count : 0;

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _store = await JsonSerializer.DeserializeAsync<StoreModel>(stream, AppStorage.JsonOptions) ?? new StoreModel();
        }
        catch (JsonException)
        {
            // Corrupted file — start empty; it is rewritten on the next change.
            _store = new StoreModel();
        }

        // Deserialization drops the case-insensitive comparer; restore it so lookups stay tolerant.
        _store.Counts = new Dictionary<string, int>(_store.Counts, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetCountAsync(string name, int count)
    {
        if (!ApplyCount(name, count))
        {
            // Nothing actually changed — skip the write and event.
            return;
        }

        await SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetCountsAsync(IReadOnlyDictionary<string, int> counts)
    {
        var changed = false;
        foreach (var (name, count) in counts)
        {
            changed |= ApplyCount(name, count);
        }

        if (!changed)
        {
            return;
        }

        await SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies one clamped count to the in-memory store; returns whether it changed anything.</summary>
    private bool ApplyCount(string name, int count)
    {
        count = Math.Max(0, count);

        if (count == 0)
        {
            // Removing an absent key changes nothing.
            return _store.Counts.Remove(name);
        }

        if (_store.Counts.TryGetValue(name, out var current) && current == count)
        {
            return false;
        }

        _store.Counts[name] = count;
        return true;
    }

    private async Task SaveAsync()
    {
        // Atomic write: serialize to a temp file, then swap it in.
        var tempPath = _filePath + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, _store, AppStorage.JsonOptions);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
