using QDXM.Avalon.Core.Downloads;

namespace QDXM.Avalon.Tests;

public sealed class DownloadUrlParserTests
{
    [Theory]
    [InlineData("https://open.qobuz.com/album/abc123", DownloadContentType.Album, "abc123")]
    [InlineData("https://open.qobuz.com/track/987654", DownloadContentType.Track, "987654")]
    [InlineData("https://open.qobuz.com/playlist/111222", DownloadContentType.Playlist, "111222")]
    [InlineData("https://www.qobuz.com/us-en/playlists/new-releases/2049430", DownloadContentType.Playlist, "2049430")]
    [InlineData("https://www.qobuz.com/us-en/playlists/100-tracks-to-test-your-speakers/14853863", DownloadContentType.Playlist, "14853863")]
    [InlineData("https://open.qobuz.com/interpreter/artistid", DownloadContentType.Artist, "artistid")]
    [InlineData("https://open.qobuz.com/label/labelid", DownloadContentType.Label, "labelid")]
    [InlineData("https://play.qobuz.com/user/library/favorites/albums", DownloadContentType.Favorites, "albums")]
    [InlineData("https://play.qobuz.com/user/library/favorites/tracks", DownloadContentType.Favorites, "tracks")]
    [InlineData("QDXMA://album/protocolid", DownloadContentType.Album, "protocolid")]
    public void TryParseDownloadUrl_ReturnsRequestForSupportedUrls(
        string url,
        DownloadContentType expectedType,
        string expectedId)
    {
        var parsed = DownloadUrlParser.TryParseDownloadUrl(url, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(expectedType, request.ContentType);
        Assert.Equal(expectedId, request.ContentId);
    }

    [Theory]
    [InlineData(DownloadContentType.Album, true)]
    [InlineData(DownloadContentType.Track, true)]
    [InlineData(DownloadContentType.Playlist, true)]
    [InlineData(DownloadContentType.Favorites, true)]
    [InlineData(DownloadContentType.Artist, false)]
    [InlineData(DownloadContentType.Label, false)]
    public void DownloadRequestSupport_MatchesCurrentAvaloniaRunnerScope(
        DownloadContentType type,
        bool expected)
    {
        Assert.Equal(expected, DownloadRequestSupport.IsSupportedNow(type));
    }

    [Fact]
    public void TryParseDownloadUrl_RejectsUnsupportedUrls()
    {
        var parsed = DownloadUrlParser.TryParseDownloadUrl("https://example.com/album/abc123", out var request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParseDownloadUrl_RejectsGenericUserUrls()
    {
        var parsed = DownloadUrlParser.TryParseDownloadUrl("https://open.qobuz.com/user/favoritesid", out var request);

        Assert.False(parsed);
        Assert.Null(request);
    }
}
