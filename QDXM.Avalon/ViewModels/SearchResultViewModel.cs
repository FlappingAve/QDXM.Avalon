using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.ViewModels;

public partial class SearchResultViewModel : ViewModelBase
{
    private readonly Action<SearchResultViewModel, string> actionCompleted;
    private readonly Func<SearchResultViewModel, Task>? loadTracks;
    private readonly Func<SearchResultViewModel, int, Task>? loadTrackPage;
    private readonly Func<SearchResultViewModel, Task>? downloadPrimary;
    private readonly Func<SearchResultViewModel, Task>? downloadSelected;
    private readonly Func<SearchResultViewModel, Task>? openAlbums;
    private readonly Func<SearchResultViewModel, Task>? downloadSourceAlbum;
    private readonly Func<SearchResultViewModel, Task>? openSourceAlbum;
    private readonly Func<SearchResultViewModel, Task>? openSourceArtist;
    private readonly Func<PreviewTrackRequest, Task>? previewPrimary;
    private readonly Action<string>? clearPreviewContext;
    private readonly RemoteImageCache imageCache;
    private readonly Dictionary<int, IReadOnlyList<AlbumTrackSelectionViewModel>> trackPageCache = [];
    private readonly List<string> playlistSelectionOrder = [];
    private int thumbnailVisualReferenceCount;
    private int thumbnailLoadVersion;

    public SearchResultViewModel(
        Action<SearchResultViewModel, string> actionCompleted,
        Func<SearchResultViewModel, Task>? loadTracks = null,
        Func<SearchResultViewModel, int, Task>? loadTrackPage = null,
        Func<SearchResultViewModel, Task>? downloadPrimary = null,
        Func<SearchResultViewModel, Task>? downloadSelected = null,
        Func<SearchResultViewModel, Task>? openAlbums = null,
        Func<SearchResultViewModel, Task>? downloadSourceAlbum = null,
        Func<SearchResultViewModel, Task>? openSourceAlbum = null,
        Func<SearchResultViewModel, Task>? openSourceArtist = null,
        Func<PreviewTrackRequest, Task>? previewPrimary = null,
        Action<string>? clearPreviewContext = null,
        RemoteImageCache? imageCache = null)
    {
        this.actionCompleted = actionCompleted;
        this.loadTracks = loadTracks;
        this.loadTrackPage = loadTrackPage;
        this.downloadPrimary = downloadPrimary;
        this.downloadSelected = downloadSelected;
        this.openAlbums = openAlbums;
        this.downloadSourceAlbum = downloadSourceAlbum;
        this.openSourceAlbum = openSourceAlbum;
        this.openSourceArtist = openSourceArtist;
        this.previewPrimary = previewPrimary;
        this.clearPreviewContext = clearPreviewContext;
        this.imageCache = imageCache ?? RemoteImageCache.Shared;
    }

    public string Id { get; init; } = string.Empty;
    public string AlbumId { get; init; } = string.Empty;
    public string ArtistId { get; init; } = string.Empty;
    public bool IsAlbum { get; init; } = true;
    public bool IsPlaylist { get; init; }
    public bool IsArtist { get; init; }
    public bool IsLabel { get; init; }
    public bool IsDownloadable => IsAlbum || IsPlaylist || (!IsArtist && !IsLabel);
    public bool CanOpenAlbums => IsArtist || IsLabel;
    public bool CanExpand => IsAlbum || IsPlaylist;
    public bool IsEntityResult => IsArtist || IsLabel;
    public bool IsNotEntityResult => !IsEntityResult;
    public bool IsTrackResult => !CanExpand && IsNotEntityResult;
    public bool CanUseTrackAlbumActions => IsTrackResult && !string.IsNullOrWhiteSpace(AlbumId);
    public bool CanPlayPrimaryPreview => previewPrimary is not null && !CanExpand && IsNotEntityResult;
    public string PreviewContextKey => IsPlaylist
        ? $"playlist:{Id}"
        : IsAlbum
            ? $"album:{Id}"
            : $"track:{Id}";
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string AlbumTitle { get; init; } = string.Empty;
    public string DisplayTitle => Title;
    public bool HasVersion => !string.IsNullOrWhiteSpace(Version);
    public string Artist { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;
    public string ReleaseDate { get; init; } = string.Empty;
    public string TracksDisplay { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int TotalAlbums { get; set; }
    public int TotalDiscs { get; set; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string ThumbnailDisplayUrl { get; init; } = string.Empty;
    public string StoreUrl { get; init; } = string.Empty;
    public string WebPlayerUrl { get; init; } = string.Empty;
    public bool Explicit { get; init; }
    public ObservableCollection<AlbumTrackSelectionViewModel> Tracks { get; } = [];
    public IReadOnlyList<AlbumTrackListRowViewModel> AlbumTrackRows =>
        AlbumTrackListRowBuilder.Build(Tracks, TotalDiscs);
    public int TrackPageSize { get; init; } = QobuzApiLimits.PlaylistTrackPreviewPageSize;

    [ObservableProperty]
    private Bitmap? thumbnailImage;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandedTrackListVisible))]
    [NotifyPropertyChangedFor(nameof(AlbumExpandedTrackListVisible))]
    [NotifyPropertyChangedFor(nameof(PlaylistExpandedTrackListVisible))]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTrackSummary))]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewIdle))]
    private bool isPreviewActive;

    [ObservableProperty]
    private bool isPreviewPlaying;

    public bool IsPreviewIdle => !IsPreviewActive;

    public bool IsCollapsed => !IsExpanded;
    public bool ExpandedTrackListVisible => CanExpand && IsExpanded;
    public bool AlbumExpandedTrackListVisible => IsAlbum && IsExpanded;
    public bool PlaylistExpandedTrackListVisible => IsPlaylist && IsExpanded;
    public bool HasSelectedTrackSummary => CanExpand && IsExpanded;
    public bool CanShowDownloadSelected => CanExpand && IsExpanded && SelectedTrackCount > 0;
    public string PrimaryActionText => CanOpenAlbums
        ? IsArtist
            ? "Select Artist"
            : "Select Label"
        : IsAlbum
            ? "Album"
            : IsPlaylist
                ? "Playlist"
                : "Track";
    public bool HasPrimaryActionIcon => !CanOpenAlbums;

    public int SelectedTrackCount => IsPlaylist
        ? playlistSelectionOrder.Count
        : Tracks.Count(track => track.IsSelected);

    public bool AllTracksSelected
    {
        get => Tracks.Count > 0 && Tracks.All(track => track.IsSelected);
        set
        {
            foreach (var track in Tracks)
            {
                track.IsSelected = value;
            }

            NotifyTrackSelectionChanged();
        }
    }

    public string SelectedTrackSummary => HasSelectedTrackSummary
        ? $"{SelectedTrackCount} selected"
        : string.Empty;

    public int TrackPageIndex { get; private set; }
    public int TrackPageNumber => TrackPageIndex + 1;
    public int TrackPageCount => TotalTracks <= 0 ? 1 : (int)Math.Ceiling((double)TotalTracks / TrackPageSize);
    public bool CanGoPreviousTrackPage => IsPlaylist && TrackPageIndex > 0;
    public bool CanGoNextTrackPage => IsPlaylist && TrackPageNumber < TrackPageCount;
    public string TrackPageText => $"Page {TrackPageNumber} of {TrackPageCount}";
    public string TrackRangeText
    {
        get
        {
            if (!IsPlaylist || TotalTracks <= 0 || Tracks.Count == 0)
            {
                return string.Empty;
            }

            var first = TrackPageIndex * TrackPageSize + 1;
            var last = Math.Min(TotalTracks, first + Tracks.Count - 1);
            return $"Tracks {first}-{last} of {TotalTracks}";
        }
    }

    public IReadOnlyList<string> SelectedTrackSelectionKeys => IsPlaylist
        ? playlistSelectionOrder.ToList()
        : Tracks
            .Where(track => track.IsSelected && !string.IsNullOrWhiteSpace(track.TrackId))
            .Select(track => track.TrackId)
            .ToList();

    public IReadOnlyList<AlbumTrackSelectionViewModel> SelectedTracksForPreview => IsPlaylist
        ? GetSelectedPlaylistTracksInSelectionOrder()
        : Tracks
            .Where(track => track.IsSelected)
            .ToList();

    public bool CanDownloadSelected() => CanExpand && IsExpanded && SelectedTrackCount > 0;

    [RelayCommand]
    private async Task ToggleExpanded()
    {
        if (!CanExpand)
        {
            return;
        }

        var isExpanding = !IsExpanded;
        IsExpanded = isExpanding;
        NotifyTrackSelectionChanged();

        if (!isExpanding)
        {
            clearPreviewContext?.Invoke(PreviewContextKey);
        }

        if (isExpanding && Tracks.Count == 0 && loadTracks is not null)
        {
            await loadTracks(this);
            NotifyTrackSelectionChanged();
        }
    }

    [RelayCommand]
    private async Task PreviousTrackPage()
    {
        if (!CanGoPreviousTrackPage || loadTrackPage is null)
        {
            return;
        }

        clearPreviewContext?.Invoke(PreviewContextKey);
        await loadTrackPage(this, TrackPageIndex - 1);
    }

    [RelayCommand]
    private async Task NextTrackPage()
    {
        if (!CanGoNextTrackPage || loadTrackPage is null)
        {
            return;
        }

        clearPreviewContext?.Invoke(PreviewContextKey);
        await loadTrackPage(this, TrackPageIndex + 1);
    }

    [RelayCommand]
    private async Task DownloadAlbum()
    {
        if (openAlbums is not null)
        {
            await openAlbums(this);
            return;
        }

        if (downloadPrimary is not null)
        {
            await downloadPrimary(this);
            return;
        }

        actionCompleted(this, IsAlbum ? "Album queued" : "Track queued");
    }

    [RelayCommand(CanExecute = nameof(CanDownloadSelected))]
    private async Task DownloadSelected()
    {
        if (downloadSelected is not null)
        {
            await downloadSelected(this);
            return;
        }

        actionCompleted(this, $"{SelectedTrackCount} selected tracks queued");
    }

    [RelayCommand]
    private async Task PlayPreview()
    {
        if (previewPrimary is null)
        {
            return;
        }

        await previewPrimary(new PreviewTrackRequest(
            Id,
            Title,
            AlbumTitle,
            PreviewContextKey));
    }

    [RelayCommand]
    private async Task DownloadSourceAlbum()
    {
        if (downloadSourceAlbum is not null)
        {
            await downloadSourceAlbum(this);
        }
    }

    [RelayCommand]
    private async Task GoToSourceAlbum()
    {
        if (openSourceAlbum is not null)
        {
            await openSourceAlbum(this);
        }
    }

    [RelayCommand]
    private async Task GoToSourceArtist()
    {
        if (openSourceArtist is not null)
        {
            await openSourceArtist(this);
        }
    }

    [RelayCommand]
    private void OpenQobuz()
    {
        var url = !string.IsNullOrWhiteSpace(WebPlayerUrl)
            ? WebPlayerUrl
            : StoreUrl;

        if (string.IsNullOrWhiteSpace(url))
        {
            actionCompleted(this, "No Qobuz URL is available for this result");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            actionCompleted(this, "Opened Qobuz");
        }
        catch (Exception ex)
        {
            actionCompleted(this, $"Could not open Qobuz: {ex.Message}");
        }
    }

    public void NotifyTrackSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedTrackCount));
        OnPropertyChanged(nameof(SelectedTrackSelectionKeys));
        OnPropertyChanged(nameof(SelectedTracksForPreview));
        OnPropertyChanged(nameof(AllTracksSelected));
        OnPropertyChanged(nameof(HasSelectedTrackSummary));
        OnPropertyChanged(nameof(CanShowDownloadSelected));
        OnPropertyChanged(nameof(SelectedTrackSummary));
        DownloadSelectedCommand.NotifyCanExecuteChanged();
    }

    public void NotifyAlbumTrackRowsChanged()
    {
        OnPropertyChanged(nameof(AlbumTrackRows));
    }

    public void ReleaseHeavyState()
    {
        thumbnailVisualReferenceCount = 0;
        ReleaseThumbnail();
        IsExpanded = false;
        Tracks.Clear();
        trackPageCache.Clear();
        playlistSelectionOrder.Clear();
        NotifyAlbumTrackRowsChanged();
        NotifyTrackSelectionChanged();
    }

    public void UpdateTrackTotal(int totalTracks)
    {
        if (totalTracks <= 0 || totalTracks == TotalTracks)
        {
            return;
        }

        TotalTracks = totalTracks;
        TracksDisplay = SearchResultDisplayText.FormatTrackCount(totalTracks);
        OnPropertyChanged(nameof(TotalTracks));
        OnPropertyChanged(nameof(TracksDisplay));
        OnPropertyChanged(nameof(TrackPageCount));
        OnPropertyChanged(nameof(CanGoNextTrackPage));
        OnPropertyChanged(nameof(TrackPageText));
        OnPropertyChanged(nameof(TrackRangeText));
    }

    public void UpdatePreviewState(string activeTrackId, bool previewIsPlaying)
    {
        var activePrimary = CanPlayPrimaryPreview &&
            !string.IsNullOrWhiteSpace(activeTrackId) &&
            string.Equals(Id, activeTrackId, StringComparison.Ordinal);
        IsPreviewActive = activePrimary;
        IsPreviewPlaying = activePrimary && previewIsPlaying;

        var updatedTracks = new HashSet<AlbumTrackSelectionViewModel>();
        foreach (var track in Tracks)
        {
            UpdateTrackPreviewState(track, activeTrackId, previewIsPlaying);
            updatedTracks.Add(track);
        }

        foreach (var track in trackPageCache.Values.SelectMany(page => page))
        {
            if (updatedTracks.Add(track))
            {
                UpdateTrackPreviewState(track, activeTrackId, previewIsPlaying);
            }
        }
    }

    public void NotifyTrackSelectionChanged(AlbumTrackSelectionViewModel changedTrack)
    {
        if (IsPlaylist && !string.IsNullOrWhiteSpace(changedTrack.SelectionKey))
        {
            if (changedTrack.IsSelected)
            {
                if (!playlistSelectionOrder.Contains(changedTrack.SelectionKey, StringComparer.Ordinal))
                {
                    playlistSelectionOrder.Add(changedTrack.SelectionKey);
                }
            }
            else
            {
                playlistSelectionOrder.RemoveAll(key => string.Equals(
                    key,
                    changedTrack.SelectionKey,
                    StringComparison.Ordinal));
            }
        }

        NotifyTrackSelectionChanged();
    }

    public bool TryShowCachedTrackPage(int pageIndex)
    {
        if (!trackPageCache.TryGetValue(pageIndex, out var tracks))
        {
            return false;
        }

        ShowTrackPage(pageIndex, tracks);
        return true;
    }

    public void SetTrackPage(int pageIndex, IReadOnlyList<AlbumTrackSelectionViewModel> tracks)
    {
        trackPageCache[pageIndex] = tracks;
        ShowTrackPage(pageIndex, tracks);
    }

    public void SetTrackPages(
        int firstPageIndex,
        IReadOnlyList<IReadOnlyList<AlbumTrackSelectionViewModel>> pages,
        int pageIndexToShow)
    {
        for (var index = 0; index < pages.Count; index++)
        {
            trackPageCache[firstPageIndex + index] = pages[index];
        }

        TryShowCachedTrackPage(pageIndexToShow);
    }

    private void ShowTrackPage(int pageIndex, IReadOnlyList<AlbumTrackSelectionViewModel> tracks)
    {
        TrackPageIndex = Math.Max(0, pageIndex);
        Tracks.Clear();
        foreach (var track in tracks)
        {
            Tracks.Add(track);
        }

        NotifyTrackSelectionChanged();
        OnPropertyChanged(nameof(TrackPageIndex));
        OnPropertyChanged(nameof(TrackPageNumber));
        OnPropertyChanged(nameof(TrackPageCount));
        OnPropertyChanged(nameof(CanGoPreviousTrackPage));
        OnPropertyChanged(nameof(CanGoNextTrackPage));
        OnPropertyChanged(nameof(TrackPageText));
        OnPropertyChanged(nameof(TrackRangeText));
    }

    private IReadOnlyList<AlbumTrackSelectionViewModel> GetSelectedPlaylistTracksInSelectionOrder()
    {
        if (playlistSelectionOrder.Count == 0)
        {
            return [];
        }

        var cachedTracks = trackPageCache.Values
            .SelectMany(page => page)
            .Where(track => track.IsSelected)
            .ToDictionary(track => track.SelectionKey, StringComparer.Ordinal);

        return playlistSelectionOrder
            .Select(key => cachedTracks.TryGetValue(key, out var track) ? track : null)
            .Where(track => track is not null)
            .Cast<AlbumTrackSelectionViewModel>()
            .ToList();
    }

    private static void UpdateTrackPreviewState(
        AlbumTrackSelectionViewModel track,
        string activeTrackId,
        bool previewIsPlaying)
    {
        var active = !string.IsNullOrWhiteSpace(activeTrackId) &&
            string.Equals(track.TrackId, activeTrackId, StringComparison.Ordinal);
        track.IsPreviewActive = active;
        track.IsPreviewPlaying = active && previewIsPlaying;
    }

    public void AttachThumbnailVisual()
    {
        thumbnailVisualReferenceCount++;
        LoadThumbnail();
    }

    public void DetachThumbnailVisual(bool keepLoaded)
    {
        if (thumbnailVisualReferenceCount > 0)
        {
            thumbnailVisualReferenceCount--;
        }

        if (thumbnailVisualReferenceCount == 0 && !keepLoaded)
        {
            ReleaseThumbnail();
        }
    }

    public void ReleaseThumbnailIfNotVisible()
    {
        if (thumbnailVisualReferenceCount == 0)
        {
            ReleaseThumbnail();
        }
    }

    public void EnsureThumbnailLoaded()
    {
        LoadThumbnail();
    }

    private void LoadThumbnail()
    {
        if (ThumbnailImage is not null)
        {
            return;
        }

        var loadVersion = ++thumbnailLoadVersion;
        _ = LoadThumbnailAsync(loadVersion);
    }

    private void ReleaseThumbnail()
    {
        thumbnailLoadVersion++;
        var image = ThumbnailImage;
        ThumbnailImage = null;

        if (!imageCache.RetainsDecodedImages)
        {
            image?.Dispose();
        }
    }

    private async Task LoadThumbnailAsync(int loadVersion)
    {
        var thumbnailUrl = string.IsNullOrWhiteSpace(ThumbnailDisplayUrl)
            ? ThumbnailUrl
            : ThumbnailDisplayUrl;
        var bitmap = await imageCache.LoadAsync(thumbnailUrl);
        if (bitmap is null)
        {
            return;
        }

        if (loadVersion != thumbnailLoadVersion)
        {
            DisposeUnretainedImage(bitmap);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (loadVersion == thumbnailLoadVersion)
            {
                ThumbnailImage = bitmap;
                return;
            }

            DisposeUnretainedImage(bitmap);
        });
    }

    private void DisposeUnretainedImage(Bitmap image)
    {
        if (!imageCache.RetainsDecodedImages)
        {
            image.Dispose();
        }
    }
}
