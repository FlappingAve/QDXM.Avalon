namespace QDXM.Avalon.Core.Search;

public static class SearchQueryClassifier
{
    private const string IdPrefix = "id:";

    public static bool TryGetDirectAlbumId(SearchQueryOptions options, out string albumId)
    {
        albumId = GetIdCandidate(options.Query);

        return options.Type is SearchResultType.Albums
            && options.Offset == 0
            && albumId.Length > 0
            && (IsExplicitIdQuery(options.Query) || albumId.All(char.IsDigit));
    }

    public static bool TryGetDirectTrackId(SearchQueryOptions options, out string trackId)
    {
        trackId = GetIdCandidate(options.Query);

        return options.Type is SearchResultType.Tracks
            && options.Offset == 0
            && trackId.Length > 0
            && IsExplicitIdQuery(options.Query)
            && trackId.All(char.IsDigit);
    }

    public static bool TryGetNumericId(SearchQueryOptions options, out string id)
    {
        id = GetIdCandidate(options.Query);
        return id.Length > 0 && id.All(char.IsDigit);
    }

    public static bool IsExplicitIdQuery(string? query)
    {
        return query?.TrimStart().StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string GetIdCandidate(string query)
    {
        var trimmed = query.Trim();
        return IsExplicitIdQuery(trimmed)
            ? trimmed[IdPrefix.Length..].Trim()
            : trimmed;
    }
}
