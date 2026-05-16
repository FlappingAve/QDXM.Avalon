using System.Net;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.Tests;

public sealed class DownloadQueueCoverArtCacheTests
{
    [Fact]
    public async Task EnsureCachedAsync_ReusesCachedCoverForSameQueueItemAndUrl()
    {
        using var workspace = TestPaths.CreateWorkspace();
        using var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new DownloadQueueCoverArtCache(workspace.CreateDirectory("queue-covers"), httpClient);
        const string imageUrl = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg";

        Assert.True(await cache.EnsureCachedAsync("queue-item-1", imageUrl));
        Assert.True(await cache.EnsureCachedAsync("queue-item-1", imageUrl));

        Assert.Equal(1, handler.GetRequestCount(imageUrl));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(workspace.DirectoryPath, "queue-covers")).Length);
    }

    [Fact]
    public async Task EnsureCachedAsync_RedownloadsWhenCoverUrlChangesForSameQueueItem()
    {
        using var workspace = TestPaths.CreateWorkspace();
        using var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new DownloadQueueCoverArtCache(workspace.CreateDirectory("queue-covers"), httpClient);
        const string firstUrl = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg";
        const string secondUrl = "https://static.qobuz.com/images/covers/ef/gh/other_600.jpg";

        Assert.True(await cache.EnsureCachedAsync("queue-item-1", firstUrl));
        Assert.True(await cache.EnsureCachedAsync("queue-item-1", secondUrl));
        Assert.True(await cache.EnsureCachedAsync("queue-item-1", secondUrl));

        Assert.Equal(1, handler.GetRequestCount(firstUrl));
        Assert.Equal(1, handler.GetRequestCount(secondUrl));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(workspace.DirectoryPath, "queue-covers")).Length);
    }

    [Fact]
    public async Task Delete_RemovesCachedCoverForQueueItem()
    {
        using var workspace = TestPaths.CreateWorkspace();
        using var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        var cacheDirectory = workspace.CreateDirectory("queue-covers");
        using var cache = new DownloadQueueCoverArtCache(cacheDirectory, httpClient);
        const string imageUrl = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg";

        Assert.True(await cache.EnsureCachedAsync("queue-item-1", imageUrl));

        cache.Delete("queue-item-1");

        Assert.Empty(Directory.GetFiles(cacheDirectory));
    }

    [Fact]
    public void Delete_DoesNotCreateQueueCoverDirectory()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var cacheDirectory = workspace.FilePath("queue-covers");
        using var cache = new DownloadQueueCoverArtCache(cacheDirectory);

        cache.Delete("queue-item-1");

        Assert.False(Directory.Exists(cacheDirectory));
    }

    [Fact]
    public async Task PruneExcept_RemovesOnlyRecognizedOrphanedQueueCoverFiles()
    {
        using var workspace = TestPaths.CreateWorkspace();
        using var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        var cacheDirectory = workspace.CreateDirectory("queue-covers");
        using var cache = new DownloadQueueCoverArtCache(cacheDirectory, httpClient);
        const string keptUrl = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg";
        const string orphanedUrl = "https://static.qobuz.com/images/covers/ef/gh/other_600.jpg";

        Assert.True(await cache.EnsureCachedAsync("kept-item", keptUrl));
        Assert.True(await cache.EnsureCachedAsync("orphaned-item", orphanedUrl));
        var unknownFilePath = Path.Combine(cacheDirectory, "notes.txt");
        await File.WriteAllTextAsync(unknownFilePath, "not owned by the cache");

        cache.PruneExcept(["kept-item"]);

        Assert.Equal(3, Directory.GetFiles(cacheDirectory).Length);
        Assert.True(File.Exists(unknownFilePath));
        Assert.Contains(Directory.GetFiles(cacheDirectory), path => File.ReadAllText(path) == keptUrl);
        Assert.DoesNotContain(Directory.GetFiles(cacheDirectory), path =>
            path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) &&
            File.ReadAllText(path) == orphanedUrl);
    }

    [Fact]
    public void PruneExcept_DoesNotCreateQueueCoverDirectory()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var cacheDirectory = workspace.FilePath("queue-covers");
        using var cache = new DownloadQueueCoverArtCache(cacheDirectory);

        cache.PruneExcept(["queue-item-1"]);

        Assert.False(Directory.Exists(cacheDirectory));
    }

    private sealed class CountingImageHandler : HttpMessageHandler
    {
        private static readonly byte[] ImageBytes = [0x01, 0x02, 0x03];
        private readonly Dictionary<string, int> requestCounts = new(StringComparer.OrdinalIgnoreCase);

        public int GetRequestCount(string url)
        {
            return requestCounts.GetValueOrDefault(url);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            requestCounts[url] = GetRequestCount(url) + 1;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ImageBytes)
            });
        }
    }
}
