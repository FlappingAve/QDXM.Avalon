using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Services;

public sealed class DownloadQueueCoverArtCache : IDisposable
{
    public static DownloadQueueCoverArtCache Shared { get; } = new();

    private readonly string cacheDirectory;
    private readonly HttpClient httpClient;
    private readonly bool disposeHttpClient;

    public DownloadQueueCoverArtCache(
        string? cacheDirectory = null,
        HttpClient? httpClient = null)
    {
        this.cacheDirectory = cacheDirectory ?? AppDataPaths.QueueCoverCacheDirectory;
        this.httpClient = httpClient ?? new HttpClient();
        disposeHttpClient = httpClient is null;
    }

    public async Task<Bitmap?> LoadAsync(
        string queueItemId,
        string? imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!await EnsureCachedAsync(queueItemId, imageUrl, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var cachePath = GetImagePath(queueItemId);
        var bitmap = CreateBitmap(cachePath);
        if (bitmap is not null)
        {
            return bitmap;
        }

        return await EnsureCachedAsync(queueItemId, imageUrl, cancellationToken).ConfigureAwait(false)
            ? CreateBitmap(cachePath)
            : null;
    }

    public async Task<bool> EnsureCachedAsync(
        string queueItemId,
        string? imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetImageUri(imageUrl, out var uri))
        {
            return false;
        }

        var cachePath = GetImagePath(queueItemId);
        if (!IsCachedForUrl(cachePath, imageUrl!))
        {
            Delete(queueItemId);
            if (!await DownloadAsync(uri!, cachePath, imageUrl!, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    public void Delete(string queueItemId)
    {
        if (!TryGetExistingCacheDirectory(out var directory))
        {
            return;
        }

        var cacheKey = GetCacheKey(queueItemId);
        DeleteFileIfExists(Path.Combine(directory, $"{cacheKey}.img"));
        DeleteFileIfExists(Path.Combine(directory, $"{cacheKey}.url"));

        foreach (var partialPath in EnumerateFiles(directory, $"{cacheKey}.*.tmp"))
        {
            DeleteFileIfExists(partialPath);
        }
    }

    public void PruneExcept(IEnumerable<string> queueItemIds)
    {
        if (!TryGetExistingCacheDirectory(out var directory))
        {
            return;
        }

        var allowedKeys = queueItemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(GetCacheKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in EnumerateFiles(directory, "*"))
        {
            var fileName = Path.GetFileName(filePath);
            if (!TryGetRecognizedCacheFileKey(fileName, out var cacheKey))
            {
                continue;
            }

            if (!allowedKeys.Contains(cacheKey))
            {
                DeleteFileIfExists(filePath);
            }
        }
    }

    public void Dispose()
    {
        if (disposeHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private async Task<bool> DownloadAsync(
        Uri uri,
        string cachePath,
        string imageUrl,
        CancellationToken cancellationToken)
    {
        var partialPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await using (var input = await httpClient.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(partialPath, cachePath, overwrite: true);
            await File.WriteAllTextAsync(GetUrlPathFromImagePath(cachePath), imageUrl, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            DeleteFileIfExists(partialPath);
            return false;
        }
    }

    private bool IsCachedForUrl(string cachePath, string imageUrl)
    {
        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            var cachedUrl = File.ReadAllText(GetUrlPathFromImagePath(cachePath));
            return string.Equals(cachedUrl, imageUrl, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private string GetImagePath(string queueItemId)
    {
        return Path.Combine(GetCacheDirectory(), $"{GetCacheKey(queueItemId)}.img");
    }

    private string GetCacheDirectory()
    {
        Directory.CreateDirectory(cacheDirectory);
        return cacheDirectory;
    }

    private bool TryGetExistingCacheDirectory(out string directory)
    {
        directory = cacheDirectory;
        return Directory.Exists(directory);
    }

    private static string GetUrlPathFromImagePath(string cachePath)
    {
        return Path.ChangeExtension(cachePath, ".url");
    }

    private static string GetCacheKey(string queueItemId)
    {
        var id = string.IsNullOrWhiteSpace(queueItemId)
            ? Guid.Empty.ToString("N")
            : queueItemId;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
    }

    private static bool TryGetRecognizedCacheFileKey(string fileName, out string cacheKey)
    {
        cacheKey = string.Empty;
        var parts = fileName.Split('.');
        if (parts.Length is not (2 or 3))
        {
            return false;
        }

        if (parts.Length == 2 &&
            parts[1] is not ("img" or "url"))
        {
            return false;
        }

        if (parts.Length == 3 && parts[2] != "tmp")
        {
            return false;
        }

        var candidateKey = parts[0];
        if (candidateKey.Length != 64 ||
            !candidateKey.All(Uri.IsHexDigit))
        {
            return false;
        }

        cacheKey = candidateKey;
        return true;
    }

    private static bool TryGetImageUri(string? imageUrl, out Uri? uri)
    {
        uri = null;
        return !string.IsNullOrWhiteSpace(imageUrl) &&
            Uri.TryCreate(imageUrl, UriKind.Absolute, out uri) &&
            uri.Scheme is "http" or "https";
    }

    private static Bitmap? CreateBitmap(string cachePath)
    {
        try
        {
            using var stream = File.OpenRead(cachePath);
            return new Bitmap(stream);
        }
        catch
        {
            DeleteFileIfExists(cachePath);
            DeleteFileIfExists(GetUrlPathFromImagePath(cachePath));
            return null;
        }
    }

    private static void DeleteFileIfExists(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
        }
    }

    private static IReadOnlyList<string> EnumerateFiles(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern).ToArray();
        }
        catch
        {
            return [];
        }
    }
}
