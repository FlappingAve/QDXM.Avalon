namespace QDXM.Avalon.Core.Search;

public sealed record SearchQueryOptions(
    string Query,
    SearchResultType Type = SearchResultType.Albums,
    SearchArtistAlbumSortOption ArtistAlbumSort = SearchArtistAlbumSortOption.Newestish,
    int Limit = 25,
    int Offset = 0,
    SearchGenreSortOption GenreSort = SearchGenreSortOption.BestSellers)
{
    public int NormalizedLimit => SearchPageSizeOptions.ClampLimit(Type, Limit);
}
