using QDXM.Avalon.Core.Downloads;

namespace QDXM.Avalon.Core.Api;

public static class QobuzUrlBuilder
{
    public static string CreateOpenUrl(DownloadContentType contentType, string? contentId)
    {
        return CreateOpenUrl(GetContentTypePath(contentType), contentId);
    }

    public static string CreateOpenUrl(string contentType, string? contentId)
    {
        return string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(contentId)
            ? string.Empty
            : $"https://open.qobuz.com/{contentType}/{contentId}";
    }

    public static string CreateOpenUrlFromPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : $"https://open.qobuz.com/{path.TrimStart('/')}";
    }

    private static string GetContentTypePath(DownloadContentType contentType)
    {
        return contentType switch
        {
            DownloadContentType.Album => "album",
            DownloadContentType.Track => "track",
            DownloadContentType.Playlist => "playlist",
            DownloadContentType.Artist => "artist",
            DownloadContentType.Label => "label",
            _ => string.Empty
        };
    }
}
