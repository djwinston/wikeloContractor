using System.IO;
using System.Text.Json;
using WikeloContractor.Models;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IFavoritesService" />
public sealed class FavoritesService : IFavoritesService
{
    private readonly string _filePath;

    private FavoritesStore _store = new();

    public FavoritesService() => _filePath = Path.Combine(AppStorage.Root, "favorites.json");

    /// <summary>Test seam: points the store at a temp file, mirroring <see cref="CompletionService"/>.</summary>
    internal FavoritesService(string filePath) => _filePath = filePath;

    public event EventHandler? Changed;

    public int Count => _store.Favorites.Count;

    public bool IsFavorite(string uuid) => _store.Favorites.Contains(uuid);

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _store = await JsonSerializer.DeserializeAsync<FavoritesStore>(stream, AppStorage.JsonOptions) ?? new FavoritesStore();
        }
        catch (JsonException)
        {
            // Corrupted file — start empty; it is rewritten on the next change.
            _store = new FavoritesStore();
        }
    }

    public async Task SetFavoriteAsync(string uuid, bool favorite)
    {
        // Add/Remove both report whether they actually changed the set, so an unchanged flag
        // costs neither a disk write nor an event.
        var changed = favorite ? _store.Favorites.Add(uuid) : _store.Favorites.Remove(uuid);
        if (!changed)
        {
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
