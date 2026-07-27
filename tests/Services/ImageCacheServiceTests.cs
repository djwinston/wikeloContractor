using System.IO;
using System.Net;
using System.Net.Http;
using WikeloContractor.Services;
using Xunit;

namespace WikeloContractor.Tests.Services;

public sealed class ImageCacheServiceTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(
        Path.GetTempPath(), "WikeloContractorTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory is harmless.
        }
    }

    private static HttpResponseMessage Png(byte[] bytes) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes),
    };

    private ImageCacheService CreateService(StubHandler handler) =>
        new(new HttpClient(handler), _cacheDirectory);

    [Fact]
    public async Task Image_is_downloaded_once_and_served_from_disk_afterwards()
    {
        var handler = new StubHandler(_ => Png([1, 2, 3]));
        var service = CreateService(handler);

        var first = await service.GetLocalPathAsync("https://cdn.example/img/item.png");
        var second = await service.GetLocalPathAsync("https://cdn.example/img/item.png");

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.Requests);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(first));
        Assert.EndsWith(".png", first);
    }

    [Fact]
    public async Task Different_urls_map_to_different_cache_files()
    {
        var handler = new StubHandler(_ => Png([1]));
        var service = CreateService(handler);

        var a = await service.GetLocalPathAsync("https://cdn.example/a.webp");
        var b = await service.GetLocalPathAsync("https://cdn.example/b.webp");

        Assert.NotEqual(a, b);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task Failed_download_returns_null_and_is_retried_on_the_next_call()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        Assert.Null(await service.GetLocalPathAsync("https://cdn.example/missing.png"));
        Assert.Null(await service.GetLocalPathAsync("https://cdn.example/missing.png"));

        // Failures are not cached — each call retried the download.
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task Local_file_path_is_passed_through_without_any_download()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("No HTTP expected"));
        var service = CreateService(handler);

        _ = Directory.CreateDirectory(_cacheDirectory);
        var localFile = Path.Combine(_cacheDirectory, "custom.png");
        await File.WriteAllBytesAsync(localFile, [42]);

        Assert.Equal(localFile, await service.GetLocalPathAsync(localFile));
        Assert.Null(await service.GetLocalPathAsync(Path.Combine(_cacheDirectory, "absent.png")));
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task Relative_override_path_resolves_against_the_install_directory()
    {
        // How every bundled image ships: the shared override file stores "Resources/img/…" with
        // forward slashes so it stays machine-independent, and the app resolves it next to the exe.
        var handler = new StubHandler(_ => throw new InvalidOperationException("No HTTP expected"));
        var service = CreateService(handler);

        // The directory has to sit under the install dir — that is the resolution being tested.
        var name = "img-test-" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(AppContext.BaseDirectory, name);
        _ = Directory.CreateDirectory(root);
        try
        {
            var relative = $"{name}/bundled.webp";
            var expected = Path.Combine(AppContext.BaseDirectory, relative);
            await File.WriteAllBytesAsync(expected, [42]);

            // Compared as an exact rooted string, so the relative value being handed straight back
            // fails here: resolving it is not the caller's job, and a caller resolving it against
            // the *working* directory would break outside a dev run.
            Assert.Equal(expected, await service.GetLocalPathAsync(relative));
            Assert.Null(await service.GetLocalPathAsync($"{name}/absent.webp"));
            Assert.Equal(0, handler.Requests);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Url_without_a_usable_extension_falls_back_to_img()
    {
        var handler = new StubHandler(_ => Png([7]));
        var service = CreateService(handler);

        var path = await service.GetLocalPathAsync("https://cdn.example/render?id=123");

        Assert.NotNull(path);
        Assert.EndsWith(".img", path);
    }
}
