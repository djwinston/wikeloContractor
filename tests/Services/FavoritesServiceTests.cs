using System.IO;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class FavoritesServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    public FavoritesServiceTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "favorites.json");
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
    public void Missing_file_yields_no_favorites()
    {
        var service = new FavoritesService(_filePath);

        Assert.False(service.IsFavorite("abc"));
        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task Favorites_persist_across_reloads()
    {
        var service = new FavoritesService(_filePath);
        await service.SetFavoriteAsync("uuid-1", true);
        await service.SetFavoriteAsync("uuid-2", true);

        var reloaded = new FavoritesService(_filePath);
        await reloaded.LoadAsync();

        Assert.True(reloaded.IsFavorite("uuid-1"));
        Assert.True(reloaded.IsFavorite("uuid-2"));
        Assert.Equal(2, reloaded.Count);
    }

    [Fact]
    public async Task Unflagging_removes_the_contract_and_persists()
    {
        var service = new FavoritesService(_filePath);
        await service.SetFavoriteAsync("uuid-1", true);
        await service.SetFavoriteAsync("uuid-1", false);

        var reloaded = new FavoritesService(_filePath);
        await reloaded.LoadAsync();

        Assert.False(reloaded.IsFavorite("uuid-1"));
        Assert.Equal(0, reloaded.Count);
    }

    [Fact]
    public async Task Changed_fires_only_on_an_actual_change()
    {
        var service = new FavoritesService(_filePath);
        var raised = 0;
        service.Changed += (_, _) => raised++;

        await service.SetFavoriteAsync("uuid-1", true);  // change
        await service.SetFavoriteAsync("uuid-1", true);  // no-op (already flagged)
        await service.SetFavoriteAsync("uuid-2", false); // no-op (never flagged)

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Corrupted_file_falls_back_to_empty()
    {
        await File.WriteAllTextAsync(_filePath, "{ not json");
        var service = new FavoritesService(_filePath);

        await service.LoadAsync();

        Assert.False(service.IsFavorite("uuid-1"));
        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task A_corrupted_file_is_replaced_by_the_next_write()
    {
        await File.WriteAllTextAsync(_filePath, "{ not json");
        var service = new FavoritesService(_filePath);
        await service.LoadAsync();

        await service.SetFavoriteAsync("uuid-1", true);

        var reloaded = new FavoritesService(_filePath);
        await reloaded.LoadAsync();
        Assert.True(reloaded.IsFavorite("uuid-1"));
    }
}
