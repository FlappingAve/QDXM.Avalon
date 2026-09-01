using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Api;

namespace QDXM.Avalon.Tests;

public sealed class QobuzApiSearchMapperTests
{
    [Fact]
    public void ToAlbumResult_MapsAlbumMetadataAndExpandedTracks()
    {
        var album = new Album
        {
            Id = "wuzmnoqpnd7hn",
            Title = "Fur Elise Nightmare",
            Version = "Piano Solo",
            Artist = new Artist { Name = "MusicalBasics" },
            MaximumBitDepth = 24,
            MaximumSamplingRate = 192,
            ReleaseDateOriginal = new DateTimeOffset(2024, 6, 18, 0, 0, 0, TimeSpan.Zero),
            Upc = "0198673572136",
            Image = new Image { Large = "https://static.qobuz.com/cover.jpg" },
            TracksCount = 1,
            Tracks = new ItemSearchResult<Track>
            {
                Items =
                [
                    new Track
                    {
                        Id = 123,
                        TrackNumber = 1,
                        Title = "Fur Elise Nightmare",
                        Duration = 216,
                        MaximumBitDepth = 24,
                        MaximumSamplingRate = 192,
                        Work = "Example Work",
                        Composer = new Artist { Name = "Example Composer" },
                        ParentalWarning = true
                    }
                ]
            }
        };

        var result = QobuzApiSearchMapper.ToAlbumResult(album);

        Assert.Equal("wuzmnoqpnd7hn", result.AlbumId);
        Assert.Equal("Fur Elise Nightmare", result.Title);
        Assert.Equal("Piano Solo", result.Version);
        Assert.Equal("MusicalBasics", result.Artist);
        Assert.Equal("FLAC 24/192", result.Quality);
        Assert.Equal("2024-06-18", result.ReleaseDate);
        Assert.Equal("0198673572136", result.Upc);
        Assert.Equal("https://static.qobuz.com/cover.jpg", result.ThumbnailUrl);
        Assert.Equal("https://open.qobuz.com/album/wuzmnoqpnd7hn", result.WebPlayerUrl);
        Assert.True(result.Explicit);

        var track = Assert.Single(result.Tracks);
        Assert.Equal("123", track.TrackId);
        Assert.Equal(1, track.TrackNumber);
        Assert.Equal("Example Work", track.Work);
        Assert.Equal("Example Composer", track.Composer);
        Assert.Equal(TimeSpan.FromSeconds(216), track.Duration);
    }

    [Fact]
    public void ToTrackResult_UsesTrackFieldsWithAlbumFallbacks()
    {
        var track = new Track
        {
            Id = 456,
            Title = "Nocturne",
            Version = "Remastered",
            Performer = new Artist { Id = 1234, Name = "Pianist" },
            Duration = 121,
            ReleaseDateDownload = new DateTimeOffset(2025, 4, 8, 0, 0, 0, TimeSpan.Zero),
            Album = new Album
            {
                Title = "Piano Miniatures",
                Artist = new Artist { Name = "Album Artist" },
                MaximumBitDepth = 16,
                MaximumSamplingRate = 44.1,
                Image = new Image { Medium = "https://static.qobuz.com/medium.jpg" },
                RelativeUrl = "/us-en/album/piano-miniatures"
            }
        };

        var result = QobuzApiSearchMapper.ToTrackResult(track);

        Assert.Equal("456", result.TrackId);
        Assert.Equal("1234", result.ArtistId);
        Assert.Equal("Nocturne", result.Title);
        Assert.Equal("Remastered", result.Version);
        Assert.Equal("Pianist", result.Artist);
        Assert.Equal("Piano Miniatures", result.AlbumTitle);
        Assert.Equal("FLAC 16/44.1", result.Quality);
        Assert.Equal("2025-04-08", result.ReleaseDate);
        Assert.Equal("https://static.qobuz.com/medium.jpg", result.ThumbnailUrl);
        Assert.Equal("https://open.qobuz.com/track/456", result.WebPlayerUrl);
        Assert.Equal("https://www.qobuz.com/us-en/album/piano-miniatures", result.StoreUrl);
    }

    [Fact]
    public void ToPlaylistResult_PrefersPlaylistRectangleImage()
    {
        var playlist = new Playlist
        {
            Id = 14853863,
            Name = "Speaker Test",
            Owner = new Owner { Name = "MusicEnjoyer" },
            UpdatedAt = 1770854400,
            CreatedAt = 1770768000,
            Duration = 3600,
            TracksCount = 1900,
            ImageRectangle = ["https://static.qobuz.com/playlist-rectangle.jpg"],
            Images300 = ["https://static.qobuz.com/first-track-album.jpg"]
        };

        var result = QobuzApiSearchMapper.ToPlaylistResult(playlist);

        Assert.Equal("14853863", result.PlaylistId);
        Assert.Equal("Speaker Test", result.Title);
        Assert.Equal("MusicEnjoyer", result.Owner);
        Assert.Equal("2026-02-12", result.UpdatedDate);
        Assert.Equal(TimeSpan.FromSeconds(3600), result.Duration);
        Assert.Equal("https://static.qobuz.com/playlist-rectangle.jpg", result.ThumbnailUrl);
        Assert.Equal("https://open.qobuz.com/playlist/14853863", result.WebPlayerUrl);
        Assert.Equal(1900, result.TotalTracks);
    }

    [Fact]
    public void ToPlaylistTrackPage_MapsPlaylistAndAlbumPositions()
    {
        var playlist = new Playlist
        {
            TracksCount = 1900,
            Tracks = new ItemSearchResult<Track>
            {
                Items =
                [
                    new Track
                    {
                        Id = 123,
                        PlaylistTrackId = 999,
                        PlaylistPosition = 42,
                        TrackNumber = 7,
                        MediaNumber = 2,
                        Title = "Example Track",
                        Version = "2018 Mix",
                        Duration = 245,
                        Performer = new Artist { Name = "Example Artist" },
                        Album = new Album
                        {
                            Title = "Example Album",
                            MediaCount = 2
                        }
                    }
                ]
            }
        };

        var page = QobuzApiSearchMapper.ToPlaylistTrackPage(playlist, offset: 30);

        Assert.Equal(1900, page.TotalTracks);
        var track = Assert.Single(page.Tracks);
        Assert.Equal("123", track.TrackId);
        Assert.Equal("playlist-track:999", track.SelectionKey);
        Assert.Equal(42, track.PlaylistPosition);
        Assert.Equal("0042", track.PlaylistPositionDisplay);
        Assert.Equal(7, track.AlbumTrackNumber);
        Assert.Equal(2, track.AlbumDiscNumber);
        Assert.Equal("2-07", track.AlbumPositionDisplay);
        Assert.Equal("Example Track", track.Title);
        Assert.Equal("2018 Mix", track.Version);
        Assert.Equal("Example Artist", track.Artist);
        Assert.Equal("Example Album", track.AlbumTitle);
        Assert.Equal(TimeSpan.FromSeconds(245), track.Duration);
    }
}
