using QDXM.Avalon.Core.Api;

namespace QDXM.Avalon.Core.Search;

public static class SearchPageSizeOptions
{
    private static readonly IReadOnlyList<int> DefaultOptions = [15, 25, 50, 100, 250, QobuzApiLimits.SearchPageSize];
    private static readonly IReadOnlyList<int> ArtistAlbumOptions = [15, 25, 50, QobuzApiLimits.ArtistReleasePageSize];

    public static IReadOnlyList<int> ForType(SearchResultType type)
    {
        return type switch
        {
            SearchResultType.ArtistAlbums => ArtistAlbumOptions,
            SearchResultType.Genres => [],
            _ => DefaultOptions
        };
    }

    public static int ClampLimit(SearchResultType type, int limit)
    {
        if (type is SearchResultType.Genres)
        {
            return 0;
        }

        var max = type is SearchResultType.ArtistAlbums
            ? QobuzApiLimits.ArtistReleasePageSize
            : QobuzApiLimits.SearchPageSize;

        return Math.Clamp(limit, 1, max);
    }
}
