using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.Tests;

public sealed class SearchCoreRulesTests
{
    [Theory]
    [InlineData("0060252730452", SearchResultType.Albums, 0, true, "0060252730452")]
    [InlineData(" 0060252730452 ", SearchResultType.Albums, 0, true, "0060252730452")]
    [InlineData("id:0060252730452", SearchResultType.Albums, 0, true, "0060252730452")]
    [InlineData("id: wuzmnoqpnd7hn", SearchResultType.Albums, 0, true, "wuzmnoqpnd7hn")]
    [InlineData("wuzmnoqpnd7hn", SearchResultType.Albums, 0, false, "wuzmnoqpnd7hn")]
    [InlineData("0060252730452", SearchResultType.Tracks, 0, false, "0060252730452")]
    [InlineData("0060252730452", SearchResultType.Albums, 25, false, "0060252730452")]
    public void TryGetDirectAlbumId_MatchesFirstPageNumericOrExplicitAlbumIdQueries(
        string query,
        SearchResultType type,
        int offset,
        bool expectedMatch,
        string expectedAlbumId)
    {
        var options = new SearchQueryOptions(query, type, Offset: offset);

        var matched = SearchQueryClassifier.TryGetDirectAlbumId(options, out var albumId);

        Assert.Equal(expectedMatch, matched);
        Assert.Equal(expectedAlbumId, albumId);
    }

    [Theory]
    [InlineData("12345", true, "12345")]
    [InlineData(" id:12345 ", true, "12345")]
    [InlineData("id: 12345", true, "12345")]
    [InlineData("abc123", false, "abc123")]
    [InlineData("id:abc123", false, "abc123")]
    public void TryGetNumericId_NormalizesExplicitIdQueries(
        string query,
        bool expectedMatch,
        string expectedId)
    {
        var options = new SearchQueryOptions(query);

        var matched = SearchQueryClassifier.TryGetNumericId(options, out var id);

        Assert.Equal(expectedMatch, matched);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("id:12345", SearchResultType.Tracks, 0, true, "12345")]
    [InlineData("12345", SearchResultType.Tracks, 0, false, "12345")]
    [InlineData("id:abc123", SearchResultType.Tracks, 0, false, "abc123")]
    [InlineData("id:12345", SearchResultType.Tracks, 25, false, "12345")]
    [InlineData("id:12345", SearchResultType.Albums, 0, false, "12345")]
    public void TryGetDirectTrackId_RequiresExplicitFirstPageNumericTrackId(
        string query,
        SearchResultType type,
        int offset,
        bool expectedMatch,
        string expectedTrackId)
    {
        var options = new SearchQueryOptions(query, type, Offset: offset);

        var matched = SearchQueryClassifier.TryGetDirectTrackId(options, out var trackId);

        Assert.Equal(expectedMatch, matched);
        Assert.Equal(expectedTrackId, trackId);
    }

    [Fact]
    public void PlaylistTrackSelectionKey_UsesStablePlaylistIdentity()
    {
        var withPlaylistTrackId = new Track { PlaylistTrackId = 1234, PlaylistPosition = 9 };
        var withPositionOnly = new Track { PlaylistPosition = 9 };
        var withReturnedIndexOnly = new Track();

        Assert.Equal("playlist-track:1234", PlaylistTrackSelectionKey.Create(
            withPlaylistTrackId.PlaylistTrackId,
            withPlaylistTrackId.PlaylistPosition,
            returnedIndex: 8));
        Assert.Equal("position:9", PlaylistTrackSelectionKey.Create(
            withPositionOnly.PlaylistTrackId,
            withPositionOnly.PlaylistPosition,
            returnedIndex: 8));
        Assert.Equal("index:8", PlaylistTrackSelectionKey.Create(
            withReturnedIndexOnly.PlaylistTrackId,
            withReturnedIndexOnly.PlaylistPosition,
            returnedIndex: 8));

        Assert.True(PlaylistTrackSelectionKey.Matches(withPlaylistTrackId, 8, "playlist-track:1234"));
        Assert.True(PlaylistTrackSelectionKey.Matches(withPositionOnly, 8, "position:9"));
        Assert.True(PlaylistTrackSelectionKey.Matches(withReturnedIndexOnly, 8, "index:8"));
    }

    [Fact]
    public void QobuzApiSearchMapper_CarriesTrackSourceAlbumId()
    {
        var track = new Track
        {
            Id = 12345,
            Title = "Example Track",
            Performer = new Artist
            {
                Id = 6789,
                Name = "Example Artist"
            },
            Album = new Album
            {
                Id = "source-album",
                Title = "Source Album"
            }
        };

        var result = QobuzApiSearchMapper.ToTrackResult(track);

        Assert.Equal("12345", result.TrackId);
        Assert.Equal("source-album", result.AlbumId);
        Assert.Equal("6789", result.ArtistId);
    }

    [Fact]
    public void SearchQualityRanker_RanksActualQualityMetadata()
    {
        var qualities = new[]
        {
            "FLAC 16/44.1",
            "FLAC 24/96",
            "FLAC 24/176.4",
            "FLAC 24/88.2",
            "MP3 320"
        };

        var sorted = qualities
            .OrderByDescending(SearchQualityRanker.Rank)
            .ToList();

        Assert.Equal(
            ["FLAC 24/176.4", "FLAC 24/96", "FLAC 24/88.2", "FLAC 16/44.1", "MP3 320"],
            sorted);
    }

    [Theory]
    [InlineData(SearchResultType.Albums, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.ReleaseDate, SearchArrangeOption.Quality, SearchArrangeOption.Name, SearchArrangeOption.TotalTracks })]
    [InlineData(SearchResultType.Tracks, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.ReleaseDate, SearchArrangeOption.Quality, SearchArrangeOption.Name })]
    [InlineData(SearchResultType.Playlists, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.LastUpdated, SearchArrangeOption.Name, SearchArrangeOption.TotalTracks })]
    [InlineData(SearchResultType.Artist, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.Name, SearchArrangeOption.TotalAlbums })]
    [InlineData(SearchResultType.Label, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.Name, SearchArrangeOption.TotalAlbums })]
    [InlineData(SearchResultType.ArtistAlbums, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.ReleaseDate, SearchArrangeOption.Quality, SearchArrangeOption.Name, SearchArrangeOption.TotalTracks })]
    [InlineData(SearchResultType.LabelAlbums, new[] { SearchArrangeOption.Unarranged, SearchArrangeOption.ReleaseDate, SearchArrangeOption.Quality, SearchArrangeOption.Name, SearchArrangeOption.TotalTracks })]
    [InlineData(SearchResultType.Genres, new[] { SearchArrangeOption.Unarranged })]
    public void SearchArrangeOptions_ReturnsCategorySpecificOptions(
        SearchResultType type,
        SearchArrangeOption[] expectedOptions)
    {
        Assert.Equal(expectedOptions, SearchArrangeOptions.ForType(type));
    }

    [Fact]
    public void SearchPageSizeOptions_CapsArtistAlbumsAtArtistReleaseLimit()
    {
        Assert.Equal([15, 25, 50, QobuzApiLimits.ArtistReleasePageSize], SearchPageSizeOptions.ForType(SearchResultType.ArtistAlbums));
        Assert.Equal(QobuzApiLimits.ArtistReleasePageSize, SearchPageSizeOptions.ClampLimit(SearchResultType.ArtistAlbums, 500));
        Assert.DoesNotContain(250, SearchPageSizeOptions.ForType(SearchResultType.ArtistAlbums));
        Assert.DoesNotContain(500, SearchPageSizeOptions.ForType(SearchResultType.ArtistAlbums));
    }

    [Fact]
    public void GenreStorefrontUrl_NormalizesPageAndSort()
    {
        var matched = QobuzGenreStorefrontUrl.TryParse(
            "https://www.qobuz.com/ch-fr/genre/k-pop/download-streaming-albums/page/5?ssf%5BsortBy%5D=main_catalog_date_desc",
            out var url);

        Assert.True(matched);
        Assert.Equal(SearchGenreSortOption.Newest, url.Sort);
        Assert.Equal(
            "https://www.qobuz.com/ch-fr/genre/k-pop/download-streaming-albums/page/2?ssf%5BsortBy%5D=main_catalog_awards",
            url.CreatePageUrl(2, SearchGenreSortOption.MostAwarded));
    }

    [Fact]
    public void GenreStorefrontParser_ExtractsLanguageNeutralAlbumCardFields()
    {
        const string Html = """
            <li>
              <div class="product__item">
                <div class="product__cover webp-bg lazy" data-src="https://static.qobuz.com/images/covers/56/wk/albumid_230.jpg">
                  <a href="/fr-fr/album/example-album/exampleid">
                    <div class="product__data">
                      <p class="product__data--genre">K-pop</p>
                      <p class="product__data--release">16 mai 2026</p>
                    </div>
                  </a>
                  <button data-itemId="exampleid"></button>
                </div>
                <div class="product__container">
                  <h3 class="product__name" data-title="Example &amp; Album">Example &amp; Album</h3>
                  <p class="product__artist"><a href="/fr-fr/interpreter/example/1">Example Artist</a></p>
                  <span class="album-quality__info">24-Bit/44.1 kHz</span>
                  <span class="album-quality__info">Stereo</span>
                </div>
              </div>
            </li>
            """;

        var albums = new QobuzGenreStorefrontParser()
            .ParseAlbums(Html, new Uri("https://www.qobuz.com/fr-fr/genre/k-pop/download-streaming-albums"));

        var album = Assert.Single(albums);
        Assert.Equal("exampleid", album.AlbumId);
        Assert.Equal("Example & Album", album.Title);
        Assert.Equal("Example Artist", album.Artist);
        Assert.Equal("16 mai 2026", album.ReleaseDate);
        Assert.Equal("FLAC 24/44.1", album.Quality);
        Assert.Equal("https://open.qobuz.com/album/exampleid", album.WebPlayerUrl);
    }

    [Fact]
    public void SearchQueryOptions_NormalizesLimitBySearchType()
    {
        var artistAlbums = new SearchQueryOptions("12345", SearchResultType.ArtistAlbums, Limit: 500);
        var albums = new SearchQueryOptions("beatles", SearchResultType.Albums, Limit: 500);

        Assert.Equal(QobuzApiLimits.ArtistReleasePageSize, artistAlbums.NormalizedLimit);
        Assert.Equal(QobuzApiLimits.SearchPageSize, albums.NormalizedLimit);
    }

    [Fact]
    public void QobuzUrlBuilder_CreatesOpenUrls()
    {
        Assert.Equal("https://open.qobuz.com/album/abc123", QobuzUrlBuilder.CreateOpenUrl("album", "abc123"));
        Assert.Equal("https://open.qobuz.com/playlist/11932795", QobuzUrlBuilder.CreateOpenUrl(DownloadContentType.Playlist, "11932795"));
        Assert.Equal("https://open.qobuz.com/album/abc123", QobuzUrlBuilder.CreateOpenUrlFromPath("/album/abc123"));
    }
}
