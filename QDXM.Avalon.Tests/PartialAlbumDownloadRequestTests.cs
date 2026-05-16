using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Services;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class PartialAlbumDownloadRequestTests
{
    [Fact]
    public void PartialAlbumDownloadRequest_KeepsAlbumAndTrackContextTogether()
    {
        var request = new PartialAlbumDownloadRequest(
            AlbumId: "album-1",
            AlbumUrl: "https://open.qobuz.com/album/album-1",
            TrackIds: ["track-1", "track-3"],
            DisplayTitle: "Piano Miniatures",
            DisplayArtist: "MusicalBasics");

        Assert.Equal("album-1", request.AlbumId);
        Assert.Equal(["track-1", "track-3"], request.TrackIds);
        Assert.Equal("Piano Miniatures", request.DisplayTitle);
        Assert.Equal("MusicalBasics", request.DisplayArtist);
    }

    [Fact]
    public void DestinationPreviewRenderer_UsesSearchAlbumVersion()
    {
        var settings = new AppSettings
        {
            DownloadFolder = TestPaths.DownloadRoot,
            FolderTemplate = @"{AlbumArtist}\({ReleaseYear}) {AlbumTitle} {Version} [{Quality}]",
            FilenameTemplate = "{TrackNumberPadded} - {TrackTitle} ({Version})"
        };
        var result = new SearchResultViewModel((_, _) => { })
        {
            IsAlbum = true,
            Title = "1989",
            Version = "Deluxe Edition",
            Artist = "Taylor Swift",
            Quality = "FLAC 24/44.1",
            ReleaseDate = "2014-10-27",
            TotalTracks = 19
        };
        result.Tracks.Add(new AlbumTrackSelectionViewModel(
            "1",
            trackNumber: 1,
            discNumber: 1,
            title: "Welcome To New York",
            version: string.Empty,
            work: string.Empty,
            composer: string.Empty,
            duration: "3:32",
            quality: "FLAC 24/44.1",
            isSelected: true,
            selectionChanged: _ => { }));

        var preview = DestinationPreviewRenderer.ForSearchResult(result, settings);

        Assert.Contains("(2014) 1989 Deluxe Edition [FLAC (24bit-44.1kHz)]", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void DestinationPreviewRenderer_UsesConnectedTreePrefixesForDownloadPreview()
    {
        var settings = new AppSettings { DownloadFolder = TestPaths.DownloadRoot };
        var item = new DownloadQueueItemViewModel
        {
            Type = DownloadContentType.Playlist,
            DestinationFilePaths =
            [
                Path.Combine(TestPaths.DownloadRoot, "Playlists", "Road Trip", "0001 - First.flac")
            ],
            DestinationPreviewRemainingCount = 1899
        };

        var preview = DestinationPreviewRenderer.ForDownloadItem(item, settings);

        var expected = string.Join(
            '\n',
            TestPaths.DownloadRoot,
            "\u2514\u2500 Playlists",
            "   \u2514\u2500 Road Trip",
            "      \u251c\u2500 0001 - First.flac",
            "      \u2514\u2500 1899 more");

        Assert.Equal(expected, preview.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void DestinationPreviewRenderer_PlaylistSearchPreviewEndsSingleSelectedTrackAsLastLeaf()
    {
        var settings = new AppSettings { DownloadFolder = TestPaths.DownloadRoot };
        var result = new SearchResultViewModel((_, _) => { })
        {
            Id = "playlist-1",
            IsAlbum = false,
            IsPlaylist = true,
            Title = "Road Trip",
            Artist = "MusicEnjoyer",
            TotalTracks = 1
        };
        var track = new AlbumTrackSelectionViewModel(
            "track-1",
            trackNumber: 1,
            discNumber: 0,
            title: "First",
            version: string.Empty,
            work: string.Empty,
            composer: string.Empty,
            duration: "1:00",
            quality: string.Empty,
            isSelected: false,
            selectionKey: "position:1",
            artist: "Example Artist",
            albumTitle: "Example Album");
        result.SetTrackPage(0, [track]);
        track.IsSelected = true;
        result.NotifyTrackSelectionChanged(track);

        var preview = DestinationPreviewRenderer.ForSearchResult(result, settings);

        var expected = string.Join(
            '\n',
            TestPaths.DownloadRoot,
            "\u2514\u2500 Playlists",
            "   \u2514\u2500 Road Trip",
            "      \u2514\u2500 01 - Example Artist - First.flac");

        Assert.Equal(expected, preview.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void SearchResultViewModel_PreservesPlaylistSelectionOrder()
    {
        var result = new SearchResultViewModel((_, _) => { })
        {
            IsAlbum = false,
            IsPlaylist = true,
            TotalTracks = 2
        };
        var first = new AlbumTrackSelectionViewModel(
            "1",
            trackNumber: 1,
            discNumber: 0,
            title: "First",
            version: string.Empty,
            work: string.Empty,
            composer: string.Empty,
            duration: "1:00",
            quality: string.Empty,
            isSelected: false,
            selectionKey: "position:1");
        var second = new AlbumTrackSelectionViewModel(
            "2",
            trackNumber: 2,
            discNumber: 0,
            title: "Second",
            version: string.Empty,
            work: string.Empty,
            composer: string.Empty,
            duration: "1:00",
            quality: string.Empty,
            isSelected: false,
            selectionKey: "position:2");
        result.SetTrackPage(0, [first, second]);

        second.IsSelected = true;
        result.NotifyTrackSelectionChanged(second);
        first.IsSelected = true;
        result.NotifyTrackSelectionChanged(first);

        Assert.Equal(["position:2", "position:1"], result.SelectedTrackSelectionKeys);
    }
}
