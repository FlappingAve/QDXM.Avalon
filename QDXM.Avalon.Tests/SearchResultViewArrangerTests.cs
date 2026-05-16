using QDXM.Avalon.Core.Search;
using QDXM.Avalon.Services;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class SearchResultViewArrangerTests
{
    [Fact]
    public void Arrange_PreservesInputOrderWhenUnarranged()
    {
        var results = new[]
        {
            CreateResult("C"),
            CreateResult("A"),
            CreateResult("B")
        };

        var arranged = SearchResultViewArranger.Arrange(
            results,
            SearchArrangeOption.Unarranged);

        Assert.Equal(["C", "A", "B"], arranged.Select(result => result.Title));
    }

    [Fact]
    public void Arrange_SortsAlbumLikeResultsByActualQualityMetadata()
    {
        var results = new[]
        {
            CreateResult("A", quality: "FLAC 16/44.1"),
            CreateResult("B", quality: "FLAC 24/96"),
            CreateResult("C", quality: "FLAC 24/176.4"),
            CreateResult("D", quality: "FLAC 24/88.2"),
            CreateResult("E", quality: "MP3 320")
        };

        var arranged = SearchResultViewArranger.Arrange(
            results,
            SearchArrangeOption.Quality);

        Assert.Equal(["C", "B", "D", "A", "E"], arranged.Select(result => result.Title));
    }

    [Fact]
    public void Arrange_SortsEntitiesByTotalAlbums()
    {
        var results = new[]
        {
            CreateResult("Small", totalAlbums: 2),
            CreateResult("Large", totalAlbums: 12),
            CreateResult("Medium", totalAlbums: 5)
        };

        var arranged = SearchResultViewArranger.Arrange(
            results,
            SearchArrangeOption.TotalAlbums);

        Assert.Equal(["Large", "Medium", "Small"], arranged.Select(result => result.Title));
    }

    [Fact]
    public void Arrange_SortsPlaylistsByLastUpdated()
    {
        var results = new[]
        {
            CreateResult("Older", releaseDate: "2024-01-01"),
            CreateResult("Newest", releaseDate: "2024-03-01"),
            CreateResult("Middle", releaseDate: "2024-02-01")
        };

        var arranged = SearchResultViewArranger.Arrange(
            results,
            SearchArrangeOption.LastUpdated);

        Assert.Equal(["Newest", "Middle", "Older"], arranged.Select(result => result.Title));
    }

    private static SearchResultViewModel CreateResult(
        string title,
        string quality = "",
        int totalAlbums = 0,
        string releaseDate = "")
    {
        return new SearchResultViewModel((_, _) => { })
        {
            Title = title,
            Quality = quality,
            TotalAlbums = totalAlbums,
            ReleaseDate = releaseDate
        };
    }
}
