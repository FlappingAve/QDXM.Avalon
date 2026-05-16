using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.Core.Api;

public interface IQobuzClient
{
    Task<IReadOnlyList<SearchAlbumResult>> SearchAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchTrackResult>> SearchTracksAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchPlaylistResult>> SearchPlaylistsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<SearchPlaylistTrackPage> GetPlaylistTracksAsync(
        string playlistId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<SearchTrackResult> GetTrackAsync(
        string trackId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchArtistResult>> SearchArtistsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchAlbumResult>> SearchArtistAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SearchAlbumResult> SearchArtistAlbumsIncrementalAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchLabelResult>> SearchLabelsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchAlbumResult>> SearchLabelAlbumsAsync(
        SearchQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<SearchAlbumResult> GetAlbumTracksAsync(
        string albumId,
        CancellationToken cancellationToken = default);
}
