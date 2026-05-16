using QobuzApiSharp.Models.Content;

namespace QDXM.Avalon.Core.Api;

public static class QobuzPagination
{
    public static QobuzPagedResult<TPage, TItem> FetchAll<TPage, TItem>(
        int pageSize,
        Func<int, int, TPage> fetchPage,
        Func<TPage, IEnumerable<TItem>?> selectItems,
        Func<TPage, int?>? selectTotal = null,
        int maxPages = QobuzApiLimits.MaxPaginationPages)
    {
        ArgumentNullException.ThrowIfNull(fetchPage);
        ArgumentNullException.ThrowIfNull(selectItems);

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        if (maxPages <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPages), "Max pages must be greater than zero.");
        }

        TPage? firstPage = default;
        var hasFirstPage = false;
        var items = new List<TItem>();
        int? total = null;
        var offset = 0;

        for (var pageIndex = 0; pageIndex < maxPages; pageIndex++)
        {
            var page = fetchPage(pageSize, offset);
            if (!hasFirstPage)
            {
                firstPage = page;
                hasFirstPage = true;
            }

            total ??= selectTotal?.Invoke(page);

            var pageItems = selectItems(page)?
                .Where(item => item is not null)
                .ToList() ?? [];

            if (pageItems.Count == 0)
            {
                break;
            }

            items.AddRange(pageItems);

            if (total is not null && items.Count >= total.Value)
            {
                break;
            }

            if (total is null && pageItems.Count < pageSize)
            {
                break;
            }

            offset += pageItems.Count;
        }

        if (!hasFirstPage || firstPage is null)
        {
            throw new InvalidOperationException("Pagination did not return an initial page.");
        }

        return new QobuzPagedResult<TPage, TItem>(firstPage, items);
    }

    public static Album FetchAlbumWithAllTracks(Func<int, int, Album> fetchAlbumPage)
    {
        ArgumentNullException.ThrowIfNull(fetchAlbumPage);

        var result = FetchAll(
            QobuzApiLimits.AlbumTrackPageSize,
            fetchAlbumPage,
            album => album.Tracks?.Items,
            album => album.Tracks?.Total ?? album.TracksCount);

        var album = result.FirstPage;
        album.Tracks ??= new ItemSearchResult<Track>();
        album.Tracks.Items = result.Items.ToList();
        album.Tracks.Total ??= album.Tracks.Items.Count;

        return album;
    }
}

public sealed record QobuzPagedResult<TPage, TItem>(
    TPage FirstPage,
    IReadOnlyList<TItem> Items);
