using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;
using QobuzApiSharp.Models.Content;

namespace QDXM.Avalon.Tests;

public sealed class PathTemplateRendererCharacterizationTests
{
    [Fact]
    public void RenderAlbumDestination_SplitsSlashAndBackslashIntoSubfolders()
    {
        var album = CreateAlbum();

        var path = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"{AlbumArtist}/{AlbumTitle}\{Quality}",
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96");

        Assert.Equal(
            Path.Combine(@"D:\Sort", "Example Artist", "Example Album", "FLAC (24bit-96kHz)"),
            path);
    }

    [Fact]
    public void RenderAlbumDestination_StripsInvalidFilenameCharactersFromRenderedSegments()
    {
        var album = CreateAlbum(title: "Bad:Name / Deluxe?");

        var path = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            "{AlbumTitle}",
            album,
            "Example Artist",
            "Bad:Name / Deluxe?",
            "FLAC 24/96");

        Assert.Equal(Path.Combine(@"D:\Sort", "Bad Name Deluxe"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenderAudioFilename_UsesDefaultTemplateWhenFilenameTemplateIsBlank(string? filenameTemplate)
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, trackNumber: 7, title: "Example Track");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            filenameTemplate,
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150,
            trackNumberPaddingWidth: 12);

        Assert.Equal("07 - Example Track.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_PreservesLiteralHyphenSpacingFromTemplate()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, trackNumber: 1, title: "Example Track");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{TrackNumberPadded}-{TrackTitle}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150,
            trackNumberPaddingWidth: 12);

        Assert.Equal("01-Example Track.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_TrimsToMaxFileNameLengthButKeepsExtension()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, title: "1234567890");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{TrackTitle}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 12);

        Assert.Equal("1234567.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_RendersRepeatedAndCaseInsensitiveFields()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, trackNumber: 7, title: "Example Track");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{tracktitle} - {TRACKTITLE} - {tracknumberpadded}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150,
            trackNumberPaddingWidth: 12);

        Assert.Equal("Example Track - Example Track - 07.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_LeavesUnknownFieldsLiteral()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, title: "Example Track");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{UnknownField} - {TrackTitle}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("{UnknownField} - Example Track.flac", filename);
    }

    [Theory]
    [InlineData("[{Version}] {TrackTitle}")]
    [InlineData("({Version}) {TrackTitle}")]
    [InlineData("{Version} - {TrackTitle}")]
    public void RenderAudioFilename_RemovesEmptyWrappersAndOrphanSeparators(string template)
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, title: "Example Track", version: string.Empty);

        var filename = PathTemplateRenderer.RenderAudioFilename(
            template,
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("Example Track.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_FallsBackWhenTemplateRendersEmpty()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, trackNumber: 7, title: "Example Track", version: string.Empty);

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{Version}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("07 - Example Track.flac", filename);
    }

    [Fact]
    public void RenderAudioFilenamePreview_FallsBackWhenTemplateRendersEmpty()
    {
        var filename = PathTemplateRenderer.RenderAudioFilenamePreview(
            "{Version}",
            "Preview Artist",
            "Preview Album",
            "FLAC 24/96",
            "2026-05-10",
            totalTracks: 12,
            trackNumber: 7,
            trackTitle: "Preview Track",
            version: string.Empty,
            discNumber: 1,
            totalDiscs: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("07 - Preview Track.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_SuppressesVersionWhenTrackTitleAlreadyContainsVersion()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, title: "Example Track (Live)", version: "Live");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{TrackTitle} ({Version})",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("Example Track (Live).flac", filename);
    }

    [Fact]
    public void RenderDiscFolderSegments_InlineWorkHonorsNoSpacesSeparator()
    {
        var album = CreateAlbum();
        var tracks = new[]
        {
            CreateTrack(album, title: "Track 1", work: "Work 1"),
            CreateTrack(album, title: "Track 2", trackNumber: 2, work: "Work 2")
        };
        album.Tracks = new ItemSearchResult<Track> { Items = tracks.ToList() };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work}",
            "Inline",
            "-",
            workSeparatorNoSpaces: true,
            tracks[0],
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 2);

        Assert.Equal(["Disc 1 - Work 1-Work 2"], segments);
    }

    [Fact]
    public void RenderDiscFolderSegments_InlineOrFoldersUsesInlineWhenDiscHasOneWork()
    {
        var album = CreateAlbum();
        var track = CreateTrack(album, title: "Track 1", work: "Work 1");
        album.Tracks = new ItemSearchResult<Track> { Items = [track] };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work}",
            "Inline or Folders",
            "&",
            workSeparatorNoSpaces: false,
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1);

        Assert.Equal(["Disc 1 - Work 1"], segments);
    }

    [Fact]
    public void RenderDiscFolderSegments_InlineOrFoldersUsesWorkFolderWhenDiscHasMultipleWorks()
    {
        var album = CreateAlbum();
        var tracks = new[]
        {
            CreateTrack(album, title: "Track 1", work: "Work 1"),
            CreateTrack(album, title: "Track 2", trackNumber: 2, work: "Work 2")
        };
        album.Tracks = new ItemSearchResult<Track> { Items = tracks.ToList() };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work}",
            "Inline or Folders",
            "&",
            workSeparatorNoSpaces: false,
            tracks[0],
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 2);

        Assert.Equal(["Disc 1", "Work 1"], segments);
    }

    [Fact]
    public void RenderDiscFolderSegments_WorkFoldersUseFirstComposerForMatchingWork()
    {
        var album = CreateAlbum();
        var tracks = new[]
        {
            CreateTrack(album, title: "Track 1", work: "Symphony No. 5", composer: "First Composer"),
            CreateTrack(album, title: "Track 2", trackNumber: 2, work: "Symphony No. 5", composer: "Second Composer"),
            CreateTrack(album, title: "Track 3", trackNumber: 3, work: "Symphony No. 6", composer: "Third Composer")
        };
        album.Tracks = new ItemSearchResult<Track> { Items = tracks.ToList() };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work} ({WorkComposer})",
            "Folders",
            "&",
            workSeparatorNoSpaces: false,
            tracks[1],
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 3);

        Assert.Equal(["Disc 1", "Symphony No. 5 (First Composer)"], segments);
    }

    [Fact]
    public void RenderDiscFolderSegments_InlineComposerFollowsWorkOrder()
    {
        var album = CreateAlbum();
        var tracks = new[]
        {
            CreateTrack(album, title: "Track 1", work: "Work 1", composer: "Composer 1"),
            CreateTrack(album, title: "Track 2", trackNumber: 2, work: "Work 2", composer: "Composer 2")
        };
        album.Tracks = new ItemSearchResult<Track> { Items = tracks.ToList() };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work} ({WorkComposer})",
            "Inline",
            "&",
            workSeparatorNoSpaces: false,
            tracks[0],
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 2);

        Assert.Equal(["Disc 1 - Work 1 & Work 2 (Composer 1 & Composer 2)"], segments);
    }

    [Fact]
    public void RenderDiscFolderSegments_ComposerDoesNotFallBackToAlbumComposer()
    {
        var album = CreateAlbum();
        album.Composer = new Artist { Name = "Album Composer" };
        var track = CreateTrack(album, title: "Track 1", work: "Work 1");
        album.Tracks = new ItemSearchResult<Track> { Items = [track] };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work} ({WorkComposer})",
            "Inline",
            "&",
            workSeparatorNoSpaces: false,
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1);

        Assert.Equal(["Disc 1 - Work 1"], segments);
    }

    [Fact]
    public void RenderAlbumDestination_RendersAlbumComposerWithoutTrackFallback()
    {
        var album = CreateAlbum();
        album.Composer = new Artist { Name = "Album Composer" };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"{AlbumComposer}\{AlbumTitle}",
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96");

        Assert.Equal(Path.Combine(@"D:\Sort", "Album Composer", "Example Album"), destination);
    }

    [Fact]
    public void RenderAudioFilename_RendersAlbumAndTrackComposerWithoutFallbacks()
    {
        var album = CreateAlbum();
        album.Composer = new Artist { Name = "Album Composer" };
        var track = CreateTrack(album, title: "Track 1", composer: "Track Composer");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{AlbumComposer} - {TrackComposer} - {TrackTitle}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("Album Composer - Track Composer - Track 1.flac", filename);
    }

    [Fact]
    public void RenderAudioFilename_DoesNotFallBackBetweenAlbumAndTrackComposer()
    {
        var album = CreateAlbum();
        album.Composer = new Artist { Name = "Album Composer" };
        var track = CreateTrack(album, title: "Track 1");

        var filename = PathTemplateRenderer.RenderAudioFilename(
            "{TrackComposer} - {TrackTitle}",
            track,
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("Track 1.flac", filename);
    }

    [Fact]
    public void PreviewRenderers_PreserveUiFields()
    {
        var albumPath = PathTemplateRenderer.RenderAlbumDestinationPreview(
            @"D:\Sort",
            @"{AlbumArtist}\{AlbumTitle} [{Quality}]",
            "Preview Artist",
            "Preview Album",
            "FLAC 24/96",
            "2026-05-10",
            totalTracks: 107,
            version: "Preview Version",
            releaseType: "album",
            label: "Preview Label",
            upc: "1234567890123",
            totalDiscs: 10);
        var filename = PathTemplateRenderer.RenderAudioFilenamePreview(
            "{TrackNumberPadded} - {TrackTitle} ({Version})",
            "Preview Artist",
            "Preview Album",
            "FLAC 24/96",
            "2026-05-10",
            totalTracks: 107,
            trackNumber: 3,
            trackTitle: "Preview Track",
            version: "Preview Version",
            discNumber: 2,
            totalDiscs: 10,
            extension: ".flac",
            maxFileNameLength: 150,
            releaseType: "album",
            label: "Preview Label",
            upc: "1234567890123",
            isrc: "USPR32600001",
            trackNumberPaddingWidth: 107);
        var discSegments = PathTemplateRenderer.RenderDiscFolderSegmentsPreview(
            "Disc {DiscNumberPadded} of {TotalDiscsPadded} - {Work}",
            "Inline",
            "&",
            workSeparatorNoSpaces: false,
            "Preview Artist",
            "Preview Album",
            "FLAC 24/96",
            "2026-05-10",
            totalTracks: 107,
            trackNumber: 3,
            trackTitle: "Preview Track",
            version: "Preview Version",
            discNumber: 2,
            totalDiscs: 10,
            works: ["Work 1"],
            currentWork: "Work 1");

        Assert.Equal(Path.Combine(@"D:\Sort", "Preview Artist", "Preview Album [FLAC (24bit-96kHz)]"), albumPath);
        Assert.Equal("003 - Preview Track (Preview Version).flac", filename);
        Assert.Equal(["Disc 02 of 10 - Work 1"], discSegments);
    }

    [Fact]
    public void RenderPlaylistDestination_UsesDefaultPlaylistFolderTemplate()
    {
        var path = PathTemplateRenderer.RenderPlaylistDestination(
            @"D:\Sort",
            null,
            "11932795",
            "Road: Trip",
            "Example Owner",
            track: null,
            album: null,
            albumArtist: string.Empty,
            albumTitle: string.Empty,
            quality: "FLAC 24/96",
            playlistNumber: 0,
            playlistTotalTracks: 25);

        Assert.Equal(Path.Combine(@"D:\Sort", "Playlists", "Road Trip"), path);
    }

    [Fact]
    public void RenderPlaylistDestination_FallsBackForBlankPlaylistTitleAndOwner()
    {
        var path = PathTemplateRenderer.RenderPlaylistDestination(
            @"D:\Sort",
            @"{PlaylistOwner}\{PlaylistTitle}",
            "11932795",
            "<iframe src='bad'></iframe>",
            "",
            track: null,
            album: null,
            albumArtist: string.Empty,
            albumTitle: string.Empty,
            quality: "FLAC 24/96",
            playlistNumber: 0,
            playlistTotalTracks: 25);

        Assert.Equal(Path.Combine(@"D:\Sort", "Unknown Owner", "Playlist 11932795"), path);
    }

    [Fact]
    public void RenderPlaylistAudioFilename_RendersPlaylistFieldsAndAlbumTrackFields()
    {
        var album = CreateAlbum(title: "The London Sessions", artist: "Tiesto", tracksCount: 13);
        var track = CreateTrack(album, trackNumber: 10, title: "Lose You");

        var filename = PathTemplateRenderer.RenderPlaylistAudioFilename(
            "{PlaylistNumberPadded} - {TrackArtist} - {TrackTitle} - {AlbumTitle} - Track {TrackNumberPadded} of {PlaylistTotalTracks}",
            track,
            album,
            "Tiesto",
            "The London Sessions",
            "FLAC 24/96",
            "11932795",
            "Tiesto : les indispensables",
            "Xouma31",
            playlistNumber: 7,
            playlistTotalTracks: 1943,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("0007 - Tiesto - Lose You - The London Sessions - Track 10 of 1943.flac", filename);
    }

    [Fact]
    public void RenderPlaylistAudioFilename_UsesDefaultTemplateAndRemovesEmptyVersionParentheses()
    {
        var album = CreateAlbum(title: "The Motto", artist: "Tiesto", tracksCount: 1);
        var track = CreateTrack(album, title: "The Motto");

        var filename = PathTemplateRenderer.RenderPlaylistAudioFilename(
            null,
            track,
            album,
            "Tiesto",
            "The Motto",
            "FLAC 24/96",
            "11932795",
            "Tiesto : les indispensables",
            "Xouma31",
            playlistNumber: 2,
            playlistTotalTracks: 22,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("02 - Tiesto - The Motto.flac", filename);
    }

    [Fact]
    public void RenderPlaylistAudioFilename_RendersPaddedPlaylistAndAlbumTotals()
    {
        var album = CreateAlbum(title: "Nine Track Album", tracksCount: 9);
        var track = CreateTrack(album, trackNumber: 3, title: "Third Track");

        var filename = PathTemplateRenderer.RenderPlaylistAudioFilename(
            "{PlaylistNumberPadded}-{PlaylistTotalTracksPadded} - {TrackNumberPadded}-{TotalTracksPadded}",
            track,
            album,
            "Example Artist",
            "Nine Track Album",
            "FLAC 24/96",
            "11932795",
            "Road Trip",
            "MusicEnjoyer",
            playlistNumber: 1,
            playlistTotalTracks: 9,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("01-09 - 03-09.flac", filename);
    }

    private static Album CreateAlbum(
        string title = "Example Album",
        string artist = "Example Artist",
        string version = "",
        string quality = "FLAC 24/96",
        int tracksCount = 12,
        int mediaCount = 1)
    {
        return new Album
        {
            Id = "1",
            Title = title,
            Artist = new Artist { Name = artist },
            Version = version,
            MaximumBitDepth = quality.Contains("24/", StringComparison.OrdinalIgnoreCase) ? 24 : 16,
            MaximumSamplingRate = quality.Contains("/96", StringComparison.OrdinalIgnoreCase) ? 96 : 44.1,
            ReleaseDateOriginal = DateTime.Parse("2026-05-10"),
            ReleaseType = "album",
            Label = new Label { Name = "Example Label" },
            Upc = "1234567890123",
            TracksCount = tracksCount,
            MediaCount = mediaCount
        };
    }

    private static Track CreateTrack(
        Album album,
        int trackNumber = 1,
        int mediaNumber = 1,
        string title = "Example Track",
        string version = "",
        string work = "",
        string composer = "")
    {
        return new Track
        {
            Id = trackNumber,
            Title = title,
            Version = version,
            TrackNumber = trackNumber,
            MediaNumber = mediaNumber,
            Album = album,
            Performer = new Artist { Name = album.Artist?.Name ?? "Example Artist" },
            Isrc = $"USPR326000{trackNumber:00}",
            Work = work,
            Composer = string.IsNullOrWhiteSpace(composer) ? null : new Artist { Name = composer }
        };
    }
}
