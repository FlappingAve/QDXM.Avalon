namespace QDXM.Avalon.Core.Downloads;

public sealed record DownloadRequest(
    string SourceUrl,
    DownloadContentType ContentType,
    string ContentId);
