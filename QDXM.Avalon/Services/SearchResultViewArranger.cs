using QDXM.Avalon.Core.Search;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Services;

public static class SearchResultViewArranger
{
    public static IReadOnlyList<SearchResultViewModel> Arrange(
        IEnumerable<SearchResultViewModel> results,
        SearchArrangeOption arrangeBy)
    {
        var arranged = arrangeBy switch
        {
            SearchArrangeOption.ReleaseDate => results.OrderByDescending(result => result.ReleaseDate),
            SearchArrangeOption.LastUpdated => results.OrderByDescending(result => result.ReleaseDate),
            SearchArrangeOption.Quality => results.OrderByDescending(result => SearchQualityRanker.Rank(result.Quality)),
            SearchArrangeOption.Name => results.OrderBy(result => result.Title, StringComparer.OrdinalIgnoreCase),
            SearchArrangeOption.TotalTracks => results.OrderByDescending(result => result.TotalTracks),
            SearchArrangeOption.TotalAlbums => results.OrderByDescending(result => result.TotalAlbums),
            _ => results
        };

        return arranged.ToList();
    }
}
