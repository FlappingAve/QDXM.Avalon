using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.Core.Api;

public sealed record QobuzGenreStorefrontUrl(string RootUrl, SearchGenreSortOption Sort)
{
    private const string GenreSegment = "/genre/";
    private const string AlbumsSegment = "/download-streaming-albums";

    public static bool TryParse(string? value, out QobuzGenreStorefrontUrl result)
    {
        result = new QobuzGenreStorefrontUrl(string.Empty, SearchGenreSortOption.BestSellers);
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "www.qobuz.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = NormalizePath(uri.AbsolutePath);
        if (!path.Contains(GenreSegment, StringComparison.OrdinalIgnoreCase) ||
            !path.Contains(AlbumsSegment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var albumsIndex = path.IndexOf(AlbumsSegment, StringComparison.OrdinalIgnoreCase);
        var rootPath = path[..(albumsIndex + AlbumsSegment.Length)];
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var rootBuilder = new UriBuilder(uri.Scheme, uri.Host)
        {
            Path = rootPath,
            Query = string.Empty
        };
        result = new QobuzGenreStorefrontUrl(
            rootBuilder.Uri.ToString().TrimEnd('/'),
            QobuzGenreStorefrontSortMapper.FromStorefrontValue(GetSortValue(uri.Query)));
        return true;
    }

    public string CreatePageUrl(int pageNumber, SearchGenreSortOption sort)
    {
        var page = Math.Max(1, pageNumber);
        var path = page == 1
            ? RootUrl
            : $"{RootUrl}/page/{page}";
        return $"{path}?ssf%5BsortBy%5D={Uri.EscapeDataString(QobuzGenreStorefrontSortMapper.ToStorefrontValue(sort))}";
    }

    private static string NormalizePath(string path)
    {
        var result = path.Replace('\\', '/');
        while (result.Contains("//", StringComparison.Ordinal))
        {
            result = result.Replace("//", "/", StringComparison.Ordinal);
        }

        return result.TrimEnd('/');
    }

    private static string? GetSortValue(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pieces[0]);
            if (!string.Equals(key, "ssf[sortBy]", StringComparison.Ordinal))
            {
                continue;
            }

            return pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : string.Empty;
        }

        return null;
    }
}
