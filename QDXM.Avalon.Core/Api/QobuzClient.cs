using QobuzApiSharp.Service;
using QDXM.Avalon.Core.Search;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace QDXM.Avalon.Core.Api;

public sealed class QobuzClient : IQobuzClient, IDisposable
{
    private readonly QobuzApiServiceFactory serviceFactory;
    private readonly QobuzStorefrontSearchConfigProvider storefrontSearchConfigProvider;
    private readonly Lazy<QobuzApiService> service;
    private readonly Lazy<HttpClient> labelSearchClient = new(QobuzStorefrontHttpClientFactory.Create);
    private readonly Lazy<QobuzGenreStorefrontClient> genreStorefrontClient = new(() => new QobuzGenreStorefrontClient());

    public QobuzClient(
        QobuzApiServiceFactory? serviceFactory = null,
        QobuzStorefrontSearchConfigProvider? storefrontSearchConfigProvider = null)
    {
        this.serviceFactory = serviceFactory ?? new QobuzApiServiceFactory();
        this.storefrontSearchConfigProvider = storefrontSearchConfigProvider ?? new QobuzStorefrontSearchConfigProvider();
        service = new Lazy<QobuzApiService>(this.serviceFactory.Create);
    }

    public Task<IReadOnlyList<SearchAlbumResult>> SearchAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = service.Value.SearchAlbums(
                options.Query,
                options.NormalizedLimit,
                options.Offset,
                serviceFactory.HasUserAuthToken);

            return (IReadOnlyList<SearchAlbumResult>)(result.Albums?.Items?
                .Where(album => album is not null)
                .Select(QobuzApiSearchMapper.ToAlbumResult)
                .ToList() ?? []);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SearchTrackResult>> SearchTracksAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = service.Value.SearchTracks(
                options.Query,
                options.NormalizedLimit,
                options.Offset,
                serviceFactory.HasUserAuthToken);

            return (IReadOnlyList<SearchTrackResult>)(result.Tracks?.Items?
                .Where(track => track is not null)
                .Select(QobuzApiSearchMapper.ToTrackResult)
                .ToList() ?? []);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SearchPlaylistResult>> SearchPlaylistsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = service.Value.SearchPlaylists(
                options.Query,
                options.NormalizedLimit,
                options.Offset,
                serviceFactory.HasUserAuthToken);

            return (IReadOnlyList<SearchPlaylistResult>)(result.Playlists?.Items?
                .Where(playlist => playlist is not null)
                .Select(QobuzApiSearchMapper.ToPlaylistResult)
                .ToList() ?? []);
        }, cancellationToken);
    }

    public Task<SearchPlaylistTrackPage> GetPlaylistTracksAsync(
        string playlistId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            throw new ArgumentException("Playlist ID is required.", nameof(playlistId));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var playlist = service.Value.GetPlaylist(
                playlistId.Trim(),
                serviceFactory.HasUserAuthToken,
                extra: "tracks",
                limit: Math.Clamp(limit, 1, QobuzApiLimits.PlaylistTrackPageSize),
                offset: Math.Max(0, offset));

            return QobuzApiSearchMapper.ToPlaylistTrackPage(playlist, Math.Max(0, offset));
        }, cancellationToken);
    }

    public Task<SearchTrackResult> GetTrackAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            throw new ArgumentException("Track ID is required.", nameof(trackId));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var track = service.Value.GetTrack(
                trackId.Trim(),
                serviceFactory.HasUserAuthToken);

            return QobuzApiSearchMapper.ToTrackResult(track);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SearchArtistResult>> SearchArtistsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = service.Value.SearchArtists(
                options.Query,
                options.NormalizedLimit,
                options.Offset,
                serviceFactory.HasUserAuthToken);

            return (IReadOnlyList<SearchArtistResult>)(result.Artists?.Items?
                .Where(artist => artist is not null)
                .Select(QobuzApiSearchMapper.ToArtistResult)
                .ToList() ?? []);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SearchAlbumResult>> SearchArtistAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SearchQueryClassifier.TryGetNumericId(options, out var artistId))
            {
                return (IReadOnlyList<SearchAlbumResult>)[];
            }

            var api = service.Value;
            var result = api.GetReleaseList(
                artistId,
                serviceFactory.HasUserAuthToken,
                release_type: "all",
                sort: GetArtistAlbumSort(options.ArtistAlbumSort),
                track_size: 0,
                limit: options.NormalizedLimit,
                offset: options.Offset);

            return (IReadOnlyList<SearchAlbumResult>)(result.Items?
                .Where(release => release is not null)
                .Select(release => ToHydratedAlbumResult(api, release))
                .ToList() ?? []);
        }, cancellationToken);
    }

    public async IAsyncEnumerable<SearchAlbumResult> SearchArtistAlbumsIncrementalAsync(
        SearchQueryOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!SearchQueryClassifier.TryGetNumericId(options, out var artistId))
        {
            yield break;
        }

        var api = service.Value;
        var result = await Task.Run(() => api.GetReleaseList(
            artistId,
            serviceFactory.HasUserAuthToken,
            release_type: "all",
            sort: GetArtistAlbumSort(options.ArtistAlbumSort),
            track_size: 0,
            limit: options.NormalizedLimit,
            offset: options.Offset), cancellationToken);

        foreach (var release in result.Items?.Where(release => release is not null) ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await Task.Run(() => ToHydratedAlbumResult(api, release), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SearchLabelResult>> SearchLabelsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var limit = options.NormalizedLimit;
        var page = Math.Max(0, options.Offset / limit);
        var request = new AlgoliaLabelSearchRequest(
            $"query={Uri.EscapeDataString(options.Query)}&hitsPerPage={limit}&page={page}");
        var config = await storefrontSearchConfigProvider.GetConfigAsync(cancellationToken);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.LabelsEndpoint)
        {
            Content = JsonContent.Create(request)
        };
        requestMessage.Headers.Add("X-Algolia-Application-Id", config.ApplicationId);
        requestMessage.Headers.Add("X-Algolia-API-Key", config.ApiKey);

        using var response = await labelSearchClient.Value.SendAsync(
            requestMessage,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AlgoliaLabelSearchResponse>(
            cancellationToken: cancellationToken);

        return result?.Hits?
            .Where(hit => hit is not null)
            .Select(hit => new SearchLabelResult(
                LabelId: hit.ObjectId ?? string.Empty,
                Name: hit.Name?.FirstOrDefault() ?? string.Empty,
                Slug: hit.Slug ?? string.Empty,
                WebPlayerUrl: string.IsNullOrWhiteSpace(hit.ObjectId)
                    ? string.Empty
                    : QobuzUrlBuilder.CreateOpenUrl("label", hit.ObjectId),
                AlbumsCount: hit.AlbumsCount ?? 0))
            .ToList() ?? [];
    }

    public Task<IReadOnlyList<SearchAlbumResult>> SearchLabelAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SearchQueryClassifier.TryGetNumericId(options, out var labelId))
            {
                return (IReadOnlyList<SearchAlbumResult>)[];
            }

            var label = service.Value.GetLabel(
                labelId,
                serviceFactory.HasUserAuthToken,
                extra: "albums",
                options.NormalizedLimit,
                options.Offset);

            return (IReadOnlyList<SearchAlbumResult>)(label.Albums?.Items?
                .Where(album => album is not null)
                .Select(QobuzApiSearchMapper.ToAlbumResult)
                .ToList() ?? []);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SearchAlbumResult>> SearchGenreAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        return genreStorefrontClient.Value.SearchGenreAlbumsAsync(options, cancellationToken);
    }

    public Task<SearchAlbumResult> GetAlbumTracksAsync(
        string albumId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            throw new ArgumentException("Album ID is required.", nameof(albumId));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var album = QobuzPagination.FetchAlbumWithAllTracks(
                (limit, offset) => service.Value.GetAlbum(
                    albumId,
                    serviceFactory.HasUserAuthToken,
                    extra: "track_ids",
                    limit: limit,
                    offset: offset));

            return QobuzApiSearchMapper.ToAlbumResult(album);
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (service.IsValueCreated)
        {
            service.Value.Dispose();
        }

        if (labelSearchClient.IsValueCreated)
        {
            labelSearchClient.Value.Dispose();
        }

        if (genreStorefrontClient.IsValueCreated)
        {
            genreStorefrontClient.Value.Dispose();
        }
    }

    private static string GetArtistAlbumSort(SearchArtistAlbumSortOption sort)
    {
        return sort switch
        {
            SearchArtistAlbumSortOption.Relevance => "relevant",
            _ => "release_date_by_priority"
        };
    }

    private SearchAlbumResult ToHydratedAlbumResult(QobuzApiService api, QobuzApiSharp.Models.Content.Release release)
    {
        if (string.IsNullOrWhiteSpace(release.Id))
        {
            return QobuzApiSearchMapper.ToAlbumResult(release);
        }

        try
        {
            var album = api.GetAlbum(
                release.Id,
                serviceFactory.HasUserAuthToken);

            return QobuzApiSearchMapper.ToAlbumResult(album);
        }
        catch
        {
            return QobuzApiSearchMapper.ToAlbumResult(release);
        }
    }

    private sealed record AlgoliaLabelSearchRequest(string Params);

    private sealed class AlgoliaLabelSearchResponse
    {
        [JsonPropertyName("hits")]
        public List<AlgoliaLabelHit>? Hits { get; set; }
    }

    private sealed class AlgoliaLabelHit
    {
        [JsonPropertyName("objectID")]
        public string? ObjectId { get; set; }

        [JsonPropertyName("name")]
        public List<string>? Name { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("albums_count")]
        public int? AlbumsCount { get; set; }
    }
}
