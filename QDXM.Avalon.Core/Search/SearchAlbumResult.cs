namespace QDXM.Avalon.Core.Search;

public sealed record SearchAlbumResult(
    string AlbumId,
    string Title,
    string Version,
    string Artist,
    string Quality,
    string ReleaseDate,
    string? Upc,
    string ThumbnailUrl,
    string WebPlayerUrl,
    string StoreUrl,
    int TotalTracks,
    int TotalDiscs,
    bool Explicit,
    IReadOnlyList<SearchAlbumTrack> Tracks);
