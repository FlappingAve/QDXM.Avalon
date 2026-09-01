using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.Core.Api;

public sealed class QobuzGenreStorefrontClient : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly QobuzGenreStorefrontParser parser;
    private readonly bool ownsHttpClient;

    public QobuzGenreStorefrontClient(
        HttpClient? httpClient = null,
        QobuzGenreStorefrontParser? parser = null)
    {
        this.httpClient = httpClient ?? QobuzStorefrontHttpClientFactory.Create();
        ownsHttpClient = httpClient is null;
        this.parser = parser ?? new QobuzGenreStorefrontParser();
    }

    public void Dispose()
    {
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    public async Task<IReadOnlyList<SearchAlbumResult>> SearchGenreAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!QobuzGenreStorefrontUrl.TryParse(options.Query, out var storefrontUrl))
        {
            return [];
        }

        var pageNumber = Math.Max(1, options.Offset + 1);
        var pageUrl = storefrontUrl.CreatePageUrl(pageNumber, options.GenreSort);
        var html = await httpClient.GetStringAsync(pageUrl, cancellationToken);
        return parser.ParseAlbums(html, new Uri(pageUrl));
    }

}
