namespace QDXM.Avalon.Core.Search;

public sealed record SearchPlaylistTrackResult(
    string TrackId,
    string SelectionKey,
    int PlaylistPosition,
    string PlaylistPositionDisplay,
    int AlbumTrackNumber,
    int AlbumDiscNumber,
    string AlbumPositionDisplay,
    string Title,
    string Version,
    string Artist,
    string AlbumTitle,
    TimeSpan Duration);
