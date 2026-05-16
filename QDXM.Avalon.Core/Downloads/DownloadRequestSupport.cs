namespace QDXM.Avalon.Core.Downloads;

public static class DownloadRequestSupport
{
    public static bool IsSupportedNow(DownloadContentType contentType)
    {
        return contentType is DownloadContentType.Album or DownloadContentType.Track or DownloadContentType.Playlist or DownloadContentType.Favorites;
    }

    public static string GetUnsupportedMessage(DownloadContentType contentType)
    {
        return $"{contentType} downloads are not supported in QDXM Avalon yet.";
    }
}
