using QobuzApiSharp.Models.Content;

namespace QDXM.Avalon.Core.Tools;

public static class PlaylistImageUrlSelector
{
    public static string? GetBestImageUrl(Playlist playlist)
    {
        return FirstNonEmpty(
            playlist.ImageRectangle,
            playlist.ImageRectangleMini,
            playlist.Images300,
            playlist.Images150,
            playlist.Images);
    }

    private static string? FirstNonEmpty(params IEnumerable<string>?[] imageGroups)
    {
        foreach (var group in imageGroups)
        {
            var image = group?
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(image))
            {
                return image;
            }
        }

        return null;
    }
}
