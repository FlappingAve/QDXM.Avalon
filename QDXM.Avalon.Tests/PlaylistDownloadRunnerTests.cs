using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;
using System.Net.Http;

namespace QDXM.Avalon.Tests;

public sealed class PlaylistDownloadRunnerTests
{
    [Fact]
    public async Task RunPlaylistAsync_ContinuesAfterTrackFailureAndCompletesWithWarnings()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var tracks = new[]
        {
            CreateTrack(id: 1, title: "First", playlistPosition: 1),
            CreateTrack(id: 2, title: "Second", playlistPosition: 2),
            CreateTrack(id: 3, title: "Third", playlistPosition: 3)
        };
        var coverSaveFlags = new List<bool>();
        var attemptedPlaylistPositions = new List<int>();
        var services = CreatePlaylistRunnerServices(
            CreatePlaylistPage(total: 3, tracks),
            coverSaveFlags,
            (track, _, _, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, _) =>
            {
                attemptedPlaylistPositions.Add(displayTrackNumber);
                return displayTrackNumber == 2
                    ? FailedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, "simulated failure")
                    : CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, ResolveTestFilePath(resolvedFilePathFactory));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunPlaylistAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://open.qobuz.com/playlist/11932795",
                ContentId = "11932795",
                Type = DownloadContentType.Playlist
            },
            "11932795",
            settings,
            services,
            CancellationToken.None));

        var resolved = Assert.IsType<DownloadResolvedEvent>(events[0]);
        Assert.Equal(DownloadContentType.Playlist, resolved.Type);
        Assert.NotNull(resolved.FilePaths);
        Assert.Equal(2, resolved.FilePaths!.Count);
        Assert.Equal(1, resolved.DestinationPreviewRemainingCount);
        Assert.Equal([1, 2, 3], attemptedPlaylistPositions);
        Assert.Equal([true, false, false, false], coverSaveFlags);

        var failedTrack = Assert.Single(events.OfType<PlaylistTrackFailedEvent>());
        Assert.Equal(2, failedTrack.PlaylistPosition);
        Assert.Contains("simulated failure", failedTrack.Message, StringComparison.OrdinalIgnoreCase);

        var warning = Assert.Single(events.OfType<DownloadWarningEvent>());
        Assert.Equal("1 playlist tracks failed. See Logs.", warning.Message);
        Assert.True(Assert.IsType<DownloadCompletedEvent>(events.Last()).HasWarnings);
    }

    [Fact]
    public async Task RunPlaylistAsync_ContinuesAfterTrackDownloadThrows()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var tracks = new[]
        {
            CreateTrack(id: 1, title: "First", playlistPosition: 1),
            CreateTrack(id: 2, title: "Good Times", playlistPosition: 2),
            CreateTrack(id: 3, title: "Third", playlistPosition: 3)
        };
        var attemptedPlaylistPositions = new List<int>();
        var services = CreatePlaylistRunnerServices(
            CreatePlaylistPage(total: 3, tracks),
            [],
            (track, _, _, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, _) =>
            {
                attemptedPlaylistPositions.Add(displayTrackNumber);
                return displayTrackNumber == 2
                    ? ThrowingTrackEvents(new HttpRequestException("The response ended prematurely."))
                    : CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, ResolveTestFilePath(resolvedFilePathFactory));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunPlaylistAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://play.qobuz.com/playlist/1648634",
                ContentId = "1648634",
                Type = DownloadContentType.Playlist
            },
            "1648634",
            settings,
            services,
            CancellationToken.None));

        Assert.Equal([1, 2, 3], attemptedPlaylistPositions);
        Assert.Equal(2, events.OfType<TrackCompletedEvent>().Count());
        var failedTrack = Assert.Single(events.OfType<PlaylistTrackFailedEvent>());
        Assert.Equal(2, failedTrack.PlaylistPosition);
        Assert.Contains("response ended prematurely", failedTrack.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(Assert.IsType<DownloadCompletedEvent>(events.Last()).HasWarnings);
    }

    [Fact]
    public async Task RunPlaylistAsync_RetryUsesOnlyStoredFailedPlaylistPositions()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var tracks = new[]
        {
            CreateTrack(id: 1, title: "First", playlistPosition: 1),
            CreateTrack(id: 2, title: "Second", playlistPosition: 2),
            CreateTrack(id: 3, title: "Third", playlistPosition: 3),
            CreateTrack(id: 4, title: "Fourth", playlistPosition: 4)
        };
        var attemptedPlaylistPositions = new List<int>();
        var services = CreatePlaylistRunnerServices(
            CreatePlaylistPage(total: 4, tracks),
            [],
            (track, _, _, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, _) =>
            {
                attemptedPlaylistPositions.Add(displayTrackNumber);
                return CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, ResolveTestFilePath(resolvedFilePathFactory));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunPlaylistAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://open.qobuz.com/playlist/11932795",
                ContentId = "11932795",
                Type = DownloadContentType.Playlist,
                CompletedTracks = 4,
                FailedPlaylistPositions = [2, 4]
            },
            "11932795",
            settings,
            services,
            CancellationToken.None));

        Assert.Equal([2, 4], attemptedPlaylistPositions);
        Assert.Empty(events.OfType<PlaylistTrackFailedEvent>());
        Assert.Empty(events.OfType<DownloadWarningEvent>());
        Assert.False(Assert.IsType<DownloadCompletedEvent>(events.Last()).HasWarnings);
    }

    [Fact]
    public async Task RunPlaylistAsync_SelectedPlaylistTracksPreserveSelectedOrderAndRenumberScope()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var tracks = new[]
        {
            CreateTrack(id: 1, title: "First", playlistPosition: 1),
            CreateTrack(id: 2, title: "Second", playlistPosition: 2),
            CreateTrack(id: 3, title: "Third", playlistPosition: 3)
        };
        tracks[1].PlaylistTrackId = 2002;
        tracks[2].PlaylistTrackId = 2003;

        var attemptedTrackTitles = new List<string?>();
        var attemptedPlaylistPositions = new List<int>();
        var displayTotals = new List<int>();
        var services = CreatePlaylistRunnerServices(
            CreatePlaylistPage(total: 3, tracks),
            [],
            (track, _, _, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, _) =>
            {
                attemptedTrackTitles.Add(track.Title);
                attemptedPlaylistPositions.Add(displayTrackNumber);
                displayTotals.Add(displayTotalTracks);
                return CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, ResolveTestFilePath(resolvedFilePathFactory));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunPlaylistAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://open.qobuz.com/playlist/11932795",
                ContentId = "11932795",
                Type = DownloadContentType.Playlist,
                SelectedTrackIds = ["playlist-track:2003", "playlist-track:2002"]
            },
            "11932795",
            settings,
            services,
            CancellationToken.None));

        Assert.Equal(["Third", "Second"], attemptedTrackTitles);
        Assert.Equal([1, 2], attemptedPlaylistPositions);
        Assert.Equal([2, 2], displayTotals);

        var resolved = Assert.IsType<DownloadResolvedEvent>(events[0]);
        Assert.Equal(2, resolved.TotalTracks);
        Assert.Equal(2, resolved.FilePaths?.Count);
        Assert.Contains(@"\01 - Example Artist - Third.flac", resolved.FilePaths![0], StringComparison.Ordinal);
        Assert.Contains(@"\02 - Example Artist - Second.flac", resolved.FilePaths![1], StringComparison.Ordinal);
        Assert.Equal(0, resolved.DestinationPreviewRemainingCount);
    }

    [Fact]
    public async Task RunPlaylistAsync_PassesAlbumTotalForTrackContextAndPlaylistTotalForDisplay()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var album = CreateAlbum("Nine Track Album", "Example Artist", tracksCount: 9);
        var tracks = new[]
        {
            CreateTrack(album, id: 1, title: "First", trackNumber: 1, playlistPosition: 1),
            CreateTrack(album, id: 2, title: "Second", trackNumber: 2, playlistPosition: 2),
            CreateTrack(album, id: 3, title: "Third", trackNumber: 3, playlistPosition: 3)
        };
        var receivedTrackTotals = new List<int>();
        var receivedDisplayTotals = new List<int>();
        var services = CreatePlaylistRunnerServices(
            CreatePlaylistPage(total: 3, tracks),
            [],
            (track, _, _, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, _) =>
            {
                receivedTrackTotals.Add(totalTracks);
                receivedDisplayTotals.Add(displayTotalTracks);
                return CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, ResolveTestFilePath(resolvedFilePathFactory));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunPlaylistAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://open.qobuz.com/playlist/11932795",
                ContentId = "11932795",
                Type = DownloadContentType.Playlist
            },
            "11932795",
            settings,
            services,
            CancellationToken.None));

        var completedEvents = events.OfType<TrackCompletedEvent>().ToList();
        Assert.Equal([9, 9, 9], receivedTrackTotals);
        Assert.Equal([3, 3, 3], receivedDisplayTotals);
        Assert.All(completedEvents, completed => Assert.Equal(3, completed.TotalTracks));
    }

    [Fact]
    public async Task RunPlaylistAsync_FallsBackToFirstTrackCoverAndUsesTrackCoversForEmbeddedArt()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var firstAlbum = CreateAlbum("First Album", "Example Artist", tracksCount: 1);
        firstAlbum.Image = new Image { Large = "https://img.example/first-album.jpg" };
        var secondAlbum = CreateAlbum("Second Album", "Example Artist", tracksCount: 1);
        secondAlbum.Image = new Image { Large = "https://img.example/second-album.jpg" };
        var tracks = new[]
        {
            CreateTrack(firstAlbum, id: 1, title: "First", playlistPosition: 1),
            CreateTrack(secondAlbum, id: 2, title: "Second", playlistPosition: 2)
        };
        var coverRequests = new List<(string? Url, bool SaveFolderCover, string FileName)>();
        var services = new QobuzDownloadJobRunner.PlaylistRunnerServices(
            _ => (CreatePlaylistPage(total: 2, tracks), tracks),
            (_, coverArtUrl, _, _, saveFolderCover, folderCoverFileName) =>
            {
                coverRequests.Add((coverArtUrl, saveFolderCover, folderCoverFileName));
                return Task.FromResult(new CoverArtDownloadResult(null));
            },
            _ => { },
            () => { },
            (track, _, _, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, _) =>
                CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, ResolveTestFilePath(resolvedFilePathFactory)));

        var events = await CollectEvents(QobuzDownloadJobRunner.RunPlaylistAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://open.qobuz.com/playlist/11932795",
                ContentId = "11932795",
                Type = DownloadContentType.Playlist
            },
            "11932795",
            settings,
            services,
            CancellationToken.None));

        var resolved = Assert.IsType<DownloadResolvedEvent>(events[0]);
        Assert.Equal("https://img.example/first-album.jpg", resolved.CoverArtUrl);
        Assert.Equal(
            [
                ("https://img.example/first-album.jpg", true, QobuzDownloadJobRunner.PlaylistFolderCoverFileName),
                ("https://img.example/first-album.jpg", false, "Cover.jpg"),
                ("https://img.example/second-album.jpg", false, "Cover.jpg")
            ],
            coverRequests);
    }

    [Fact]
    public void DownloadStatus_IssuesDoesNotShiftExistingPersistedStatusValues()
    {
        Assert.Equal(5, (int)DownloadStatus.Failed);
        Assert.Equal(7, (int)DownloadStatus.Skipped);
        Assert.Equal(8, (int)DownloadStatus.Issues);
    }

    [Fact]
    public void GetPlaylistNumber_PrefersExplicitPlaylistPosition()
    {
        var track = new Track { PlaylistPosition = 42 };

        var playlistNumber = QobuzDownloadJobRunner.GetPlaylistNumber(track, returnedIndex: 0);

        Assert.Equal(42, playlistNumber);
    }

    [Fact]
    public void GetPlaylistNumber_FallsBackToReturnedOrder()
    {
        var track = new Track();

        var playlistNumber = QobuzDownloadJobRunner.GetPlaylistNumber(track, returnedIndex: 4);

        Assert.Equal(5, playlistNumber);
    }

    [Fact]
    public void GetPlaylistTrackFilePath_UsesPlaylistPositionAndPlaylistTotal()
    {
        var settings = CreateSettings();
        settings.PlaylistFilenameTemplate = "{PlaylistNumberPadded}-{PlaylistTotalTracksPadded} - {TrackNumberPadded}-{TotalTracksPadded} - {TrackTitle}";
        var album = CreateAlbum("Nine Track Album", "Example Artist", tracksCount: 9);
        var track = CreateTrack(album, title: "First Track", trackNumber: 1, playlistPosition: 1);

        var path = QobuzDownloadJobRunner.GetPlaylistTrackFilePath(
            Path.Combine(settings.EffectiveDownloadFolder, "Playlists", "Road Trip"),
            track,
            "11932795",
            "Road Trip",
            "MusicEnjoyer",
            playlistNumber: 1,
            playlistTotalTracks: 1900,
            settings);

        Assert.Equal(
            Path.Combine(TestPaths.DownloadRoot, "Playlists", "Road Trip", "0001-1900 - 01-09 - First Track.flac"),
            path);
    }

    [Fact]
    public void GetPlaylistTrackFilePath_CanUseResolvedQualityAndExtension()
    {
        var settings = CreateSettings();
        settings.PlaylistFilenameTemplate = "{PlaylistNumberPadded} - {TrackTitle} [{Quality}]";
        var album = CreateAlbum("Nine Track Album", "Example Artist", tracksCount: 9);
        var track = CreateTrack(album, title: "First Track", trackNumber: 1, playlistPosition: 1);

        var path = QobuzDownloadJobRunner.GetPlaylistTrackFilePath(
            Path.Combine(settings.EffectiveDownloadFolder, "Playlists", "Road Trip"),
            track,
            "11932795",
            "Road Trip",
            "MusicEnjoyer",
            playlistNumber: 1,
            playlistTotalTracks: 1900,
            settings,
            trackQualityOverride: "MP3 320",
            extensionOverride: ".mp3");

        Assert.Equal(
            Path.Combine(TestPaths.DownloadRoot, "Playlists", "Road Trip", "0001 - First Track [MP3 (320kbps)].mp3"),
            path);
    }

    [Fact]
    public void GetStandardPlaylistTrackFilePath_UsesAlbumNumberingAndAlbumTotal()
    {
        var settings = CreateSettings();
        settings.PlaylistOrganization = AppSettings.UseStandardTemplatesPlaylistOrganization;
        settings.FolderTemplate = @"{AlbumArtist}\{AlbumTitle}";
        settings.FilenameTemplate = "{TrackNumberPadded}-{TotalTracksPadded} - {TrackTitle}";
        settings.DiscFolderTemplate = string.Empty;
        var album = CreateAlbum("Nine Track Album", "Example Artist", tracksCount: 9);
        var track = CreateTrack(album, title: "First Track", trackNumber: 1, playlistPosition: 183);

        var path = QobuzDownloadJobRunner.GetStandardPlaylistTrackFilePath(track, settings);

        Assert.Equal(
            Path.Combine(TestPaths.DownloadRoot, "Example Artist", "Nine Track Album", "01-09 - First Track.flac"),
            path);
    }

    [Fact]
    public void GetStandardPlaylistTrackFilePath_UsesStandardDiscFolderTemplate()
    {
        var settings = CreateSettings();
        settings.PlaylistOrganization = AppSettings.UseStandardTemplatesPlaylistOrganization;
        settings.FolderTemplate = @"{AlbumArtist}\{AlbumTitle}";
        settings.FilenameTemplate = "{TrackNumberPadded} - {TrackTitle}";
        settings.DiscFolderTemplate = "Disc {DiscNumberPadded}";
        var album = CreateAlbum("Double Album", "Example Artist", tracksCount: 20, mediaCount: 2);
        var track = CreateTrack(album, title: "Disc Two Track", trackNumber: 3, mediaNumber: 2, playlistPosition: 99);

        var path = QobuzDownloadJobRunner.GetStandardPlaylistTrackFilePath(track, settings);

        Assert.Equal(
            Path.Combine(TestPaths.DownloadRoot, "Example Artist", "Double Album", "Disc 02", "03 - Disc Two Track.flac"),
            path);
    }

    [Fact]
    public void GetStandardPlaylistTrackFilePath_UsesPerformerWhenAlbumArtistIsMissing()
    {
        var settings = CreateSettings();
        settings.PlaylistOrganization = AppSettings.UseStandardTemplatesPlaylistOrganization;
        settings.FolderTemplate = @"{AlbumArtist}\{AlbumTitle}";
        settings.FilenameTemplate = "{TrackArtist} - {TrackTitle}";
        settings.DiscFolderTemplate = string.Empty;
        var album = CreateAlbum("Loose Single", artist: string.Empty, tracksCount: 1);
        album.Artist = null;
        var track = CreateTrack(album, title: "Fallback Artist Track", playlistPosition: 1);
        track.Performer = new Artist { Name = "Performer Fallback" };

        var path = QobuzDownloadJobRunner.GetStandardPlaylistTrackFilePath(track, settings);

        Assert.Equal(
            Path.Combine(TestPaths.DownloadRoot, "Performer Fallback", "Loose Single", "Performer Fallback - Fallback Artist Track.flac"),
            path);
    }

    [Fact]
    public void ShouldSaveStandardFolderCover_IsFalseWhenStandardFolderTemplateIsBlank()
    {
        var settings = CreateSettings();
        settings.FolderTemplate = " ";

        Assert.False(QobuzDownloadJobRunner.ShouldSaveStandardFolderCover(settings));
    }

    [Fact]
    public void FirstNonEmptyPlaylistImage_PrefersPlaylistRectangleBeforeFallbackLists()
    {
        var playlist = new Playlist
        {
            Images = ["https://img.example/primary.jpg"],
            Images300 = ["https://img.example/300.jpg"],
            ImageRectangle = ["https://img.example/rectangle.jpg"]
        };

        var image = QobuzDownloadJobRunner.FirstNonEmptyPlaylistImage(playlist);

        Assert.Equal("https://img.example/rectangle.jpg", image);
    }

    [Fact]
    public void GetDownloadCandidateFormatIds_TriesFlacOnlyWhenMp3FallbackIsDisabled()
    {
        var settings = new AppSettings
        {
            FormatId = QualityStringMappings.FlacHighestFormatId,
            FallbackToMp3IfFlacUnavailable = false
        };

        Assert.Equal(["27", "7", "6"], QobuzDownloadJobRunner.GetDownloadCandidateFormatIds(settings));
    }

    [Fact]
    public void GetDownloadCandidateFormatIds_AddsMp3WhenFlacFallbackIsEnabled()
    {
        var settings = new AppSettings
        {
            FormatId = QualityStringMappings.FlacHighestFormatId,
            FallbackToMp3IfFlacUnavailable = true
        };

        Assert.Equal(["27", "7", "6", "5"], QobuzDownloadJobRunner.GetDownloadCandidateFormatIds(settings));
    }

    [Fact]
    public void GetDownloadCandidateFormatIds_UsesOnlyMp3WhenMp3IsSelected()
    {
        var settings = new AppSettings
        {
            FormatId = QualityStringMappings.Mp3FormatId,
            FallbackToMp3IfFlacUnavailable = true
        };

        Assert.Equal(["5"], QobuzDownloadJobRunner.GetDownloadCandidateFormatIds(settings));
    }

    [Fact]
    public void GetQualityFallbackWarningMessage_UsesActualReturnedFlacQuality()
    {
        var stream = new QobuzDownloadJobRunner.ResolvedDownloadStream(
            Url: "https://cdn.example/track.flac",
            Quality: new AudioQualityDescriptor("7", "FLAC 24/88.2", "FLAC (24bit-88.2kHz)", ".flac"));

        var message = QobuzDownloadJobRunner.GetQualityFallbackWarningMessage("Black Magic Woman / Gypsy Queen", stream);

        Assert.Equal(
            "Black Magic Woman / Gypsy Queen quality was reduced to FLAC 24/88.2 after the requested FLAC stream failed.",
            message);
    }

    [Fact]
    public void GetQualityFallbackWarningMessage_DescribesMp3Fallback()
    {
        var stream = new QobuzDownloadJobRunner.ResolvedDownloadStream(
            Url: "https://cdn.example/track.mp3",
            Quality: new AudioQualityDescriptor("5", "MP3 320", "MP3", ".mp3"));

        var message = QobuzDownloadJobRunner.GetQualityFallbackWarningMessage("Black Magic Woman / Gypsy Queen", stream);

        Assert.Equal(
            "Black Magic Woman / Gypsy Queen fell back to MP3 320 after no FLAC stream succeeded.",
            message);
    }

    [Theory]
    [InlineData(false, false, DownloadFailureKind.TrackUnavailable)]
    [InlineData(true, false, DownloadFailureKind.General)]
    [InlineData(false, true, DownloadFailureKind.General)]
    public void GetTerminalDownloadFailureKind_OnlyClassifiesCleanMissingUrlsAsUnavailable(
        bool fileUrlLookupFailedWithException,
        bool downloadWasAttempted,
        DownloadFailureKind expected)
    {
        Assert.Equal(
            expected,
            QobuzDownloadJobRunner.GetTerminalDownloadFailureKind(
                fileUrlLookupFailedWithException,
                downloadWasAttempted));
    }

    [Fact]
    public void GetUnavailableAlbumTrackWarningMessage_ExplainsSkippedAlbumOnlyTrack()
    {
        var message = QobuzDownloadJobRunner.GetUnavailableAlbumTrackWarningMessage(
            new Track { Title = "End Of An Era" });

        Assert.Contains("End Of An Era was skipped", message, StringComparison.Ordinal);
        Assert.Contains("album-only", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsLocalFileFailure_DoesNotTreatNetworkIoAsLocalPathFailure()
    {
        Assert.True(QobuzDownloadJobRunner.IsLocalFileFailure(new PathTooLongException()));
        Assert.False(QobuzDownloadJobRunner.IsLocalFileFailure(new IOException("The response ended prematurely.")));
        Assert.False(QobuzDownloadJobRunner.IsLocalFileFailure(new HttpRequestException("The response ended prematurely.")));
    }

    [Fact]
    public async Task ReadWithInactivityTimeoutAsync_ReturnsBytesWhenStreamResponds()
    {
        await using var stream = new MemoryStream([0x01, 0x02, 0x03]);
        var buffer = new byte[8];

        var bytesRead = await QobuzDownloadJobRunner.ReadWithInactivityTimeoutAsync(
            stream,
            buffer,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(3, bytesRead);
        Assert.Equal([0x01, 0x02, 0x03], buffer.Take(3).ToArray());
    }

    [Fact]
    public async Task ReadWithInactivityTimeoutAsync_ThrowsIoExceptionWhenStreamStalls()
    {
        await using var stream = new StallingStream();
        var buffer = new byte[8];

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            QobuzDownloadJobRunner.ReadWithInactivityTimeoutAsync(
                stream,
                buffer,
                TimeSpan.FromMilliseconds(10),
                CancellationToken.None).AsTask());

        Assert.Contains("No download data", exception.Message);
    }

    [Fact]
    public async Task ReadWithInactivityTimeoutAsync_PreservesUserCancellation()
    {
        await using var stream = new StallingStream();
        var buffer = new byte[8];
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            QobuzDownloadJobRunner.ReadWithInactivityTimeoutAsync(
                stream,
                buffer,
                TimeSpan.FromSeconds(1),
                cancellationTokenSource.Token).AsTask());
    }

    [Fact]
    public void GetDownloadFailureMessage_NotesRetryBeforeFinalAttempt()
    {
        var message = QobuzDownloadJobRunner.GetDownloadFailureMessage(
            "FLAC 24/96",
            "Example Track",
            new IOException("The response ended prematurely."),
            willRetry: true);

        Assert.Contains("will be retried", message);
    }

    [Fact]
    public void GetDownloadFailureMessage_DoesNotMentionRetryWhenRetryIsDisabled()
    {
        var message = QobuzDownloadJobRunner.GetDownloadFailureMessage(
            "FLAC 24/96",
            "Example Track",
            new IOException("The response ended prematurely."),
            willRetry: false);

        Assert.DoesNotContain("will be retried", message);
    }

    [Fact]
    public void FetchPlaylistTracks_PaginatesUntilReportedTotalIsReached()
    {
        var requestedOffsets = new List<int>();
        var firstPage = CreatePlaylistPage(total: 5, [CreateTrack(id: 1), CreateTrack(id: 2)]);
        var secondPage = CreatePlaylistPage(total: 5, [CreateTrack(id: 3), CreateTrack(id: 4)]);
        var thirdPage = CreatePlaylistPage(total: 5, [CreateTrack(id: 5)]);

        var result = QobuzDownloadJobRunner.FetchPlaylistTracks((limit, offset) =>
        {
            Assert.Equal(QobuzApiLimits.PlaylistTrackPageSize, limit);
            requestedOffsets.Add(offset);
            return offset switch
            {
                0 => firstPage,
                2 => secondPage,
                4 => thirdPage,
                _ => CreatePlaylistPage(total: 5, [])
            };
        });

        Assert.Equal([0, 2, 4], requestedOffsets);
        Assert.Equal([1, 2, 3, 4, 5], result.Tracks.Select(track => track.Id));
    }

    [Fact]
    public void FetchPlaylistTracks_StopsSafelyWhenApiReturnsNoMoreTracks()
    {
        var requestedOffsets = new List<int>();
        var firstPage = CreatePlaylistPage(total: 5, [CreateTrack(id: 1), CreateTrack(id: 2)]);

        var result = QobuzDownloadJobRunner.FetchPlaylistTracks((_, offset) =>
        {
            requestedOffsets.Add(offset);
            return offset == 0
                ? firstPage
                : CreatePlaylistPage(total: 5, []);
        });

        Assert.Equal([0, 2], requestedOffsets);
        Assert.Equal([1, 2], result.Tracks.Select(track => track.Id));
    }

    [Fact]
    public void FetchFavoriteIds_PaginatesUntilSelectedListIsShort()
    {
        var requestedOffsets = new List<int>();
        var firstPage = Enumerable.Range(1, QobuzApiLimits.FavoriteIdPageSize).ToList();
        var secondPage = new[] { QobuzApiLimits.FavoriteIdPageSize + 1 };

        var result = QobuzDownloadJobRunner.FetchFavoriteIds(
            (limit, offset) =>
            {
                Assert.Equal(QobuzApiLimits.FavoriteIdPageSize, limit);
                requestedOffsets.Add(offset);
                return new UserFavoritesIds
                {
                    Tracks = offset == 0 ? firstPage : secondPage.ToList()
                };
            },
            favorites => favorites.Tracks ?? []);

        Assert.Equal([0, QobuzApiLimits.FavoriteIdPageSize], requestedOffsets);
        Assert.Equal(QobuzApiLimits.FavoriteIdPageSize + 1, result.Count);
        Assert.Equal(QobuzApiLimits.FavoriteIdPageSize + 1, result.Last());
    }

    [Fact]
    public async Task RunFavoritesAsync_DownloadsFavoriteTracksAsOneQueueItem()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var album = CreateAlbum("Favorite Track Album", "Example Artist", tracksCount: 9);
        var tracks = new Dictionary<string, Track>
        {
            ["10"] = CreateTrack(album, id: 10, title: "First Favorite", trackNumber: 1),
            ["20"] = CreateTrack(album, id: 20, title: "Second Favorite", trackNumber: 2)
        };
        var attemptedDisplayNumbers = new List<int>();
        var services = CreateFavoritesRunnerServices(
            favoriteTrackIds: [10, 20],
            tracks: tracks,
            downloadTrack: (track, _, _, completedTrackNumber, totalTracks, _, displayTrackNumber, displayTotalTracks, _) =>
            {
                attemptedDisplayNumbers.Add(displayTrackNumber);
                return CompletedTrackEvents(
                    "queue-1",
                    displayTrackNumber,
                    displayTotalTracks,
                    track.Title,
                    completedTrackNumber,
                    totalTracks,
                    Path.Combine(TestPaths.DownloadRoot, "Favorite.flac"));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunFavoritesAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://play.qobuz.com/user/library/favorites/tracks",
                ContentId = "tracks",
                Type = DownloadContentType.Favorites
            },
            "tracks",
            settings,
            services,
            CancellationToken.None));

        var resolved = Assert.IsType<DownloadResolvedEvent>(events[0]);
        Assert.Equal(DownloadContentType.Favorites, resolved.Type);
        Assert.Equal("Favorite Tracks", resolved.Title);
        Assert.Equal(2, resolved.TotalTracks);
        Assert.Equal(2, resolved.FilePaths?.Count);
        Assert.Equal([1, 2], attemptedDisplayNumbers);
        Assert.False(Assert.IsType<DownloadCompletedEvent>(events.Last()).HasWarnings);
    }

    [Fact]
    public async Task RunFavoritesAsync_ContinuesAfterFavoriteAlbumTrackFailure()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settings = CreateSettings(workspace.CreateDirectory("Sort"));
        var album = CreateAlbum("Favorite Album", "Example Artist", tracksCount: 2);
        album.Tracks = new ItemSearchResult<Track>
        {
            Total = 2,
            Items =
            [
                CreateTrack(album, id: 10, title: "First", trackNumber: 1),
                CreateTrack(album, id: 20, title: "Second", trackNumber: 2)
            ]
        };
        var albums = new Dictionary<string, Album> { ["album-1"] = album };
        var attemptedDisplayNumbers = new List<int>();
        var services = CreateFavoritesRunnerServices(
            favoriteAlbumIds: ["album-1"],
            albums: albums,
            downloadTrack: (track, _, _, completedTrackNumber, totalTracks, _, displayTrackNumber, displayTotalTracks, _) =>
            {
                attemptedDisplayNumbers.Add(displayTrackNumber);
                return displayTrackNumber == 1
                    ? FailedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, "simulated failure")
                    : CompletedTrackEvents("queue-1", displayTrackNumber, displayTotalTracks, track.Title, completedTrackNumber, totalTracks, Path.Combine(TestPaths.DownloadRoot, "Second.flac"));
            });

        var events = await CollectEvents(QobuzDownloadJobRunner.RunFavoritesAsync(
            new DownloadQueueItem
            {
                Id = "queue-1",
                SourceUrl = "https://play.qobuz.com/user/library/favorites/albums",
                ContentId = "albums",
                Type = DownloadContentType.Favorites
            },
            "albums",
            settings,
            services,
            CancellationToken.None));

        Assert.Equal([1, 2], attemptedDisplayNumbers);
        Assert.Contains(events.OfType<DownloadWarningEvent>(), warning =>
            warning.Message.Contains("Favorite album track 1 failed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(events.OfType<DownloadWarningEvent>(), warning =>
            warning.Message.Contains("1 favorite album tracks failed", StringComparison.OrdinalIgnoreCase));
        Assert.True(Assert.IsType<DownloadCompletedEvent>(events.Last()).HasWarnings);
    }

    private static AppSettings CreateSettings(string? downloadFolder = null)
    {
        return new AppSettings
        {
            DownloadFolder = downloadFolder ?? TestPaths.DownloadRoot,
            FormatId = QualityStringMappings.FlacHighestFormatId,
            SelectedQuality = QualityStringMappings.FlacHighestLabel,
            MaxFileNameLength = 150
        };
    }

    private static Playlist CreatePlaylistPage(int total, IReadOnlyList<Track> tracks)
    {
        return new Playlist
        {
            Name = "Road Trip",
            Owner = new Owner { Name = "MusicEnjoyer" },
            TracksCount = total,
            Tracks = new ItemSearchResult<Track>
            {
                Total = total,
                Items = tracks.ToList()
            }
        };
    }

    private static QobuzDownloadJobRunner.PlaylistRunnerServices CreatePlaylistRunnerServices(
        Playlist playlist,
        List<bool> coverSaveFlags,
        QobuzDownloadJobRunner.PlaylistTrackDownload downloadTrack)
    {
        return new QobuzDownloadJobRunner.PlaylistRunnerServices(
            _ => (playlist, playlist.Tracks?.Items ?? []),
            (_, _, _, _, saveFolderCover, _) =>
            {
                coverSaveFlags.Add(saveFolderCover);
                return Task.FromResult(new CoverArtDownloadResult(null));
            },
            _ => { },
            () => { },
            downloadTrack);
    }

    private static QobuzDownloadJobRunner.FavoritesRunnerServices CreateFavoritesRunnerServices(
        IReadOnlyList<string>? favoriteAlbumIds = null,
        IReadOnlyList<int>? favoriteTrackIds = null,
        IReadOnlyDictionary<string, Album>? albums = null,
        IReadOnlyDictionary<string, Track>? tracks = null,
        QobuzDownloadJobRunner.FavoriteTrackDownload? downloadTrack = null)
    {
        return new QobuzDownloadJobRunner.FavoritesRunnerServices(
            () => favoriteAlbumIds ?? [],
            () => favoriteTrackIds ?? [],
            albumId => albums is not null && albums.TryGetValue(albumId, out var album)
                ? album
                : throw new InvalidOperationException($"Album {albumId} was not registered for the test."),
            trackId => tracks is not null && tracks.TryGetValue(trackId, out var track)
                ? track
                : throw new InvalidOperationException($"Track {trackId} was not registered for the test."),
            (_, _, _, _, _, _) => Task.FromResult(new CoverArtDownloadResult(null)),
            (_, _, _) => Task.FromResult(true),
            _ => { },
            () => { },
            downloadTrack ?? ((track, _, _, completedTrackNumber, totalTracks, _, displayTrackNumber, displayTotalTracks, _) =>
                CompletedTrackEvents(
                    "queue-1",
                    displayTrackNumber,
                    displayTotalTracks,
                    track.Title,
                    completedTrackNumber,
                    totalTracks,
                    Path.Combine(TestPaths.DownloadRoot, "Test.flac"))));
    }

    private static string ResolveTestFilePath(Func<QobuzDownloadJobRunner.ResolvedDownloadStream, string>? resolvedFilePathFactory)
    {
        return resolvedFilePathFactory?.Invoke(CreateTestStream()) ?? Path.Combine(TestPaths.DownloadRoot, "Test Track.flac");
    }

    private static QobuzDownloadJobRunner.ResolvedDownloadStream CreateTestStream()
    {
        return new QobuzDownloadJobRunner.ResolvedDownloadStream(
            "https://cdn.example/test.flac",
            new AudioQualityDescriptor("7", "FLAC 24/96", "FLAC (24bit-96kHz)", ".flac"));
    }

    private static async IAsyncEnumerable<DownloadEvent> CompletedTrackEvents(
        string queueItemId,
        int displayTrackNumber,
        int displayTotalTracks,
        string? title,
        int completedTrackNumber,
        int totalTracks,
        string filePath)
    {
        await Task.Yield();
        yield return new TrackStartedEvent(queueItemId, displayTrackNumber, displayTotalTracks, title ?? "Untitled");
        yield return new TrackCompletedEvent(queueItemId, completedTrackNumber, displayTotalTracks, filePath, FileSizeBytes: 100);
    }

    private static async IAsyncEnumerable<DownloadEvent> FailedTrackEvents(
        string queueItemId,
        int displayTrackNumber,
        int displayTotalTracks,
        string? title,
        string message)
    {
        await Task.Yield();
        yield return new TrackStartedEvent(queueItemId, displayTrackNumber, displayTotalTracks, title ?? "Untitled");
        yield return new DownloadFailedEvent(queueItemId, message);
    }

    private static async IAsyncEnumerable<DownloadEvent> ThrowingTrackEvents(Exception exception)
    {
        await Task.Yield();
        throw exception;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async Task<List<DownloadEvent>> CollectEvents(IAsyncEnumerable<DownloadEvent> events)
    {
        var collected = new List<DownloadEvent>();
        await foreach (var downloadEvent in events)
        {
            collected.Add(downloadEvent);
        }

        return collected;
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    private static Album CreateAlbum(
        string title = "Example Album",
        string artist = "Example Artist",
        int tracksCount = 12,
        int mediaCount = 1)
    {
        return new Album
        {
            Id = "1",
            Title = title,
            Artist = new Artist { Name = artist },
            TracksCount = tracksCount,
            MediaCount = mediaCount,
            MaximumBitDepth = 24,
            MaximumSamplingRate = 96,
            Image = new Image { Large = "https://img.example/album.jpg" }
        };
    }

    private static Track CreateTrack(
        Album? album = null,
        string title = "Example Track",
        int id = 1,
        int trackNumber = 1,
        int mediaNumber = 1,
        int? playlistPosition = null)
    {
        return new Track
        {
            Id = id,
            Title = title,
            Album = album ?? CreateAlbum(),
            Performer = new Artist { Name = album?.Artist?.Name ?? "Example Artist" },
            TrackNumber = trackNumber,
            MediaNumber = mediaNumber,
            PlaylistPosition = playlistPosition
        };
    }
}
