using System.IO;
using System.Text.Json;
using WikeloContractor.Models;
using StoreModel = WikeloContractor.Models.InventoryStore;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IInventoryStore" />
public sealed class InventoryStore : IInventoryStore, IDisposable
{
    /// <summary>How long the store waits for the edits to stop before writing.</summary>
    private static readonly TimeSpan _defaultQuietPeriod = TimeSpan.FromMilliseconds(400);

    /// <summary>Longest a change may sit unwritten while edits keep arriving.</summary>
    private static readonly TimeSpan _defaultMaxDelay = TimeSpan.FromSeconds(2);

    private readonly string _filePath;
    private readonly TimeSpan _quietPeriod;
    private readonly TimeSpan _maxDelay;

    /// <summary>Guards the in-memory dictionary and the debounce bookkeeping.</summary>
    private readonly Lock _gate = new();

    /// <summary>Serializes the file writes, so two flushes never open the same temp file at once.</summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private StoreModel _store = new();

    private CancellationTokenSource? _debounce;

    private bool _dirty;

    /// <summary><see cref="Environment.TickCount64"/> when the current unwritten streak began.</summary>
    private long _dirtySinceTicks;

    private bool _disposed;

    public InventoryStore()
        : this(Path.Combine(AppStorage.Root, "inventory.json"))
    {
    }

    /// <summary>Test seam: lets unit tests point the store at a temp file.</summary>
    internal InventoryStore(string filePath)
        : this(filePath, _defaultQuietPeriod, _defaultMaxDelay)
    {
    }

    /// <summary>Test seam: shrinks the debounce so a test does not have to wait out the real one.</summary>
    internal InventoryStore(string filePath, TimeSpan quietPeriod, TimeSpan maxDelay)
    {
        _filePath = filePath;
        _quietPeriod = quietPeriod;
        _maxDelay = maxDelay;
    }

    public event EventHandler? Changed;

    public int GetCount(string name)
    {
        lock (_gate)
        {
            return _store.Counts.TryGetValue(name, out var count) ? count : 0;
        }
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        StoreModel loaded;

        try
        {
            await using var stream = File.OpenRead(_filePath);
            loaded = await JsonSerializer.DeserializeAsync<StoreModel>(stream, AppStorage.JsonOptions) ?? new StoreModel();
        }
        catch (JsonException)
        {
            // Corrupted file — start empty; it is rewritten on the next change.
            loaded = new StoreModel();
        }

        // Deserialization drops the case-insensitive comparer; restore it so lookups stay tolerant.
        loaded.Counts = new Dictionary<string, int>(loaded.Counts, StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            _store = loaded;
        }
    }

    public Task SetCountAsync(string name, int count)
    {
        var changed = false;

        lock (_gate)
        {
            changed = ApplyCount(name, count);
        }

        return Commit(changed);
    }

    public Task SetCountsAsync(IReadOnlyDictionary<string, int> counts)
    {
        var changed = false;

        lock (_gate)
        {
            foreach (var (name, count) in counts)
            {
                changed |= ApplyCount(name, count);
            }
        }

        return Commit(changed);
    }

    public void Flush() =>
        // Every await below is ConfigureAwait(false), so blocking here cannot deadlock against the
        // dispatcher — which matters because the one caller that must be synchronous is shutdown.
        FlushAsync().GetAwaiter().GetResult();

    public async Task FlushAsync()
    {
        lock (_gate)
        {
            // A pending debounce would otherwise write the same state again a moment later.
            _debounce?.Cancel();
        }

        await WriteAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        // Losing the last few seconds of counter edits on exit is exactly the failure the debounce
        // must not introduce, so disposal persists rather than just cancelling.
        Flush();

        _debounce?.Dispose();
        _writeLock.Dispose();
    }

    /// <summary>
    /// Publishes an applied change: schedules the write, then raises <see cref="Changed"/>
    /// synchronously so the UI never waits on disk.
    /// </summary>
    private Task Commit(bool changed)
    {
        if (!changed)
        {
            // Nothing actually changed — skip the write and the event.
            return Task.CompletedTask;
        }

        ScheduleWrite();
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies one clamped count to the in-memory store; returns whether it changed anything.
    /// Caller holds <see cref="_gate"/>.
    /// </summary>
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

    /// <summary>
    /// (Re)arms the trailing debounce. Held hotkey auto-repeat produces ~30 edits a second; writing
    /// each one would mean overlapping <c>File.Create</c> calls on a single temp path. The write is
    /// deferred until the edits stop, but never by more than <see cref="_maxDelay"/> — a player
    /// holding a key for a minute must not risk a minute of unsaved counts.
    /// </summary>
    private void ScheduleWrite()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = Environment.TickCount64;

            if (!_dirty)
            {
                _dirty = true;
                _dirtySinceTicks = now;
            }

            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = new CancellationTokenSource();

            var untilHardFlush = _maxDelay - TimeSpan.FromMilliseconds(now - _dirtySinceTicks);
            var delay = untilHardFlush < _quietPeriod ? untilHardFlush : _quietPeriod;

            _ = WriteAfterAsync(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, _debounce.Token);
        }
    }

    private async Task WriteAfterAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer edit (or by a flush) — that scheduling owns the write now.
            return;
        }

        await WriteAsync().ConfigureAwait(false);
    }

    private async Task WriteAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);

        try
        {
            string json;

            lock (_gate)
            {
                if (!_dirty)
                {
                    return;
                }

                _dirty = false;
                _dirtySinceTicks = Environment.TickCount64;

                // Snapshot under the lock: the dictionary can be mutated by the next hotkey press
                // while this write is still on disk.
                json = JsonSerializer.Serialize(_store, AppStorage.JsonOptions);
            }

            // Atomic write: serialize to a temp file, then swap it in.
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception exception)
        {
            // Nobody awaits the debounced write, so without this an IO failure is invisible.
            // Staying dirty means the next edit — or the shutdown flush — retries.
            lock (_gate)
            {
                _dirty = true;
            }

            AppLog.Write("Error", $"Failed to save inventory to {_filePath}", exception);
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }
}
