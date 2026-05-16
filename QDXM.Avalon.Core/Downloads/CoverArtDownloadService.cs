using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Core.Downloads;

public sealed record CoverArtDownloadResult(string? Path, string? WarningMessage = null);

public sealed class CoverArtDownloadService
{
    internal const long EmbeddedCoverArtByteLimit = 5L * 1024L * 1024L;
    private readonly HttpClient httpClient;
    private readonly AppSettings settings;
    private readonly string coverCacheDirectory;
    private readonly Dictionary<string, string> embeddedCoverArtCache = new(StringComparer.OrdinalIgnoreCase);

    public CoverArtDownloadService(HttpClient httpClient, AppSettings settings, string? coverCacheDirectory = null)
    {
        this.httpClient = httpClient;
        this.settings = settings;
        this.coverCacheDirectory = string.IsNullOrWhiteSpace(coverCacheDirectory)
            ? AppDataPaths.CoverCacheDirectory
            : coverCacheDirectory;
    }

    public async Task<CoverArtDownloadResult> DownloadAsync(
        Image? image,
        string? coverArtUrl,
        string destination,
        CancellationToken cancellationToken,
        bool saveFolderCover = true,
        string folderCoverFileName = "Cover.jpg")
    {
        if (!settings.SaveCoverArtFile && !settings.Tagging.WriteCoverImageTag)
        {
            return new CoverArtDownloadResult(null);
        }

        var savedCoverArtUrl = CoverArtUrlSelector.GetImageUrlForSize(image, settings.CoverArtSize, coverArtUrl);
        var savedCoverArtPath = saveFolderCover && settings.SaveCoverArtFile && !string.IsNullOrWhiteSpace(savedCoverArtUrl)
            ? Path.Combine(destination, folderCoverFileName)
            : null;

        var savedCoverArtDownloaded = false;
        if (savedCoverArtPath is not null)
        {
            savedCoverArtDownloaded = await DownloadCoverArtFileAsync(savedCoverArtUrl, savedCoverArtPath, cancellationToken);
        }

        if (!settings.Tagging.WriteCoverImageTag)
        {
            return new CoverArtDownloadResult(null);
        }

        var embeddedCoverArt = await DownloadEmbeddedCoverArtAsync(image, coverArtUrl, cancellationToken);
        if (embeddedCoverArt.Path is null)
        {
            return new CoverArtDownloadResult(null, embeddedCoverArt.WarningMessage);
        }

        if (savedCoverArtPath is not null &&
            savedCoverArtDownloaded &&
            File.Exists(savedCoverArtPath) &&
            string.Equals(savedCoverArtUrl, embeddedCoverArt.Url, StringComparison.OrdinalIgnoreCase) &&
            IsWithinEmbeddedCoverArtLimit(savedCoverArtPath))
        {
            return new CoverArtDownloadResult(savedCoverArtPath, embeddedCoverArt.WarningMessage);
        }

        return new CoverArtDownloadResult(embeddedCoverArt.Path, embeddedCoverArt.WarningMessage);
    }

    public void DeleteTemporaryCoverArt(string? coverArtPath)
    {
        if (string.IsNullOrWhiteSpace(coverArtPath) ||
            !coverArtPath.StartsWith(coverCacheDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RemoveCachedTemporaryCoverArtPath(coverArtPath);
        DeleteTemporaryCoverArtFile(coverArtPath);
    }

    public void DeleteTemporaryCoverArtCache()
    {
        var cachedPaths = embeddedCoverArtCache.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        embeddedCoverArtCache.Clear();
        foreach (var cachedPath in cachedPaths)
        {
            DeleteTemporaryCoverArtFile(cachedPath);
        }
    }

    private async Task<EmbeddedCoverArtResult> DownloadEmbeddedCoverArtAsync(
        Image? image,
        string? coverArtUrl,
        CancellationToken cancellationToken)
    {
        var firstTooLargeSize = string.Empty;
        var attemptedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artSize in CoverArtUrlSelector.GetFallbackArtSizes(settings.Tagging.ArtSize))
        {
            var embeddedCoverArtUrl = CoverArtUrlSelector.GetImageUrlForSize(image, artSize, coverArtUrl);
            if (string.IsNullOrWhiteSpace(embeddedCoverArtUrl) ||
                !attemptedUrls.Add(embeddedCoverArtUrl))
            {
                continue;
            }

            if (embeddedCoverArtCache.TryGetValue(embeddedCoverArtUrl, out var cachedPath) &&
                File.Exists(cachedPath))
            {
                if (IsWithinEmbeddedCoverArtLimit(cachedPath))
                {
                    return new EmbeddedCoverArtResult(
                        cachedPath,
                        embeddedCoverArtUrl,
                        GetCoverArtSizeFallbackWarning(firstTooLargeSize, artSize));
                }

                firstTooLargeSize = string.IsNullOrWhiteSpace(firstTooLargeSize) ? artSize : firstTooLargeSize;
                DeleteTemporaryCoverArt(cachedPath);
                continue;
            }

            var embeddedCoverArtPath = Path.Combine(coverCacheDirectory, $"{Guid.NewGuid():N}.jpg");
            if (!await DownloadCoverArtFileAsync(embeddedCoverArtUrl, embeddedCoverArtPath, cancellationToken))
            {
                continue;
            }

            if (!IsWithinEmbeddedCoverArtLimit(embeddedCoverArtPath))
            {
                firstTooLargeSize = string.IsNullOrWhiteSpace(firstTooLargeSize) ? artSize : firstTooLargeSize;
                DeleteTemporaryCoverArtFile(embeddedCoverArtPath);
                continue;
            }

            embeddedCoverArtCache[embeddedCoverArtUrl] = embeddedCoverArtPath;
            return new EmbeddedCoverArtResult(
                embeddedCoverArtPath,
                embeddedCoverArtUrl,
                GetCoverArtSizeFallbackWarning(firstTooLargeSize, artSize));
        }

        return new EmbeddedCoverArtResult(
            null,
            string.Empty,
            string.IsNullOrWhiteSpace(firstTooLargeSize)
                ? null
                : "Embedded cover art exceeded 5 MB at every available size and was skipped.");
    }

    private static string? GetCoverArtSizeFallbackWarning(string firstTooLargeSize, string selectedSize)
    {
        return string.IsNullOrWhiteSpace(firstTooLargeSize)
            ? null
            : $"Embedded cover art exceeded 5 MB and was reduced from {CoverArtUrlSelector.GetArtSizeDisplayName(firstTooLargeSize)} to {CoverArtUrlSelector.GetArtSizeDisplayName(selectedSize)}.";
    }

    private static bool IsWithinEmbeddedCoverArtLimit(string coverArtPath)
    {
        return new FileInfo(coverArtPath).Length <= EmbeddedCoverArtByteLimit;
    }

    private void RemoveCachedTemporaryCoverArtPath(string coverArtPath)
    {
        foreach (var cacheEntry in embeddedCoverArtCache.ToArray())
        {
            if (string.Equals(cacheEntry.Value, coverArtPath, StringComparison.OrdinalIgnoreCase))
            {
                embeddedCoverArtCache.Remove(cacheEntry.Key);
            }
        }
    }

    private static void DeleteTemporaryCoverArtFile(string coverArtPath)
    {
        try
        {
            File.Delete(coverArtPath);
        }
        catch
        {
            // Temporary tag art cleanup should never fail the completed audio job.
        }
    }

    private async Task<bool> DownloadCoverArtFileAsync(
        string coverArtUrl,
        string coverArtPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(coverArtPath)!);
        if (File.Exists(coverArtPath))
        {
            return false;
        }

        try
        {
            var bytes = await httpClient.GetByteArrayAsync(coverArtUrl, cancellationToken);
            await File.WriteAllBytesAsync(coverArtPath, bytes, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeleteTemporaryCoverArtFile(coverArtPath);
            throw;
        }
        catch
        {
            DeleteTemporaryCoverArtFile(coverArtPath);
            return false;
        }
    }

    private sealed record EmbeddedCoverArtResult(string? Path, string Url, string? WarningMessage);
}
