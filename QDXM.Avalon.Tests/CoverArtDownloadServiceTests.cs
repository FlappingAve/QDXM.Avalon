using System.Net;
using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Tests;

public sealed class CoverArtDownloadServiceTests
{
    [Fact]
    public async Task DownloadAsync_ReusesEmbeddedCoverArtForSameResolvedUrl()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings("600 px");
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        var first = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);
        var second = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);

        try
        {
            Assert.Equal(first.Path, second.Path);
            Assert.True(File.Exists(first.Path));
            Assert.Equal(1, handler.GetRequestCount(image.Large));
        }
        finally
        {
            service.DeleteTemporaryCoverArtCache();
        }
    }

    [Fact]
    public async Task DownloadAsync_CachesEmbeddedCoverArtByResolvedSizeUrl()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings("600 px");
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        var large = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);
        settings.Tagging.ArtSize = CoverArtUrlSelector.OriginalDisplayName;
        var original = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);

        try
        {
            Assert.NotEqual(large.Path, original.Path);
            Assert.True(File.Exists(large.Path));
            Assert.True(File.Exists(original.Path));
            Assert.Equal(1, handler.GetRequestCount("https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"));
            Assert.Equal(1, handler.GetRequestCount("https://static.qobuz.com/images/covers/ab/cd/example_org.jpg"));
        }
        finally
        {
            service.DeleteTemporaryCoverArtCache();
        }
    }

    [Fact]
    public async Task DeleteTemporaryCoverArtCache_RemovesAllCachedEmbeddedFiles()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings("600 px");
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        var result = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);

        service.DeleteTemporaryCoverArtCache();

        Assert.False(File.Exists(result.Path));
    }

    [Fact]
    public async Task DownloadAsync_CanSkipFolderCoverWhileKeepingEmbeddedCoverArt()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings("600 px");
        settings.SaveCoverArtFile = true;
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var destination = workspace.CreateDirectory("destination");
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        var embedded = await service.DownloadAsync(
            image,
            null,
            destination,
            CancellationToken.None,
            saveFolderCover: false);

        try
        {
            Assert.NotNull(embedded.Path);
            Assert.True(File.Exists(embedded.Path));
            Assert.False(File.Exists(Path.Combine(destination, "Cover.jpg")));
            Assert.Equal(1, handler.GetRequestCount(image.Large));
        }
        finally
        {
            service.DeleteTemporaryCoverArtCache();
        }
    }

    [Fact]
    public async Task DownloadAsync_DoesNotReuseExistingFolderCoverForEmbeddedArt()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings("600 px");
        settings.SaveCoverArtFile = true;
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var destination = workspace.CreateDirectory("destination");
        var folderCoverPath = Path.Combine(destination, "Cover.jpg");
        var staleBytes = new byte[] { 0xFF, 0xEE, 0xDD };
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        await File.WriteAllBytesAsync(folderCoverPath, staleBytes);

        var result = await service.DownloadAsync(image, null, destination, CancellationToken.None);

        try
        {
            Assert.NotNull(result.Path);
            Assert.NotEqual(folderCoverPath, result.Path);
            Assert.Equal(staleBytes, await File.ReadAllBytesAsync(folderCoverPath));
            Assert.Equal(1, handler.GetRequestCount(image.Large));
            Assert.True(File.Exists(result.Path));
        }
        finally
        {
            service.DeleteTemporaryCoverArtCache();
        }
    }

    [Fact]
    public async Task DownloadAsync_LeavesExistingFolderCoverWhenEmbeddedArtIsDisabled()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = new AppSettings
        {
            SaveCoverArtFile = true,
            Tagging =
            {
                WriteCoverImageTag = false
            }
        };
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var destination = workspace.CreateDirectory("destination");
        var folderCoverPath = Path.Combine(destination, "Cover.jpg");
        var existingBytes = new byte[] { 0x10, 0x20, 0x30 };
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        await File.WriteAllBytesAsync(folderCoverPath, existingBytes);

        var result = await service.DownloadAsync(image, null, destination, CancellationToken.None);

        Assert.Null(result.Path);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(folderCoverPath));
        Assert.Equal(0, handler.GetRequestCount(image.Large));
    }

    [Fact]
    public async Task DownloadAsync_FallsBackWhenEmbeddedCoverArtExceedsLimit()
    {
        var oversizedBytes = (int)CoverArtDownloadService.EmbeddedCoverArtByteLimit + 1;
        var handler = new CountingImageHandler(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://static.qobuz.com/images/covers/ab/cd/example_org.jpg"] = oversizedBytes,
            ["https://static.qobuz.com/images/covers/ab/cd/example_max.jpg"] = oversizedBytes
        });
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings(CoverArtUrlSelector.OriginalDisplayName);
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        var result = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);

        try
        {
            Assert.NotNull(result.Path);
            Assert.True(File.Exists(result.Path));
            Assert.Contains("reduced", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, handler.GetRequestCount("https://static.qobuz.com/images/covers/ab/cd/example_org.jpg"));
            Assert.Equal(1, handler.GetRequestCount("https://static.qobuz.com/images/covers/ab/cd/example_max.jpg"));
            Assert.Equal(1, handler.GetRequestCount("https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"));
        }
        finally
        {
            service.DeleteTemporaryCoverArtCache();
        }
    }

    [Fact]
    public async Task DownloadAsync_SkipsEmbeddedCoverArtWhenEverySizeExceedsLimit()
    {
        var oversizedBytes = (int)CoverArtDownloadService.EmbeddedCoverArtByteLimit + 1;
        var responseSizes = CoverArtUrlSelector.GetFallbackArtSizes(CoverArtUrlSelector.OriginalDisplayName)
            .ToDictionary(
                size => $"https://static.qobuz.com/images/covers/ab/cd/example_{size}.jpg",
                _ => oversizedBytes,
                StringComparer.OrdinalIgnoreCase);
        var handler = new CountingImageHandler(responseSizes);
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings(CoverArtUrlSelector.OriginalDisplayName);
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        var result = await service.DownloadAsync(image, null, workspace.DirectoryPath, CancellationToken.None);

        Assert.Null(result.Path);
        Assert.Contains("skipped", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadAsync_PropagatesCoverArtDownloadCancellation()
    {
        var handler = new CountingImageHandler();
        using var httpClient = new HttpClient(handler);
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateEmbeddedOnlySettings("600 px");
        var service = CreateCoverArtDownloadService(httpClient, settings, workspace);
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadAsync(image, null, workspace.DirectoryPath, cancellationTokenSource.Token));
    }

    private static AppSettings CreateEmbeddedOnlySettings(string embeddedArtSize)
    {
        return new AppSettings
        {
            SaveCoverArtFile = false,
            Tagging =
            {
                WriteCoverImageTag = true,
                ArtSize = embeddedArtSize
            }
        };
    }

    private static CoverArtDownloadService CreateCoverArtDownloadService(
        HttpClient httpClient,
        AppSettings settings,
        TestWorkspace workspace)
    {
        return new CoverArtDownloadService(
            httpClient,
            settings,
            workspace.CreateDirectory("cover-cache"));
    }

    private sealed class CountingImageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, int> responseSizes;
        private readonly Dictionary<string, int> requestCounts = new(StringComparer.OrdinalIgnoreCase);

        public CountingImageHandler(IReadOnlyDictionary<string, int>? responseSizes = null)
        {
            this.responseSizes = responseSizes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

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

            var responseSize = responseSizes.GetValueOrDefault(url, 3);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Enumerable.Repeat((byte)0x01, responseSize).ToArray())
            };

            return Task.FromResult(response);
        }
    }
}
