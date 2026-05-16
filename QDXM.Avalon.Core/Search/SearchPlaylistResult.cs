namespace QDXM.Avalon.Core.Search;

public sealed record SearchPlaylistResult(
    string PlaylistId,
    string Title,
    string Owner,
    string UpdatedDate,
    string CreatedDate,
    TimeSpan Duration,
    string ThumbnailUrl,
    string WebPlayerUrl,
    int TotalTracks);
