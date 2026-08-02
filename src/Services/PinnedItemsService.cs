using System.IO;
using System.Text.Json;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IPinnedItemsService" />
public sealed class PinnedItemsService : IPinnedItemsService
{
    /// <summary>Item names are matched the same way <see cref="InventoryStore"/> matches them.</summary>
    private static readonly StringComparer _comparer = StringComparer.OrdinalIgnoreCase;

    private readonly string _filePath;

    private PinnedItemsStore _store = new();

    public PinnedItemsService()
        : this(Path.Combine(AppStorage.Root, "pinned.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the store at a temp file.</summary>
    internal PinnedItemsService(string filePath) => _filePath = filePath;

    public event EventHandler? Changed;

    public IReadOnlyList<string> Pinned => _store.Pinned;

    public int Count => _store.Pinned.Count;

    public bool HasRoom => _store.Pinned.Count < OverlaySlots.MaxSlots;

    public bool IsPinned(string name) => IndexOf(name) >= 0;

    public int SlotOf(string name) => IndexOf(name) + 1;

    public string? ItemAt(int slot) =>
        OverlaySlots.IsValidSlot(slot) && slot <= _store.Pinned.Count ? _store.Pinned[slot - 1] : null;

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _store = await JsonSerializer.DeserializeAsync<PinnedItemsStore>(stream, AppStorage.JsonOptions) ?? new PinnedItemsStore();
        }
        catch (JsonException)
        {
            // Corrupted file — start empty; it is rewritten on the next change.
            _store = new PinnedItemsStore();
        }

        Normalize();
    }

    public async Task<bool> PinAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !HasRoom || IsPinned(name))
        {
            return false;
        }

        _store.Pinned.Add(name);
        await SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task UnpinAsync(string name)
    {
        var index = IndexOf(name);
        if (index < 0)
        {
            return;
        }

        // Removing from the list is the compaction: everything below moves up one slot.
        _store.Pinned.RemoveAt(index);
        await SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync()
    {
        if (_store.Pinned.Count == 0)
        {
            return;
        }

        _store.Pinned.Clear();
        await SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private int IndexOf(string name) => _store.Pinned.FindIndex(pinned => _comparer.Equals(pinned, name));

    /// <summary>
    /// The file is hand-editable, so a load can bring in blanks, duplicates or more entries than there
    /// are slots. Fixing it in memory keeps every consumer's invariant true; the cleaned list reaches
    /// disk on the next change rather than through a surprise write at startup.
    /// </summary>
    private void Normalize()
    {
        var seen = new HashSet<string>(_comparer);
        var cleaned = new List<string>(OverlaySlots.MaxSlots);

        foreach (var name in _store.Pinned)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
            {
                continue;
            }

            cleaned.Add(name);

            if (cleaned.Count == OverlaySlots.MaxSlots)
            {
                break;
            }
        }

        _store.Pinned = cleaned;
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
