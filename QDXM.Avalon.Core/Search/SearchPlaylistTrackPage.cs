namespace QDXM.Avalon.Core.Search;

public sealed record SearchPlaylistTrackPage(
    int TotalTracks,
    IReadOnlyList<SearchPlaylistTrackResult> Tracks);
