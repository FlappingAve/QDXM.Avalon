using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Api;

namespace QDXM.Avalon.Tests;

public sealed class QobuzPaginationTests
{
    [Fact]
    public void FetchAll_PaginatesUntilReportedTotalIsReached()
    {
        var requestedOffsets = new List<int>();

        var result = QobuzPagination.FetchAll(
            pageSize: 2,
            fetchPage: (_, offset) =>
            {
                requestedOffsets.Add(offset);
                return offset switch
                {
                    0 => new TestPage([1, 2], Total: 5),
                    2 => new TestPage([3, 4], Total: 5),
                    4 => new TestPage([5], Total: 5),
                    _ => new TestPage([], Total: 5)
                };
            },
            selectItems: page => page.Items,
            selectTotal: page => page.Total);

        Assert.Equal([0, 2, 4], requestedOffsets);
        Assert.Equal([1, 2, 3, 4, 5], result.Items);
    }

    [Fact]
    public void FetchAll_StopsWhenPageIsEmpty()
    {
        var requestedOffsets = new List<int>();

        var result = QobuzPagination.FetchAll(
            pageSize: 2,
            fetchPage: (_, offset) =>
            {
                requestedOffsets.Add(offset);
                return offset == 0
                    ? new TestPage([1, 2], Total: 5)
                    : new TestPage([], Total: 5);
            },
            selectItems: page => page.Items,
            selectTotal: page => page.Total);

        Assert.Equal([0, 2], requestedOffsets);
        Assert.Equal([1, 2], result.Items);
    }

    [Fact]
    public void FetchAll_StopsWhenPageIsShortWithoutReportedTotal()
    {
        var requestedOffsets = new List<int>();

        var result = QobuzPagination.FetchAll(
            pageSize: 2,
            fetchPage: (_, offset) =>
            {
                requestedOffsets.Add(offset);
                return offset == 0
                    ? new TestPage([1, 2])
                    : new TestPage([3]);
            },
            selectItems: page => page.Items);

        Assert.Equal([0, 2], requestedOffsets);
        Assert.Equal([1, 2, 3], result.Items);
    }

    [Fact]
    public void FetchAll_StopsAtMaxPageGuard()
    {
        var requestedOffsets = new List<int>();

        var result = QobuzPagination.FetchAll(
            pageSize: 1,
            fetchPage: (_, offset) =>
            {
                requestedOffsets.Add(offset);
                return new TestPage([offset + 1]);
            },
            selectItems: page => page.Items,
            maxPages: 3);

        Assert.Equal([0, 1, 2], requestedOffsets);
        Assert.Equal([1, 2, 3], result.Items);
    }

    [Fact]
    public void FetchAlbumWithAllTracks_MergesTrackPagesAndPreservesFirstAlbumMetadata()
    {
        var requestedOffsets = new List<int>();
        var firstPage = CreateAlbumPage(
            "Huge Album",
            Enumerable.Range(1, QobuzApiLimits.AlbumTrackPageSize),
            total: QobuzApiLimits.AlbumTrackPageSize + 2);
        var secondPage = CreateAlbumPage(
            "Ignored Metadata",
            [QobuzApiLimits.AlbumTrackPageSize + 1, QobuzApiLimits.AlbumTrackPageSize + 2],
            total: QobuzApiLimits.AlbumTrackPageSize + 2);

        var album = QobuzPagination.FetchAlbumWithAllTracks((limit, offset) =>
        {
            Assert.Equal(QobuzApiLimits.AlbumTrackPageSize, limit);
            requestedOffsets.Add(offset);
            return offset == 0 ? firstPage : secondPage;
        });

        Assert.Same(firstPage, album);
        Assert.Equal("Huge Album", album.Title);
        Assert.Equal([0, QobuzApiLimits.AlbumTrackPageSize], requestedOffsets);
        Assert.Equal(QobuzApiLimits.AlbumTrackPageSize + 2, album.Tracks?.Items?.Count);
        Assert.Equal(
            Enumerable.Range(1, QobuzApiLimits.AlbumTrackPageSize + 2),
            album.Tracks?.Items?.Select(track => track.Id ?? 0));
    }

    private static Album CreateAlbumPage(string title, IEnumerable<int> trackIds, int total)
    {
        return new Album
        {
            Id = "huge-album",
            Title = title,
            TracksCount = total,
            Tracks = new ItemSearchResult<Track>
            {
                Total = total,
                Items = trackIds.Select(id => new Track { Id = id }).ToList()
            }
        };
    }

    private sealed record TestPage(IReadOnlyList<int> Items, int? Total = null);
}
