using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Services;

public static class DownloadQueueItemStateMapper
{
    public static DownloadQueueStateItem ToStateItem(DownloadQueueItemViewModel item)
    {
        return new DownloadQueueStateItem
        {
            Id = item.Id,
            SourceUrl = item.SourceUrl,
            ContentId = item.ContentId,
            Type = item.Type,
            Title = item.Title,
            Artist = item.Artist,
            Quality = item.Quality,
            TotalTracks = item.TotalTracks,
            CompletedTracks = item.CompletedTracks,
            Status = item.Status,
            FileProgressFraction = item.FileProgressFraction,
            SizeBytes = item.SizeBytes,
            CompletedSizeBytes = item.CompletedSizeBytes,
            CurrentTrackBytesReceived = item.CurrentTrackBytesReceived,
            CurrentTrackTotalBytes = item.CurrentTrackTotalBytes,
            DestinationPath = item.DestinationPath,
            CoverArtUrl = item.CoverArtUrl,
            CurrentTrackTitle = item.CurrentTrackTitle,
            ReleaseDate = item.ReleaseDate,
            Upc = item.Upc,
            DestinationFilePaths = item.DestinationFilePaths.ToList(),
            DestinationPreviewRemainingCount = item.DestinationPreviewRemainingCount,
            ErrorMessage = item.ErrorMessage,
            WarningMessage = item.WarningMessage,
            SelectedTrackIds = item.SelectedTrackIds.ToList(),
            FailedPlaylistPositions = item.FailedPlaylistPositions.ToList()
        };
    }

    public static DownloadQueueItemViewModel ToViewModel(DownloadQueueStateItem item)
    {
        return new DownloadQueueItemViewModel
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
            SourceUrl = item.SourceUrl,
            ContentId = item.ContentId,
            Type = item.Type,
            Title = item.Title,
            Artist = item.Artist,
            Quality = item.Quality,
            TotalTracks = item.TotalTracks,
            CompletedTracks = item.CompletedTracks,
            Status = NormalizeRestoredStatus(item.Status),
            FileProgressFraction = item.FileProgressFraction,
            SizeBytes = item.SizeBytes,
            CompletedSizeBytes = item.CompletedSizeBytes,
            CurrentTrackBytesReceived = item.CurrentTrackBytesReceived,
            CurrentTrackTotalBytes = item.CurrentTrackTotalBytes,
            DestinationPath = item.DestinationPath,
            CoverArtUrl = item.CoverArtUrl,
            CurrentTrackTitle = item.CurrentTrackTitle,
            ReleaseDate = item.ReleaseDate,
            Upc = item.Upc,
            DestinationFilePaths = item.DestinationFilePaths,
            DestinationPreviewRemainingCount = item.DestinationPreviewRemainingCount,
            ErrorMessage = item.ErrorMessage,
            WarningMessage = item.WarningMessage,
            SelectedTrackIds = item.SelectedTrackIds,
            FailedPlaylistPositions = item.FailedPlaylistPositions
        };
    }

    private static DownloadStatus NormalizeRestoredStatus(DownloadStatus status)
    {
        return status is DownloadStatus.Resolving or DownloadStatus.Downloading
            ? DownloadStatus.Paused
            : status;
    }
}
