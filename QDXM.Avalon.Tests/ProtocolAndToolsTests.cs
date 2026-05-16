using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Protocol;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Tests;

public sealed class ProtocolAndToolsTests
{
    [Fact]
    public void ProtocolHandler_ConvertsProtocolUrlToOpenQobuzUrl()
    {
        var converted = ProtocolHandler.ConvertProtocolUrl("QDXMA://album/abc123");

        Assert.Equal("https://open.qobuz.com/album/abc123", converted);
    }

    [Fact]
    public void ProtocolUrlQueue_ProcessesMultipleQueuedUrls()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        using var queue = new ProtocolUrlQueue(queuePath);
        var urls = new List<string>();
        queue.UrlReceived += urls.Add;

        queue.AddToQueue("qdxm://album/1");
        queue.AddToQueue("qdxm://track/2");
        queue.Initialize();

        Assert.Equal(["qdxm://album/1", "qdxm://track/2"], urls);
    }

    [Fact]
    public void ProtocolUrlQueue_ProcessesLargeBatchOfQueuedUrls()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        using var queue = new ProtocolUrlQueue(queuePath);
        var urls = new List<string>();
        queue.UrlReceived += urls.Add;
        var expectedUrls = Enumerable
            .Range(1, 100)
            .Select(index => $"qdxm://album/{index}")
            .ToList();

        foreach (var url in expectedUrls)
        {
            queue.AddToQueue(url);
        }

        queue.Initialize();

        Assert.Equal(expectedUrls, urls);
    }

    [Fact]
    public void ProtocolUrlQueue_DoesNotLoseUrlQueuedWhileProcessing()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        using var queue = new ProtocolUrlQueue(queuePath);
        var urls = new List<string>();
        queue.UrlReceived += url =>
        {
            urls.Add(url);
            if (url == "qdxm://album/1")
            {
                queue.AddToQueue("qdxm://track/2");
            }
        };

        queue.AddToQueue("qdxm://album/1");
        queue.Initialize();

        Assert.Equal(["qdxm://album/1", "qdxm://track/2"], urls);
    }

    [Fact]
    public void ProtocolUrlQueue_IgnoresEmptyQueueFiles()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        var pendingDirectory = Path.Combine(queuePath, "pending");
        Directory.CreateDirectory(pendingDirectory);
        File.WriteAllText(Path.Combine(pendingDirectory, "empty.url"), "   ");
        using var queue = new ProtocolUrlQueue(queuePath);
        var urls = new List<string>();
        queue.UrlReceived += urls.Add;

        queue.Initialize();

        Assert.Empty(urls);
    }

    [Fact]
    public void ProtocolUrlQueue_RecoversClaimedUrlOnStartup()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        var inProgressDirectory = Path.Combine(queuePath, "in-progress");
        Directory.CreateDirectory(inProgressDirectory);
        File.WriteAllText(Path.Combine(inProgressDirectory, "claimed.url"), "qdxm://album/1");
        using var queue = new ProtocolUrlQueue(queuePath);
        var urls = new List<string>();
        queue.UrlReceived += urls.Add;

        queue.Initialize();

        Assert.Equal(["qdxm://album/1"], urls);
    }

    [Fact]
    public void ProtocolUrlQueue_RetriesUrlAfterTransientReadFailure()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        var readAttempts = 0;
        using var queue = new ProtocolUrlQueue(queuePath, path =>
        {
            readAttempts++;
            if (readAttempts == 1)
            {
                throw new IOException("File is temporarily unavailable.");
            }

            return File.ReadAllText(path);
        });
        var urls = new List<string>();
        using var received = new ManualResetEventSlim();
        queue.UrlReceived += url =>
        {
            urls.Add(url);
            received.Set();
        };

        queue.AddToQueue("qdxm://album/1");
        queue.Initialize();

        Assert.True(received.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(["qdxm://album/1"], urls);
        Assert.Equal(2, readAttempts);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(queuePath, "in-progress"), "*.url"));
    }

    [Fact]
    public void ProtocolUrlQueue_StopsRetryingAfterRepeatedReadFailures()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        var readAttempts = 0;
        using var queue = new ProtocolUrlQueue(queuePath, _ =>
        {
            readAttempts++;
            throw new IOException("File is unavailable.");
        });
        var warnings = new List<string>();
        using var warningReceived = new ManualResetEventSlim();
        queue.WarningReceived += warning =>
        {
            warnings.Add(warning);
            warningReceived.Set();
        };

        queue.AddToQueue("qdxm://album/1");
        queue.Initialize();

        Assert.True(warningReceived.Wait(TimeSpan.FromSeconds(3)));
        Assert.Single(warnings);
        Assert.Contains("could not be read", warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("failed", warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, readAttempts);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(queuePath, "in-progress"), "*.url"));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(queuePath, "failed"), "*.url"));
        Thread.Sleep(700);
        Assert.Equal(3, readAttempts);
    }

    [Fact]
    public void ProtocolUrlQueue_ProcessesQueuedWarnings()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var queuePath = workspace.CreateDirectory("protocol-queue");
        using var queue = new ProtocolUrlQueue(queuePath);
        var warnings = new List<string>();
        queue.WarningReceived += warnings.Add;

        queue.AddWarningToQueue("Import file was not found: missing.txt");
        queue.Initialize();

        Assert.Equal(["Import file was not found: missing.txt"], warnings);
    }

    [Fact]
    public void StringTools_DecodesUnicodeEscapesAndTrimsSafeFilename()
    {
        Assert.Equal("Fur Elise", StringTools.DecodeEncodedNonAsciiCharacters("Fur\\u0020Elise"));
        Assert.Equal("Name", StringTools.GetSafeFilename("Name... "));
        Assert.Equal("White Album Super Deluxe", StringTools.GetSafeFilename("White Album / Super Deluxe"));
    }

    [Fact]
    public void StringTools_AppendsTreeGlyphsWithoutMojibake()
    {
        var builder = new System.Text.StringBuilder();

        StringTools.AppendTreeLeaf(builder, [], "Folder", isLast: false);
        StringTools.AppendTreeLeaf(builder, [false], "Track.flac", isLast: true);

        var text = builder.ToString();
        Assert.Contains("\u251c\u2500 Folder", text);
        Assert.Contains("   \u2514\u2500 Track.flac", text);
        Assert.DoesNotContain("â", text);
    }

    [Fact]
    public void StringTools_AppendsTreeGlyphsWithAncestorContinuations()
    {
        var builder = new System.Text.StringBuilder();

        StringTools.AppendTreeLeaf(builder, [], "Folder", isLast: false);
        StringTools.AppendTreeLeaf(builder, [true], "Track.flac", isLast: true);

        var text = builder.ToString();
        Assert.Contains("\u251c\u2500 Folder", text);
        Assert.Contains("\u2502  \u2514\u2500 Track.flac", text);
    }

    [Fact]
    public void StringTools_SanitizesPlaylistTitleSegment()
    {
        var title = $"  Road\\Trip: <iframe src='bad'></iframe> Hits {char.ConvertFromUtf32(0x1F525)}\u0000...  ";

        var segment = StringTools.GetSafePlaylistTitleSegment(title, "11932795");

        Assert.Equal("Road Trip Hits", segment);
    }

    [Fact]
    public void StringTools_SanitizesPlaylistTitleSegmentWithFallback()
    {
        var segment = StringTools.GetSafePlaylistTitleSegment($"<iframe src='bad'></iframe> {char.ConvertFromUtf32(0x1F525)}", "11932795");

        Assert.Equal("Playlist 11932795", segment);
    }

    [Fact]
    public void StringTools_LimitsPlaylistTitleSegmentLength()
    {
        var longTitle = new string('A', 80);

        var segment = StringTools.GetSafePlaylistTitleSegment(longTitle, "11932795");

        Assert.Equal(StringTools.PlaylistTitleSegmentMaxLength, segment.Length);
        Assert.Equal(new string('A', StringTools.PlaylistTitleSegmentMaxLength), segment);
    }

    [Fact]
    public void StringTools_ReturnsNoRelativeSegmentsWhenDestinationIsBaseFolder()
    {
        Assert.Empty(StringTools.GetRelativeSegments(@"D:\Sort", @"D:\Sort"));
    }

    [Fact]
    public void QobuzTitleFormatter_KeepsAlbumTitleSeparateFromVersion()
    {
        Assert.Equal(
            "The Beatles (White Album) [Super Deluxe]",
            QobuzTitleFormatter.AlbumTitle("The Beatles (White Album) [Super Deluxe]"));
    }

    [Fact]
    public void QobuzTitleFormatter_KeepsTrackTitleSeparateFromVersion()
    {
        Assert.Equal(
            "Back In The U.S.S.R.",
            QobuzTitleFormatter.TrackTitle("Back In The U.S.S.R."));
    }

    [Fact]
    public void CoverArtUrlSelector_UsesDirectImageForKnownSizes()
    {
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg",
            Small = "https://static.qobuz.com/images/covers/ab/cd/example_230.jpg"
        };

        Assert.Equal(image.Large, CoverArtUrlSelector.GetImageUrlForSize(image, "600"));
        Assert.Equal(image.Large, CoverArtUrlSelector.GetImageUrlForSize(image, "Large"));
        Assert.Equal(image.Small, CoverArtUrlSelector.GetImageUrlForSize(image, "Small"));
        Assert.Equal(image.Small, CoverArtUrlSelector.GetImageUrlForSize(image, "230 px"));
    }

    [Fact]
    public void CoverArtUrlSelector_RewritesQobuzImageUrlForDerivedSizes()
    {
        var image = new Image
        {
            Large = "https://static.qobuz.com/images/covers/ab/cd/example_600.jpg"
        };

        Assert.Equal(
            "https://static.qobuz.com/images/covers/ab/cd/example_150.jpg",
            CoverArtUrlSelector.GetImageUrlForSize(image, "150"));
        Assert.Equal(
            "https://static.qobuz.com/images/covers/ab/cd/example_100.jpg",
            CoverArtUrlSelector.GetImageUrlForSize(image, "100"));
        Assert.Equal(
            "https://static.qobuz.com/images/covers/ab/cd/example_300.jpg",
            CoverArtUrlSelector.GetImageUrlForSize(image, "300"));
        Assert.Equal(
            "https://static.qobuz.com/images/covers/ab/cd/example_org.jpg",
            CoverArtUrlSelector.GetImageUrlForSize(image, "Original (Large File Size!)"));
        Assert.Equal(
            "https://static.qobuz.com/images/covers/ab/cd/example_max.jpg",
            CoverArtUrlSelector.GetImageUrlForSize(image, "Max (Large File Size!)"));
    }

    [Theory]
    [InlineData("600", "600 px (Recommended)")]
    [InlineData("600 px (Recommended)", "600 px (Recommended)")]
    [InlineData("300", "300 px")]
    [InlineData("230", "230 px")]
    [InlineData("small", "230 px")]
    [InlineData("max", "Max (Large File Size!)")]
    [InlineData("org", "Original (Large File Size!)")]
    public void CoverArtUrlSelector_ReturnsArtSizeDisplayName(string artSize, string expectedDisplayName)
    {
        Assert.Equal(expectedDisplayName, CoverArtUrlSelector.GetArtSizeDisplayName(artSize));
    }

    [Fact]
    public void PathTemplateRenderer_PreservesHyphenSpacingInFolderTemplate()
    {
        var album = new Album
        {
            Title = "Fur Elise Nightmare",
            Version = "Piano Solo",
            Artist = new Artist { Name = "MusicalBasics" },
            MaximumBitDepth = 16,
            MaximumSamplingRate = 44.1
        };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            "{AlbumArtist}-{AlbumTitle}-{Quality}",
            album,
            "MusicalBasics",
            "Fur Elise Nightmare",
            "FLAC 16/44.1");

        Assert.Equal(
            Path.Combine(@"D:\Sort", "MusicalBasics-Fur Elise Nightmare-FLAC (16bit-44.1kHz)"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_RendersSubfolderTemplateWithFriendlyFields()
    {
        var album = new Album
        {
            Title = "Example Album",
            Version = "Deluxe Edition",
            Artist = new Artist { Name = "Example Artist" },
            ReleaseDateOriginal = new DateTimeOffset(2024, 6, 18, 0, 0, 0, TimeSpan.Zero)
        };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"{AlbumArtist}\[{ReleaseYear}] {AlbumTitle} [{Version}] [{Quality}]",
            album,
            "Example Artist",
            "Example Album",
            "FLAC 24/192");

        Assert.Equal(
            Path.Combine(@"D:\Sort", "Example Artist", "[2024] Example Album [Deluxe Edition] [FLAC (24bit-192kHz)]"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_LeavesDedicatedVersionFolderWhenTitleAlreadyContainsVersion()
    {
        var album = new Album
        {
            Title = "Folklore (deluxe version - explicit)",
            Version = "deluxe version",
            Artist = new Artist { Name = "Example Artist" }
        };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"{AlbumArtist}\{AlbumTitle}\{Version}",
            album,
            "Example Artist",
            "Folklore (deluxe version - explicit)",
            "FLAC 16/44.1");

        Assert.Equal(
            Path.Combine(@"D:\Sort", "Example Artist", "Folklore (deluxe version - explicit)", "deluxe version"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_SuppressesVersionInSameSegmentWhenTitleAlreadyContainsVersion()
    {
        var album = new Album
        {
            Title = "Folklore (deluxe version - explicit)",
            Version = "deluxe version",
            Artist = new Artist { Name = "Example Artist" }
        };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"{AlbumArtist}\{AlbumTitle} [{Version}]",
            album,
            "Example Artist",
            "Folklore (deluxe version - explicit)",
            "FLAC 16/44.1");

        Assert.Equal(
            Path.Combine(@"D:\Sort", "Example Artist", "Folklore (deluxe version - explicit)"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_KeepsVersionInSameSegmentWhenTitleOnlyPartiallyOverlaps()
    {
        var album = new Album
        {
            Title = "A Charlie Brown Christmas (Remastered & Expanded Edition)",
            Version = "2012 Remastered & Expanded Edition",
            Artist = new Artist { Name = "Vince Guaraldi Trio" }
        };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"{AlbumArtist}\{AlbumTitle} [{Version}]",
            album,
            "Vince Guaraldi Trio",
            "A Charlie Brown Christmas (Remastered & Expanded Edition)",
            "FLAC 24/96");

        Assert.Equal(
            Path.Combine(
                @"D:\Sort",
                "Vince Guaraldi Trio",
                "A Charlie Brown Christmas (Remastered & Expanded Edition) [2012 Remastered & Expanded Edition]"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_RendersFilenameTemplate()
    {
        var album = new Album
        {
            Title = "Piano Miniatures",
            Artist = new Artist { Name = "MusicalBasics" },
            TracksCount = 12
        };
        var track = new Track
        {
            Title = "Nocturne in C Minor",
            TrackNumber = 3,
            Album = album
        };

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            "{TrackNumberPadded} - {TrackTitle}",
            track,
            album,
            "MusicalBasics",
            "Piano Miniatures",
            "FLAC 16/44.1",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("03 - Nocturne in C Minor.flac", fileName);
    }

    [Theory]
    [InlineData("{AlbumArtist}", "Example Album Artist.flac")]
    [InlineData("{AlbumTitle}", "Example Album.flac")]
    [InlineData("{TrackArtist}", "Example Track Artist.flac")]
    [InlineData("{TrackTitle}", "Example Track.flac")]
    [InlineData("{Version}", "Remastered.flac")]
    [InlineData("{ReleaseYear}", "2026.flac")]
    [InlineData("{ReleaseDate}", "2026-05-09.flac")]
    [InlineData("{ReleaseType}", "Album.flac")]
    [InlineData("{Quality}", "FLAC (24bit-96kHz).flac")]
    [InlineData("{TrackNumber}", "3.flac")]
    [InlineData("{TrackNumberPadded}", "03.flac")]
    [InlineData("{TotalTracks}", "10.flac")]
    [InlineData("{TotalTracksPadded}", "10.flac")]
    [InlineData("{DiscNumber}", "2.flac")]
    [InlineData("{DiscNumberPadded}", "02.flac")]
    [InlineData("{TotalDiscs}", "2.flac")]
    [InlineData("{TotalDiscsPadded}", "02.flac")]
    [InlineData("{Label}", "Example Records.flac")]
    [InlineData("{UPC}", "0000000000000.flac")]
    [InlineData("{ISRC}", "GBUM72600001.flac")]
    [InlineData("{ExplicitAdvisory}", "Explicit.flac")]
    public void PathTemplateRenderer_RendersEveryExposedFilenameField(string template, string expectedFileName)
    {
        var album = CreateFieldMatrixAlbum();
        var track = CreateFieldMatrixTrack(album);

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            template,
            track,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 10,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal(expectedFileName, fileName);
    }

    [Fact]
    public void PathTemplateRenderer_RendersPaddedTotalTracksWithTrackNumberPaddingWidth()
    {
        var album = CreateFieldMatrixAlbum();
        var track = CreateFieldMatrixTrack(album);

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            "{TrackNumberPadded}-{TotalTracksPadded}",
            track,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 9,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("03-09.flac", fileName);
    }

    [Theory]
    [InlineData("{AlbumArtist}", "Example Album Artist")]
    [InlineData("{AlbumTitle}", "Example Album")]
    [InlineData("{Version}", "Deluxe Edition")]
    [InlineData("{ReleaseYear}", "2026")]
    [InlineData("{ReleaseDate}", "2026-05-09")]
    [InlineData("{ReleaseType}", "Album")]
    [InlineData("{Quality}", "FLAC (24bit-96kHz)")]
    [InlineData("{TotalDiscs}", "2")]
    [InlineData("{TotalDiscsPadded}", "02")]
    [InlineData("{Label}", "Example Records")]
    [InlineData("{UPC}", "0000000000000")]
    public void PathTemplateRenderer_RendersEveryExposedFolderField(string template, string expectedFolderName)
    {
        var album = CreateFieldMatrixAlbum();

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            template,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96");

        Assert.Equal(Path.Combine(@"D:\Sort", expectedFolderName), destination);
    }

    [Fact]
    public void PathTemplateRenderer_BlankFolderTemplateUsesBaseFolder()
    {
        var album = CreateFieldMatrixAlbum();

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            string.Empty,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96");

        Assert.Equal(@"D:\Sort", destination);
    }

    [Fact]
    public void PathTemplateRenderer_NullFolderTemplateUsesDefaultFolderTemplate()
    {
        var album = CreateFieldMatrixAlbum();

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            null,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96");

        Assert.Equal(
            Path.Combine(@"D:\Sort", "Example Album Artist", "(2026) Example Album (Deluxe Edition) [FLAC (24bit-96kHz)]"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_RendersPreviewFolderFields()
    {
        var destination = PathTemplateRenderer.RenderAlbumDestinationPreview(
            @"D:\Sort",
            @"{AlbumTitle} [{Version}] [{ReleaseType}] [{Label}] [{UPC}] [{TotalDiscsPadded}] [{ExplicitAdvisory}]",
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            "2026-05-09",
            totalTracks: 10,
            version: "Remastered Edition",
            releaseType: "album",
            label: "Example Records",
            upc: "0000000000000",
            totalDiscs: 2,
            explicitAdvisory: true);

        Assert.Equal(
            Path.Combine(@"D:\Sort", "Example Album [Remastered Edition] [Album] [Example Records] [0000000000000] [02] [Explicit]"),
            destination);
    }

    [Fact]
    public void PathTemplateRenderer_RendersPreviewFilenameFields()
    {
        var fileName = PathTemplateRenderer.RenderAudioFilenamePreview(
            "{TrackNumberPadded} - {TrackTitle} [{Version}] [{ReleaseType}] [{Label}] [{ISRC}] [{ExplicitAdvisory}]",
            "Example Artist",
            "Example Album",
            "FLAC 24/96",
            "2026-05-09",
            totalTracks: 10,
            trackNumber: 1,
            trackTitle: "Example Track",
            version: "Remastered Edition",
            discNumber: 1,
            totalDiscs: 2,
            extension: ".flac",
            maxFileNameLength: 150,
            releaseType: "album",
            label: "Example Records",
            isrc: "GBUM72600001",
            explicitAdvisory: true);

        Assert.Equal("01 - Example Track [Remastered Edition] [Album] [Example Records] [GBUM72600001] [Explicit].flac", fileName);
    }

    [Theory]
    [InlineData("{DiscNumber}", "2")]
    [InlineData("{DiscNumberPadded}", "02")]
    [InlineData("{TotalDiscs}", "2")]
    [InlineData("{TotalDiscsPadded}", "02")]
    [InlineData("{Work}", "Example Work")]
    [InlineData("{Work} ({WorkComposer})", "Example Work (Example Composer)")]
    public void PathTemplateRenderer_RendersEveryExposedDiscFolderField(string template, string expectedFolderName)
    {
        var album = CreateFieldMatrixAlbum();
        var track = CreateFieldMatrixTrack(album);
        album.Tracks = new ItemSearchResult<Track> { Items = [track] };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            template,
            "Inline",
            "&",
            workSeparatorNoSpaces: false,
            track,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 10);

        Assert.Equal([expectedFolderName], segments);
    }

    [Fact]
    public void PathTemplateRenderer_RendersTrackVersionWhenFilenameTemplateRequestsIt()
    {
        var album = new Album
        {
            Title = "Piano Miniatures",
            Version = "Deluxe Edition",
            Artist = new Artist { Name = "MusicalBasics" },
            TracksCount = 12
        };
        var track = new Track
        {
            Title = "Nocturne in C Minor",
            Version = "Remastered",
            TrackNumber = 3,
            Album = album
        };

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            AppSettings.DefaultFilenameTemplate,
            track,
            album,
            "MusicalBasics",
            "Piano Miniatures",
            "FLAC 16/44.1",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("03 - Nocturne in C Minor (Remastered).flac", fileName);
    }

    [Fact]
    public void PathTemplateRenderer_RemovesEmptyVersionParenthesesFromFilename()
    {
        var album = new Album
        {
            Title = "Piano Miniatures",
            Version = "Deluxe Edition",
            Artist = new Artist { Name = "MusicalBasics" },
            TracksCount = 12
        };
        var track = new Track
        {
            Title = "Nocturne in C Minor",
            TrackNumber = 3,
            Album = album
        };

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            AppSettings.DefaultFilenameTemplate,
            track,
            album,
            "MusicalBasics",
            "Piano Miniatures",
            "FLAC 16/44.1",
            totalTracks: 12,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("03 - Nocturne in C Minor.flac", fileName);
    }

    [Fact]
    public void PathTemplateRenderer_RendersDiscFolderTemplate()
    {
        var album = new Album
        {
            Title = "Piano Miniatures",
            Artist = new Artist { Name = "MusicalBasics" },
            MediaCount = 2,
            TracksCount = 20
        };
        var track = new Track
        {
            Title = "Second Disc Opener",
            TrackNumber = 1,
            MediaNumber = 2,
            Album = album
        };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber}",
            AppSettings.DefaultDiscWorkHandling,
            AppSettings.DefaultDiscWorkSeparator,
            workSeparatorNoSpaces: false,
            track,
            album,
            "MusicalBasics",
            "Piano Miniatures",
            "FLAC 16/44.1",
            totalTracks: 20);

        Assert.Equal(["Disc 2"], segments);
    }

    [Fact]
    public void PathTemplateRenderer_RendersPaddedDiscAndTotalDiscFields()
    {
        var album = new Album
        {
            Title = "Piano Miniatures",
            Artist = new Artist { Name = "MusicalBasics" },
            MediaCount = 2,
            TracksCount = 20
        };
        var track = new Track
        {
            Title = "Second Disc Opener",
            TrackNumber = 1,
            MediaNumber = 2,
            Album = album
        };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumberPadded} of {TotalDiscsPadded}",
            AppSettings.DefaultDiscWorkHandling,
            AppSettings.DefaultDiscWorkSeparator,
            workSeparatorNoSpaces: false,
            track,
            album,
            "MusicalBasics",
            "Piano Miniatures",
            "FLAC 16/44.1",
            totalTracks: 20);

        Assert.Equal(["Disc 02 of 02"], segments);
    }

    [Fact]
    public void PathTemplateRenderer_UsesAlbumWidePaddingWhenDiscTemplateIsBlank()
    {
        var width = PathTemplateRenderer.GetTrackNumberPaddingWidth(
            [(1, 1), (10, 1), (11, 2), (107, 10)],
            currentDiscNumber: 1,
            discFolderTemplate: string.Empty);

        Assert.Equal(3, width);
    }

    [Fact]
    public void PathTemplateRenderer_UsesAlbumWidePaddingWhenDiscTrackNumbersContinue()
    {
        var width = PathTemplateRenderer.GetTrackNumberPaddingWidth(
            [(1, 1), (10, 1), (11, 2), (107, 10)],
            currentDiscNumber: 1,
            discFolderTemplate: "Disc {DiscNumber}");

        Assert.Equal(3, width);
    }

    [Fact]
    public void PathTemplateRenderer_UsesDiscScopedPaddingWhenDiscTrackNumbersRestart()
    {
        var width = PathTemplateRenderer.GetTrackNumberPaddingWidth(
            [(1, 1), (17, 1), (1, 2), (20, 2)],
            currentDiscNumber: 1,
            discFolderTemplate: "Disc {DiscNumber}");

        Assert.Equal(2, width);
    }

    [Fact]
    public void PathTemplateRenderer_UsesPaddingOverrideForTrackNumber()
    {
        var album = CreateFieldMatrixAlbum();
        var track = CreateFieldMatrixTrack(album);
        track.TrackNumber = 1;

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            "{TrackNumberPadded} - {TrackTitle}",
            track,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 17,
            extension: ".flac",
            maxFileNameLength: 150,
            trackNumberPaddingWidth: 107);

        Assert.Equal("001 - Example Track.flac", fileName);
    }

    [Fact]
    public void PathTemplateRenderer_BlankDiscTemplateCreatesNoDiscFolder()
    {
        var album = CreateFieldMatrixAlbum();
        var track = CreateFieldMatrixTrack(album);

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            string.Empty,
            "Inline",
            "&",
            workSeparatorNoSpaces: false,
            track,
            album,
            "Example Album Artist",
            "Example Album",
            "FLAC 24/96",
            totalTracks: 10);

        Assert.Empty(segments);
    }

    [Fact]
    public void PathTemplateRenderer_RendersExplicitAdvisoryAndFriendlyReleaseType()
    {
        var album = new Album
        {
            Title = "Piano Miniatures",
            Artist = new Artist { Name = "MusicalBasics" },
            ReleaseType = "epmini",
            ParentalWarning = true,
            TracksCount = 1
        };
        var track = new Track
        {
            Title = "Nocturne",
            TrackNumber = 1,
            Album = album
        };

        var fileName = PathTemplateRenderer.RenderAudioFilename(
            "{ReleaseType} - {ExplicitAdvisory} - {TrackTitle}",
            track,
            album,
            "MusicalBasics",
            "Piano Miniatures",
            "FLAC 16/44.1",
            totalTracks: 1,
            extension: ".flac",
            maxFileNameLength: 150);

        Assert.Equal("EP - Explicit - Nocturne.flac", fileName);
    }

    [Fact]
    public void PathTemplateRenderer_RendersInlineWorkFromDiscTracks()
    {
        var album = new Album
        {
            Title = "Symphonies",
            Artist = new Artist { Name = "Example Orchestra" },
            MediaCount = 1,
            TracksCount = 2
        };
        var firstTrack = new Track { Title = "Allegro", TrackNumber = 1, MediaNumber = 1, Work = "Work 1", Album = album };
        var secondTrack = new Track { Title = "Adagio", TrackNumber = 2, MediaNumber = 1, Work = "Work 2", Album = album };
        album.Tracks = new ItemSearchResult<Track> { Items = [firstTrack, secondTrack] };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work}",
            "Inline",
            "&",
            workSeparatorNoSpaces: false,
            firstTrack,
            album,
            "Example Orchestra",
            "Symphonies",
            "FLAC 24/96",
            totalTracks: 2);

        Assert.Equal(["Disc 1 - Work 1 & Work 2"], segments);
    }

    [Fact]
    public void PathTemplateRenderer_RendersWorkFoldersWhenConfigured()
    {
        var album = new Album
        {
            Title = "Symphonies",
            Artist = new Artist { Name = "Example Orchestra" },
            MediaCount = 1,
            TracksCount = 2
        };
        var track = new Track { Title = "Allegro", TrackNumber = 1, MediaNumber = 1, Work = "Work 1", Album = album };
        album.Tracks = new ItemSearchResult<Track> { Items = [track] };

        var segments = PathTemplateRenderer.RenderDiscFolderSegments(
            "Disc {DiscNumber} - {Work}",
            "Folders",
            "&",
            workSeparatorNoSpaces: false,
            track,
            album,
            "Example Orchestra",
            "Symphonies",
            "FLAC 24/96",
            totalTracks: 2);

        Assert.Equal(["Disc 1", "Work 1"], segments);
    }

    [Fact]
    public void PathTemplateRenderer_TreatsPlainWordsAsLiteralText()
    {
        var album = new Album
        {
            Title = "Example Album",
            Artist = new Artist { Name = "Example Artist" }
        };

        var destination = PathTemplateRenderer.RenderAlbumDestination(
            @"D:\Sort",
            @"Artist\Album",
            album,
            "Example Artist",
            "Example Album",
            "FLAC 16/44.1");

        Assert.Equal(Path.Combine(@"D:\Sort", "Artist", "Album"), destination);
    }

    private static Album CreateFieldMatrixAlbum()
    {
        return new Album
        {
            Title = "Example Album",
            Version = "Deluxe Edition",
            Artist = new Artist { Name = "Example Album Artist" },
            Label = new Label { Name = "Example Records" },
            ReleaseDateOriginal = new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero),
            ReleaseType = "album",
            Upc = "0000000000000",
            MediaCount = 2,
            TracksCount = 10,
            ParentalWarning = true
        };
    }

    private static Track CreateFieldMatrixTrack(Album album)
    {
        return new Track
        {
            Title = "Example Track",
            Version = "Remastered",
            Performer = new Artist { Name = "Example Track Artist" },
            TrackNumber = 3,
            MediaNumber = 2,
            Isrc = "GBUM72600001",
            Work = "Example Work",
            Composer = new Artist { Name = "Example Composer" },
            Album = album
        };
    }

    [Fact]
    public void QualityStringMappings_LimitsDisplayQualityToAlbumMaximum()
    {
        var album = new Album
        {
            MaximumBitDepth = 16,
            MaximumSamplingRate = 44.1
        };

        var quality = QualityStringMappings.GetEffectiveQuality("27", album);

        Assert.Equal("FLAC 16/44.1", quality.DisplayQuality);
        Assert.Equal("FLAC (16bit-44.1kHz)", quality.PathQuality);
    }

    [Fact]
    public void QualityStringMappings_MapsCurrentSettingsLabelsToFormatIds()
    {
        Assert.Equal(QualityStringMappings.FlacHighestFormatId, QualityStringMappings.GetFormatIdFromQualityLabel(QualityStringMappings.FlacHighestLabel));
        Assert.Equal(QualityStringMappings.Mp3FormatId, QualityStringMappings.GetFormatIdFromQualityLabel(QualityStringMappings.Mp3Label));
        Assert.Equal(QualityStringMappings.FlacHighestLabel, QualityStringMappings.GetQualityLabelFromFormatId(QualityStringMappings.FlacHighestFormatId));
        Assert.Equal(QualityStringMappings.Mp3Label, QualityStringMappings.GetQualityLabelFromFormatId(QualityStringMappings.Mp3FormatId));
    }

    [Fact]
    public void QualityStringMappings_DoesNotExposeLegacyFlacBucketsAsSettingsChoices()
    {
        Assert.Equal(string.Empty, QualityStringMappings.GetFormatIdFromQualityLabel("FLAC 24/192"));
        Assert.Equal(string.Empty, QualityStringMappings.GetQualityLabelFromFormatId("7"));
    }

    [Fact]
    public void QualityStringMappings_UsesActualFileUrlQuality()
    {
        var quality = QualityStringMappings.GetActualQuality(
            new FileUrl
            {
                FormatId = 7,
                BitDepth = 24,
                SamplingRate = 88.2,
                MimeType = "audio/flac"
            },
            "7");

        Assert.Equal("7", quality.FormatId);
        Assert.Equal("FLAC 24/88.2", quality.DisplayQuality);
        Assert.Equal("FLAC (24bit-88.2kHz)", quality.PathQuality);
        Assert.Equal(".flac", quality.Extension);
    }

    [Fact]
    public void QualityStringMappings_UsesMp3ExtensionForActualMp3()
    {
        var quality = QualityStringMappings.GetActualQuality(
            new FileUrl
            {
                FormatId = 5,
                MimeType = "audio/mpeg"
            },
            "5");

        Assert.Equal("MP3 320", quality.DisplayQuality);
        Assert.Equal("MP3", quality.PathQuality);
        Assert.Equal(".mp3", quality.Extension);
    }

    [Theory]
    [InlineData("FLAC (Highest Available)", "FLAC (Highest Available)", "FLAC")]
    [InlineData("FLAC 24/88.2", "FLAC 24/88.2", "FLAC")]
    [InlineData("MP3 320", "MP3 320", "MP3 320")]
    public void QualityStringMappings_ReturnsFullAndCompactDisplayText(
        string quality,
        string expectedFull,
        string expectedCompact)
    {
        var display = QualityStringMappings.GetDisplayText(quality);

        Assert.Equal(expectedFull, display.Full);
        Assert.Equal(expectedCompact, display.Compact);
    }

    [Fact]
    public void QobuzStorefrontSearchConfigProvider_ParsesInlineAlgoliaConfig()
    {
        const string html = """
            <script>
              window.qobuz.algolia2 = {"application_id":"APPID","api_key":"APIKEY","index":{"main_labels":"labelsV1"}};
            </script>
            """;

        var config = QobuzStorefrontSearchConfigProvider.ParseConfig(html);

        Assert.Equal("APPID", config.ApplicationId);
        Assert.Equal("APIKEY", config.ApiKey);
        Assert.Equal("labelsV1", config.LabelsIndex);
        Assert.Equal("https://APPID-dsn.algolia.net/1/indexes/labelsV1/query", config.LabelsEndpoint);
    }

    [Fact]
    public async Task QobuzStorefrontSearchConfigProvider_RetriesAfterFailedFetch()
    {
        var handler = new FailingThenSuccessfulConfigHandler();
        using var httpClient = new HttpClient(handler);
        var provider = new QobuzStorefrontSearchConfigProvider(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetConfigAsync());

        var config = await provider.GetConfigAsync();

        Assert.Equal("APPID", config.ApplicationId);
        Assert.Equal("APIKEY", config.ApiKey);
        Assert.Equal("labelsV1", config.LabelsIndex);
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class FailingThenSuccessfulConfigHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                throw new HttpRequestException("Temporary failure");
            }

            const string html = """
                <script>
                  window.qobuz.algolia2 = {"application_id":"APPID","api_key":"APIKEY","index":{"main_labels":"labelsV1"}};
                </script>
                """;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            });
        }
    }

}
