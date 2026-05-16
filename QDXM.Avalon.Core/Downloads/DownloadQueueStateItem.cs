namespace QDXM.Avalon.Core.Downloads;

public sealed class DownloadQueueStateItem
{
    public string Id { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public DownloadContentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int CompletedTracks { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public double? FileProgressFraction { get; set; }
    public long? SizeBytes { get; set; }
    public long CompletedSizeBytes { get; set; }
    public long CurrentTrackBytesReceived { get; set; }
    public long? CurrentTrackTotalBytes { get; set; }
    public string DestinationPath { get; set; } = string.Empty;
    public string CoverArtUrl { get; set; } = string.Empty;
    public string CurrentTrackTitle { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string Upc { get; set; } = string.Empty;
    public IReadOnlyList<string> DestinationFilePaths { get; set; } = [];
    public int DestinationPreviewRemainingCount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string WarningMessage { get; set; } = string.Empty;
    public IReadOnlyList<string> SelectedTrackIds { get; set; } = [];
    public IReadOnlyList<int> FailedPlaylistPositions { get; set; } = [];
}
