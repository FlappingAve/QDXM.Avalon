using QDXM.Avalon.Core.Search;
using QDXM.Avalon.Core.Tools;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Services;

public sealed class SearchResultFactory
{
    private readonly Action<SearchResultViewModel, string> actionCompleted;
    private readonly Func<SearchResultViewModel, Task> loadAlbumTracks;
    private readonly Func<SearchResultViewModel, Task> loadPlaylistTracks;
    private readonly Func<SearchResultViewModel, int, Task> loadPlaylistTrackPage;
    private readonly Func<SearchResultViewModel, Task> downloadPrimary;
    private readonly Func<SearchResultViewModel, Task> downloadSelected;
    private readonly Func<SearchResultViewModel, Task> openEntityAlbums;
    private readonly Func<SearchResultViewModel, Task> downloadSourceAlbum;
    private readonly Func<SearchResultViewModel, Task> openSourceAlbum;
    private readonly Func<SearchResultViewModel, Task> openSourceArtist;
    private readonly Action<SearchResultViewModel> trackSelectionChanged;
    private readonly Func<PreviewTrackRequest, Task>? previewTrack;
    private readonly Action<string>? clearPreviewContext;
    private readonly RemoteImageCache imageCache;

    public SearchResultFactory(
        Action<SearchResultViewModel, string> actionCompleted,
        Func<SearchResultViewModel, Task> loadAlbumTracks,
        Func<SearchResultViewModel, Task> loadPlaylistTracks,
        Func<SearchResultViewModel, int, Task> loadPlaylistTrackPage,
        Func<SearchResultViewModel, Task> downloadPrimary,
        Func<SearchResultViewModel, Task> downloadSelected,
        Func<SearchResultViewModel, Task> openEntityAlbums,
        Func<SearchResultViewModel, Task> downloadSourceAlbum,
        Func<SearchResultViewModel, Task> openSourceAlbum,
        Func<SearchResultViewModel, Task> openSourceArtist,
        Action<SearchResultViewModel> trackSelectionChanged,
        Func<PreviewTrackRequest, Task>? previewTrack = null,
        Action<string>? clearPreviewContext = null,
        RemoteImageCache? imageCache = null)
    {
        this.actionCompleted = actionCompleted;
        this.loadAlbumTracks = loadAlbumTracks;
        this.loadPlaylistTracks = loadPlaylistTracks;
        this.loadPlaylistTrackPage = loadPlaylistTrackPage;
        this.downloadPrimary = downloadPrimary;
        this.downloadSelected = downloadSelected;
        this.openEntityAlbums = openEntityAlbums;
        this.downloadSourceAlbum = downloadSourceAlbum;
        this.openSourceAlbum = openSourceAlbum;
        this.openSourceArtist = openSourceArtist;
        this.trackSelectionChanged = trackSelectionChanged;
        this.previewTrack = previewTrack;
        this.clearPreviewContext = clearPreviewContext;
        this.imageCache = imageCache ?? RemoteImageCache.Shared;
    }

    public SearchResultViewModel CreateAlbum(SearchAlbumResult album)
    {
        var result = new SearchResultViewModel(
            actionCompleted,
            loadAlbumTracks,
            loadTrackPage: null,
            downloadPrimary,
            downloadSelected,
            clearPreviewContext: clearPreviewContext,
            imageCache: imageCache)
        {
            Id = album.AlbumId,
            IsAlbum = true,
            Title = album.Title,
            Version = album.Version,
            Artist = album.Artist,
            Quality = album.Quality,
            ReleaseDate = album.ReleaseDate,
            TracksDisplay = SearchResultDisplayText.FormatTrackCount(album.TotalTracks),
            TotalTracks = album.TotalTracks,
            TotalDiscs = album.TotalDiscs,
            Explicit = album.Explicit,
            StoreUrl = album.StoreUrl,
            WebPlayerUrl = album.WebPlayerUrl,
            ThumbnailUrl = album.ThumbnailUrl,
            ThumbnailDisplayUrl = GetSearchThumbnailUrl(album.ThumbnailUrl)
        };

        foreach (var track in album.Tracks)
        {
            result.Tracks.Add(CreateTrackSelection(track, result, isSelected: true));
        }

        result.NotifyAlbumTrackRowsChanged();
        result.NotifyTrackSelectionChanged();
        return result;
    }

    public SearchResultViewModel CreateTrack(SearchTrackResult track)
    {
        var result = new SearchResultViewModel(
            actionCompleted,
            loadTracks: null,
            loadTrackPage: null,
            downloadPrimary,
            downloadSelected: null,
            downloadSourceAlbum: downloadSourceAlbum,
            openSourceAlbum: openSourceAlbum,
            openSourceArtist: openSourceArtist,
            previewPrimary: previewTrack,
            imageCache: imageCache)
        {
            Id = track.TrackId,
            AlbumId = track.AlbumId,
            ArtistId = track.ArtistId,
            IsAlbum = false,
            Title = track.Title,
            Version = track.Version,
            AlbumTitle = track.AlbumTitle,
            Artist = track.Artist,
            Quality = track.Quality,
            ReleaseDate = track.ReleaseDate,
            TracksDisplay = "1 track",
            TotalTracks = 1,
            TotalDiscs = 1,
            Explicit = track.Explicit,
            StoreUrl = track.StoreUrl,
            WebPlayerUrl = track.WebPlayerUrl,
            ThumbnailUrl = track.ThumbnailUrl,
            ThumbnailDisplayUrl = GetSearchThumbnailUrl(track.ThumbnailUrl)
        };

        return result;
    }

    public SearchResultViewModel CreatePlaylist(SearchPlaylistResult playlist)
    {
        var result = new SearchResultViewModel(
            actionCompleted,
            loadPlaylistTracks,
            loadPlaylistTrackPage,
            downloadPrimary,
            downloadSelected,
            clearPreviewContext: clearPreviewContext,
            imageCache: imageCache)
        {
            Id = playlist.PlaylistId,
            IsAlbum = false,
            IsPlaylist = true,
            Title = playlist.Title,
            Artist = playlist.Owner,
            Quality = string.Empty,
            ReleaseDate = playlist.UpdatedDate,
            TracksDisplay = SearchResultDisplayText.FormatTrackCount(playlist.TotalTracks),
            TotalTracks = playlist.TotalTracks,
            TotalDiscs = 1,
            WebPlayerUrl = playlist.WebPlayerUrl,
            ThumbnailUrl = playlist.ThumbnailUrl,
            ThumbnailDisplayUrl = GetSearchThumbnailUrl(playlist.ThumbnailUrl)
        };

        return result;
    }

    public SearchResultViewModel CreateArtist(SearchArtistResult artist)
    {
        var result = new SearchResultViewModel(
            actionCompleted,
            openAlbums: openEntityAlbums,
            imageCache: imageCache)
        {
            Id = artist.ArtistId,
            IsAlbum = false,
            IsArtist = true,
            Title = artist.Name,
            Artist = "Artist",
            TracksDisplay = FormatAlbumCount(artist.AlbumsCount),
            TotalTracks = artist.AlbumsCount,
            TotalAlbums = artist.AlbumsCount,
            TotalDiscs = 1,
            WebPlayerUrl = artist.WebPlayerUrl,
            ThumbnailUrl = artist.ThumbnailUrl,
            ThumbnailDisplayUrl = GetSearchThumbnailUrl(artist.ThumbnailUrl)
        };

        return result;
    }

    public SearchResultViewModel CreateLabel(SearchLabelResult label)
    {
        return new SearchResultViewModel(
            actionCompleted,
            openAlbums: openEntityAlbums,
            imageCache: imageCache)
        {
            Id = label.LabelId,
            IsAlbum = false,
            IsLabel = true,
            Title = label.Name,
            Artist = "Label",
            TracksDisplay = FormatAlbumCount(label.AlbumsCount),
            TotalTracks = label.AlbumsCount,
            TotalAlbums = label.AlbumsCount,
            TotalDiscs = 1,
            WebPlayerUrl = label.WebPlayerUrl
        };
    }

    public AlbumTrackSelectionViewModel CreateTrackSelection(
        SearchAlbumTrack track,
        SearchResultViewModel owner,
        bool isSelected)
    {
        return new AlbumTrackSelectionViewModel(
            track.TrackId,
            track.TrackNumber,
            track.DiscNumber,
            track.Title,
            track.Version,
            track.Work,
            track.Composer,
            StringTools.FormatDuration(track.Duration),
            track.Quality,
            isSelected,
            _ =>
            {
                owner.NotifyTrackSelectionChanged();
                trackSelectionChanged(owner);
            },
            previewRequested: selection => PlayPreviewTrack(selection, owner));
    }

    public AlbumTrackSelectionViewModel CreatePlaylistTrackSelection(
        SearchPlaylistTrackResult track,
        SearchResultViewModel owner,
        bool isSelected)
    {
        return new AlbumTrackSelectionViewModel(
            track.TrackId,
            track.PlaylistPosition,
            discNumber: 0,
            track.Title,
            track.Version,
            work: string.Empty,
            composer: string.Empty,
            StringTools.FormatDuration(track.Duration),
            quality: string.Empty,
            isSelected,
            selection =>
            {
                owner.NotifyTrackSelectionChanged(selection);
                trackSelectionChanged(owner);
            },
            selectionKey: track.SelectionKey,
            playlistPositionDisplay: track.PlaylistPositionDisplay,
            albumTrackNumber: track.AlbumTrackNumber,
            albumDiscNumber: track.AlbumDiscNumber,
            albumPositionDisplay: track.AlbumPositionDisplay,
            artist: track.Artist,
            albumTitle: track.AlbumTitle,
            previewRequested: selection => PlayPreviewTrack(selection, owner));
    }

    private Task PlayPreviewTrack(
        AlbumTrackSelectionViewModel track,
        SearchResultViewModel owner)
    {
        return previewTrack?.Invoke(CreatePreviewTrackRequest(track, owner)) ?? Task.CompletedTask;
    }

    private static PreviewTrackRequest CreatePreviewTrackRequest(
        AlbumTrackSelectionViewModel track,
        SearchResultViewModel owner)
    {
        var albumTitle = !string.IsNullOrWhiteSpace(track.AlbumTitle)
            ? track.AlbumTitle
            : owner.Title;

        return new PreviewTrackRequest(
            track.TrackId,
            track.Title,
            albumTitle,
            owner.PreviewContextKey);
    }

    private static string FormatAlbumCount(int albums)
    {
        return albums == 1 ? "1 album" : $"{albums} albums";
    }

    private static string GetSearchThumbnailUrl(string imageUrl)
    {
        return CoverArtUrlSelector.GetImageUrlForSize(imageUrl, "150");
    }
}
