namespace QDXM.Avalon.Core.Search;

public sealed record SearchAlbumTrack(
    string TrackId,
    int TrackNumber,
    int DiscNumber,
    string Title,
    string Version,
    string Work,
    string Composer,
    TimeSpan Duration,
    string Quality,
    bool Explicit);
