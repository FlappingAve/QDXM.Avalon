namespace QDXM.Avalon.Core.Downloads;

public sealed record PartialAlbumDownloadRequest(
    string AlbumId,
    string AlbumUrl,
    IReadOnlyList<string> TrackIds,
    string? DisplayTitle = null,
    string? DisplayArtist = null);
