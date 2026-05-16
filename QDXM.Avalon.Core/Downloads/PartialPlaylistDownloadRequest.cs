namespace QDXM.Avalon.Core.Downloads;

public sealed record PartialPlaylistDownloadRequest(
    string PlaylistId,
    string PlaylistUrl,
    IReadOnlyList<string> TrackSelectionKeys,
    string? DisplayTitle = null,
    string? DisplayOwner = null,
    string? CoverArtUrl = null);
