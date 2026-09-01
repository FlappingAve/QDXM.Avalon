namespace QDXM.Avalon.Core.Search;

public static class SearchArrangeOptions
{
    private static readonly IReadOnlyList<SearchArrangeOption> AlbumOptions =
    [
        SearchArrangeOption.Unarranged,
        SearchArrangeOption.ReleaseDate,
        SearchArrangeOption.Quality,
        SearchArrangeOption.Name,
        SearchArrangeOption.TotalTracks
    ];

    private static readonly IReadOnlyList<SearchArrangeOption> TrackOptions =
    [
        SearchArrangeOption.Unarranged,
        SearchArrangeOption.ReleaseDate,
        SearchArrangeOption.Quality,
        SearchArrangeOption.Name
    ];

    private static readonly IReadOnlyList<SearchArrangeOption> PlaylistOptions =
    [
        SearchArrangeOption.Unarranged,
        SearchArrangeOption.LastUpdated,
        SearchArrangeOption.Name,
        SearchArrangeOption.TotalTracks
    ];

    private static readonly IReadOnlyList<SearchArrangeOption> EntityOptions =
    [
        SearchArrangeOption.Unarranged,
        SearchArrangeOption.Name,
        SearchArrangeOption.TotalAlbums
    ];

    public static IReadOnlyList<SearchArrangeOption> ForType(SearchResultType type)
    {
        return type switch
        {
            SearchResultType.Albums or SearchResultType.ArtistAlbums or SearchResultType.LabelAlbums => AlbumOptions,
            SearchResultType.Tracks => TrackOptions,
            SearchResultType.Playlists => PlaylistOptions,
            SearchResultType.Artist or SearchResultType.Label => EntityOptions,
            SearchResultType.Genres => [SearchArrangeOption.Unarranged],
            _ => [SearchArrangeOption.Unarranged]
        };
    }
}
