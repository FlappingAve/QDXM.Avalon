namespace QDXM.Avalon.Core.Downloads;

public enum DownloadStatus
{
    Queued,
    Resolving,
    Downloading,
    Paused,
    Completed,
    Failed,
    Canceled,
    Skipped,
    Issues
}
