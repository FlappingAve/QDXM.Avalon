using QobuzApiSharp.Models.Content;

namespace QDXM.Avalon.Core.Search;

public static class PlaylistTrackSelectionKey
{
    private const string PlaylistTrackIdPrefix = "playlist-track:";
    private const string PositionPrefix = "position:";
    private const string IndexPrefix = "index:";

    public static string Create(long? playlistTrackId, int? playlistPosition, int returnedIndex)
    {
        if (playlistTrackId is > 0)
        {
            return $"{PlaylistTrackIdPrefix}{playlistTrackId.Value}";
        }

        if (playlistPosition is > 0)
        {
            return $"{PositionPrefix}{playlistPosition.Value}";
        }

        return $"{IndexPrefix}{returnedIndex}";
    }

    public static bool Matches(Track track, int returnedIndex, string selectionKey)
    {
        if (string.IsNullOrWhiteSpace(selectionKey))
        {
            return false;
        }

        return string.Equals(
            Create(track.PlaylistTrackId, track.PlaylistPosition, returnedIndex),
            selectionKey,
            StringComparison.Ordinal);
    }
}
