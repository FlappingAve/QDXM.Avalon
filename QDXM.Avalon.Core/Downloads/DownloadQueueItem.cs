namespace QDXM.Avalon.Core.Downloads;

public sealed class DownloadQueueItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string SourceUrl { get; init; } = string.Empty;
    public string ContentId { get; init; } = string.Empty;
    public DownloadContentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int CompletedTracks { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public double? FileProgressFraction { get; set; }
    public long? SizeBytes { get; set; }
    public string DestinationPath { get; set; } = string.Empty;
    public string CoverArtUrl { get; set; } = string.Empty;
    public string CurrentTrackTitle { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string Upc { get; set; } = string.Empty;
    public IReadOnlyList<string> DestinationFilePaths { get; set; } = [];
    public int DestinationPreviewRemainingCount { get; set; }
    public IReadOnlyList<string> SelectedTrackIds { get; init; } = [];
    public IReadOnlyList<int> FailedPlaylistPositions { get; set; } = [];
}
