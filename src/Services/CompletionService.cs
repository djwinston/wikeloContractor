using System.IO;
using System.Text.Json;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <inheritdoc cref="ICompletionService" />
public sealed class CompletionService : ICompletionService
{
    private readonly string _filePath = Path.Combine(AppStorage.Root, "completed.json");

    private CompletionStore _store = new();

    public event EventHandler? Changed;

    public int TotalReputation => _store.Completed.Values.Sum();

    public bool IsCompleted(string uuid) => _store.Completed.ContainsKey(uuid);

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _store = await JsonSerializer.DeserializeAsync<CompletionStore>(stream, AppStorage.JsonOptions) ?? new CompletionStore();
        }
        catch (JsonException)
        {
            // Corrupted file — start empty; it is rewritten on the next change.
            _store = new CompletionStore();
        }
    }

    public async Task SetCompletedAsync(WikeloContract contract, bool completed)
    {
        if (completed)
        {
            _store.Completed[contract.Uuid] = contract.ReputationAmount;
        }
        else if (!_store.Completed.Remove(contract.Uuid))
        {
            // Already absent — nothing to persist or announce.
            return;
        }

        await SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
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
