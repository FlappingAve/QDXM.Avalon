using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.Core.Api;

public static class QobuzGenreStorefrontSortMapper
{
    public const string BestSellersValue = "main_catalog";
    public const string MostAwardedValue = "main_catalog_awards";
    public const string PriceAscendingValue = "main_catalog_price_asc";
    public const string NewestValue = "main_catalog_date_desc";

    public static SearchGenreSortOption FromStorefrontValue(string? value)
    {
        return value switch
        {
            MostAwardedValue => SearchGenreSortOption.MostAwarded,
            PriceAscendingValue => SearchGenreSortOption.PriceAscending,
            NewestValue => SearchGenreSortOption.Newest,
            _ => SearchGenreSortOption.BestSellers
        };
    }

    public static string ToStorefrontValue(SearchGenreSortOption option)
    {
        return option switch
        {
            SearchGenreSortOption.MostAwarded => MostAwardedValue,
            SearchGenreSortOption.PriceAscending => PriceAscendingValue,
            SearchGenreSortOption.Newest => NewestValue,
            _ => BestSellersValue
        };
    }
}
