namespace QDXM.Avalon.Core.Search;

public sealed record SearchTrackResult(
    string TrackId,
    string Title,
    string Version,
    string Artist,
    string AlbumTitle,
    string Quality,
    TimeSpan Duration,
    string ReleaseDate,
    string ThumbnailUrl,
    string WebPlayerUrl,
    string StoreUrl,
    bool Explicit);
