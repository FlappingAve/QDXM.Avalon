using QDXM.Avalon.Core.Downloads;

namespace QDXM.Avalon.Services;

public static class DownloadStatusText
{
    public const string Idle = "Idle";
    public const string Queued = "Queued";
    public const string Resolving = "Resolving";
    public const string ResolvingProgress = "Resolving...";
    public const string Downloading = "Downloading";
    public const string DownloadingProgress = "Downloading...";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string CompletedWithWarnings = "Completed with warnings";
    public const string CompletedWithWarningsDetail = $"{CompletedWithWarnings}. See Logs.";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";
    public const string Skipped = "Skipped";
    public const string Issues = "Issues";
    public const string QueueRestored = "Queue restored";
    public const string PausingAfterCurrentTrack = "Pausing after current track...";
    public const string UnresolvedMetadataPlaceholder = Queued;

    public static string ForStatus(DownloadStatus status)
    {
        return status switch
        {
            DownloadStatus.Queued => Queued,
            DownloadStatus.Resolving => Resolving,
            DownloadStatus.Downloading => Downloading,
            DownloadStatus.Paused => Paused,
            DownloadStatus.Completed => Completed,
            DownloadStatus.Failed => Failed,
            DownloadStatus.Canceled => Canceled,
            DownloadStatus.Skipped => Skipped,
            DownloadStatus.Issues => Issues,
            _ => status.ToString()
        };
    }
}
