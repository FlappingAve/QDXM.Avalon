using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using QDXM.Avalon.Services;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.ViewModels;

public partial class DownloadQueueItemViewModel : ViewModelBase
{
    private static readonly DownloadQueueCoverArtCache coverArtCache = DownloadQueueCoverArtCache.Shared;

    private bool finishedJobTransientStateReleased;
    private bool coverArtLoadPending;
    private bool deleteCoverArtCacheAfterPendingLoad;
    private int coverArtLoadVersion;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string SourceUrl { get; init; } = string.Empty;
    public string ContentId { get; init; } = string.Empty;
    public DownloadContentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int CompletedTracks { get; set; }
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
    public IReadOnlyList<string> SelectedTrackIds { get; init; } = [];
    public IReadOnlyList<int> FailedPlaylistPositions { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    private DownloadStatus status = DownloadStatus.Queued;

    [ObservableProperty]
    private Bitmap? coverArtImage;

    public string TypeDisplay => Type.ToString();
    public bool IsAlbum => Type == DownloadContentType.Album;
    public bool IsTrack => Type == DownloadContentType.Track;
    public bool IsOtherType => !IsAlbum && !IsTrack;
    public bool IsPlaylist => Type == DownloadContentType.Playlist;
    public bool IsFavorites => Type == DownloadContentType.Favorites;
    public bool IsUnknownType => IsOtherType && !IsPlaylist && !IsFavorites;
    public QualityDisplayText QualityDisplay => QualityStringMappings.GetDisplayText(Quality);

    public string TracksDisplay => TotalTracks <= 0 ? string.Empty : TotalTracks.ToString();
    public string StatusDisplay => DownloadStatusText.ForStatus(Status);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public string DetailMessage => HasError ? ErrorMessage : WarningMessage;

    public string ProgressText
    {
        get
        {
            if (HasError)
            {
                return ErrorMessage;
            }

            if (TotalTracks > 1)
            {
                return $"{CompletedTracks}/{TotalTracks} tracks";
            }

            if (HasWarning)
            {
                return WarningMessage;
            }

            return FileProgressFraction is null
                ? string.Empty
                : $"{FileProgressFraction.Value:P0}";
        }
    }

    public string TracksDetailText =>
        TotalTracks > 1
            ? $"{CompletedTracks}/{TotalTracks} tracks"
            : TotalTracks == 1
                ? "1 track"
                : string.Empty;

    public double ProgressBarValue =>
        TotalTracks > 1 && TotalTracks > 0
            ? (double)CompletedTracks / TotalTracks
            : FileProgressFraction ?? 0d;

    private long? DisplaySizeBytes =>
        SizeBytes ??
        (CompletedSizeBytes + (CurrentTrackTotalBytes ?? CurrentTrackBytesReceived) > 0
            ? CompletedSizeBytes + (CurrentTrackTotalBytes ?? CurrentTrackBytesReceived)
            : null);

    public string SizeDisplay => DisplaySizeBytes is null
        ? "--"
        : FormatSize(DisplaySizeBytes.Value);

    private bool HasPlaylistWarning => Type == DownloadContentType.Playlist && HasWarning;

    public IBrush StatusBrush => Status switch
    {
        _ when HasPlaylistWarning => Brushes.Yellow,
        DownloadStatus.Downloading or DownloadStatus.Resolving => Brushes.DodgerBlue,
        DownloadStatus.Completed => Brushes.LimeGreen,
        DownloadStatus.Issues => Brushes.Yellow,
        DownloadStatus.Paused => Brushes.Orange,
        DownloadStatus.Failed => Brushes.IndianRed,
        _ => Brushes.LightSlateGray
    };

    public IBrush ProgressBrush => Status switch
    {
        _ when HasPlaylistWarning => Brushes.Yellow,
        DownloadStatus.Completed => Brushes.LimeGreen,
        DownloadStatus.Issues => Brushes.Yellow,
        DownloadStatus.Paused => Brushes.Orange,
        DownloadStatus.Failed => Brushes.IndianRed,
        DownloadStatus.Downloading or DownloadStatus.Resolving => Brushes.DodgerBlue,
        _ => Brushes.LightSlateGray
    };

    public DownloadQueueItem ToCoreItem()
    {
        return new DownloadQueueItem
        {
            Id = Id,
            SourceUrl = SourceUrl,
            ContentId = ContentId,
            Type = Type,
            Title = Title,
            Artist = Artist,
            Quality = Quality,
            TotalTracks = TotalTracks,
            CompletedTracks = CompletedTracks,
            Status = Status,
            FileProgressFraction = FileProgressFraction,
            SizeBytes = SizeBytes,
            DestinationPath = DestinationPath,
            CoverArtUrl = CoverArtUrl,
            CurrentTrackTitle = CurrentTrackTitle,
            ReleaseDate = ReleaseDate,
            Upc = Upc,
            DestinationFilePaths = DestinationFilePaths,
            DestinationPreviewRemainingCount = DestinationPreviewRemainingCount,
            SelectedTrackIds = SelectedTrackIds,
            FailedPlaylistPositions = FailedPlaylistPositions
        };
    }

    private static string FormatSize(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;
        return megabytes > 999d
            ? $"{megabytes / 1024d:0.00} GB"
            : $"{megabytes:0.0} MB";
    }

    public void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(TypeDisplay));
        OnPropertyChanged(nameof(IsAlbum));
        OnPropertyChanged(nameof(IsTrack));
        OnPropertyChanged(nameof(IsOtherType));
        OnPropertyChanged(nameof(IsPlaylist));
        OnPropertyChanged(nameof(IsFavorites));
        OnPropertyChanged(nameof(IsUnknownType));
        OnPropertyChanged(nameof(TracksDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(WarningMessage));
        OnPropertyChanged(nameof(DetailMessage));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(TracksDetailText));
        OnPropertyChanged(nameof(ProgressBarValue));
        OnPropertyChanged(nameof(SizeDisplay));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(ProgressBrush));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(QualityDisplay));
        OnPropertyChanged(nameof(DestinationPath));
        OnPropertyChanged(nameof(CoverArtUrl));
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(ReleaseDate));
        OnPropertyChanged(nameof(Upc));
        OnPropertyChanged(nameof(DestinationFilePaths));
        OnPropertyChanged(nameof(DestinationPreviewRemainingCount));
        OnPropertyChanged(nameof(FailedPlaylistPositions));
    }

    public void LoadSelectedCoverArt()
    {
        LoadCoverArt(allowFinishedJobLoad: true);
    }

    public void UnloadCoverArt()
    {
        coverArtLoadVersion++;
        coverArtLoadPending = false;
        var image = CoverArtImage;
        CoverArtImage = null;
        image?.Dispose();
    }

    public void DeleteCoverArtCache()
    {
        deleteCoverArtCacheAfterPendingLoad = true;
        UnloadCoverArt();
        coverArtCache.Delete(Id);
    }

    private void LoadCoverArt(bool allowFinishedJobLoad)
    {
        if ((finishedJobTransientStateReleased && !allowFinishedJobLoad) ||
            CoverArtImage is not null ||
            coverArtLoadPending ||
            string.IsNullOrWhiteSpace(CoverArtUrl))
        {
            return;
        }

        deleteCoverArtCacheAfterPendingLoad = false;
        coverArtLoadPending = true;
        _ = LoadCoverArtAsync(++coverArtLoadVersion, allowFinishedJobLoad);
    }

    public void ReleaseFinishedJobTransientState()
    {
        finishedJobTransientStateReleased = true;
        UnloadCoverArt();
        CurrentTrackTitle = string.Empty;
        CurrentTrackBytesReceived = 0;
        CurrentTrackTotalBytes = null;
        FileProgressFraction = 1d;
    }

    private async Task LoadCoverArtAsync(int loadVersion, bool allowFinishedJobLoad)
    {
        var requestedCoverArtUrl = CoverArtUrl;
        var bitmap = await coverArtCache.LoadAsync(Id, requestedCoverArtUrl);
        if (deleteCoverArtCacheAfterPendingLoad)
        {
            bitmap?.Dispose();
            coverArtCache.Delete(Id);
            return;
        }

        if (bitmap is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (loadVersion == coverArtLoadVersion)
                {
                    coverArtLoadPending = false;
                }
            });
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (loadVersion == coverArtLoadVersion &&
                (!finishedJobTransientStateReleased || allowFinishedJobLoad) &&
                string.Equals(CoverArtUrl, requestedCoverArtUrl, StringComparison.Ordinal))
            {
                CoverArtImage = bitmap;
                coverArtLoadPending = false;
                return;
            }

            if (loadVersion == coverArtLoadVersion)
            {
                coverArtLoadPending = false;
            }

            if (deleteCoverArtCacheAfterPendingLoad)
            {
                coverArtCache.Delete(Id);
            }

            bitmap.Dispose();
        });
    }
}
