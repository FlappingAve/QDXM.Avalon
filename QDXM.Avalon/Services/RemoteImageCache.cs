using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Services;

public sealed class RemoteImageCache
{
    public static RemoteImageCache Shared { get; } = new();

    private readonly bool retainDecodedImages;
    private readonly string? diskCacheDirectory;
    private readonly object clearLock = new();
    private readonly ConcurrentDictionary<string, Task<byte[]?>> imageBytes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<string?>> diskCacheFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> images = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient httpClient = new();
    private CancellationTokenSource clearCancellation = new();

    public RemoteImageCache(
        bool retainDecodedImages = true,
        bool cacheCompressedImagesOnDisk = false)
    {
        this.retainDecodedImages = retainDecodedImages;
        if (cacheCompressedImagesOnDisk)
        {
            diskCacheDirectory = Path.Combine(AppDataPaths.SearchImageCacheDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(diskCacheDirectory);
        }
    }

    public bool RetainsDecodedImages => retainDecodedImages;

    public static void ClearPersistentDiskCache()
    {
        DirectoryContentsCleaner.Clear(AppDataPaths.SearchImageCacheDirectory);
    }

    public Task<Bitmap?> LoadAsync(string? imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Task.FromResult<Bitmap?>(null);
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return Task.FromResult<Bitmap?>(null);
        }

        return LoadAsync(imageUrl, uri, cancellationToken);
    }

    public void Clear()
    {
        CancellationTokenSource previousCancellation;
        lock (clearLock)
        {
            previousCancellation = clearCancellation;
            clearCancellation = new CancellationTokenSource();
        }

        previousCancellation.Cancel();
        previousCancellation.Dispose();

        var cachedImages = images.Values.ToList();
        images.Clear();
        imageBytes.Clear();
        diskCacheFiles.Clear();
        ClearDiskCache();

        foreach (var imageTask in cachedImages)
        {
            DisposeWhenReady(imageTask);
        }
    }

    private async Task<Bitmap?> LoadAsync(
        string imageUrl,
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CreateLoadCancellation(cancellationToken);
        var loadToken = linkedCancellation.Token;
        return retainDecodedImages
            ? await images.GetOrAdd(imageUrl, _ => DownloadBitmapAsync(uri, loadToken))
            : await LoadUnretainedBitmapAsync(imageUrl, uri, loadToken);
    }

    private CancellationTokenSource CreateLoadCancellation(CancellationToken cancellationToken)
    {
        lock (clearLock)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                clearCancellation.Token);
        }
    }

    private async Task<Bitmap?> DownloadBitmapAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await DownloadBytesAsync(uri, cancellationToken);
            return bytes is null ? null : CreateBitmap(bytes);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Bitmap?> LoadUnretainedBitmapAsync(
        string imageUrl,
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (diskCacheDirectory is null)
        {
            var bytes = await imageBytes.GetOrAdd(imageUrl, _ => DownloadBytesAsync(uri, cancellationToken));
            return bytes is null ? null : CreateBitmap(bytes);
        }

        var cachePath = await diskCacheFiles.GetOrAdd(
            imageUrl,
            _ => LoadDiskCachedFileAsync(imageUrl, uri, cancellationToken));
        if (cachePath is null)
        {
            diskCacheFiles.TryRemove(imageUrl, out _);
            return null;
        }

        return CreateBitmap(cachePath);
    }

    private async Task<string?> LoadDiskCachedFileAsync(
        string imageUrl,
        Uri uri,
        CancellationToken cancellationToken)
    {
        var cachePath = GetDiskCachePath(imageUrl);
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        return await DownloadFileAsync(uri, cachePath, cancellationToken);
    }

    private async Task<string?> DownloadFileAsync(
        Uri uri,
        string cachePath,
        CancellationToken cancellationToken)
    {
        var partialPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await using (var input = await httpClient.GetStreamAsync(uri, cancellationToken))
            await using (var output = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            File.Move(partialPath, cachePath, overwrite: true);
            return cachePath;
        }
        catch
        {
            DeleteFileIfExists(partialPath);
            return null;
        }
    }

    private async Task<byte[]?> DownloadBytesAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetByteArrayAsync(uri, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap CreateBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    private static Bitmap CreateBitmap(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return new Bitmap(stream);
    }

    private string GetDiskCachePath(string imageUrl)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl)));
        return Path.Combine(diskCacheDirectory!, $"{hash}.img");
    }

    private void ClearDiskCache()
    {
        if (diskCacheDirectory is null)
        {
            return;
        }

        try
        {
            Directory.Delete(diskCacheDirectory, recursive: true);
        }
        catch
        {
        }

        try
        {
            Directory.CreateDirectory(diskCacheDirectory);
        }
        catch
        {
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

    private static void DisposeWhenReady(Task<Bitmap?> imageTask)
    {
        if (imageTask.IsCompletedSuccessfully)
        {
            imageTask.Result?.Dispose();
            return;
        }

        _ = DisposeWhenReadyAsync(imageTask);
    }

    private static async Task DisposeWhenReadyAsync(Task<Bitmap?> imageTask)
    {
        try
        {
            var image = await imageTask.ConfigureAwait(false);
            image?.Dispose();
        }
        catch
        {
        }
    }
}
