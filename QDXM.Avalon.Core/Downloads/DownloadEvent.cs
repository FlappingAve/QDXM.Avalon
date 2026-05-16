namespace QDXM.Avalon.Core.Downloads;

public abstract record DownloadEvent(string QueueItemId, DateTimeOffset Timestamp);

public sealed record DownloadResolvedEvent(
    string QueueItemId,
    DownloadContentType Type,
    string Title,
    string Artist,
    string Quality,
    int TotalTracks,
    string CoverArtUrl,
    string ReleaseDate,
    string Upc,
    string DestinationPath,
    IReadOnlyList<string>? FilePaths = null,
    long? TotalSizeBytes = null,
    int DestinationPreviewRemainingCount = 0)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record TrackStartedEvent(
    string QueueItemId,
    int TrackNumber,
    int TotalTracks,
    string TrackTitle)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record FileProgressEvent(
    string QueueItemId,
    long BytesReceived,
    long? TotalBytes,
    double MegabytesPerSecond)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record TrackCompletedEvent(
    string QueueItemId,
    int CompletedTracks,
    int TotalTracks,
    string FilePath,
    long? FileSizeBytes = null)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record DownloadCompletedEvent(string QueueItemId, bool HasWarnings)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record DownloadFailedEvent(
    string QueueItemId,
    string Message,
    Exception? Exception = null)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record DownloadWarningEvent(
    string QueueItemId,
    string Message,
    Exception? Exception = null)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record DownloadInfoEvent(
    string QueueItemId,
    string Message)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);

public sealed record PlaylistTrackFailedEvent(
    string QueueItemId,
    int PlaylistPosition,
    string Message,
    Exception? Exception = null)
    : DownloadEvent(QueueItemId, DateTimeOffset.Now);
