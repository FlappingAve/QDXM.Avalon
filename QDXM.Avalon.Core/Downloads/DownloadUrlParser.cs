using System.Text.RegularExpressions;
using QDXM.Avalon.Core.Protocol;

namespace QDXM.Avalon.Core.Downloads;

public static partial class DownloadUrlParser
{
    private static readonly Regex[] DownloadUrlRegexes =
    [
        SimpleOpenUrlRegex(),
        StoreAlbumUrlRegex(),
        StoreUrlRegex()
    ];

    private static readonly HashSet<string> LinkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "album",
        "track",
        "artist",
        "label",
        "playlist",
        "playlists",
        "interpreter"
    };

    public static bool TryParseDownloadUrl(string? downloadUrl, out DownloadRequest? request)
    {
        request = null;

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return false;
        }

        var normalizedUrl = ProtocolHandler.IsProtocolUrl(downloadUrl)
            ? ProtocolHandler.ConvertProtocolUrl(downloadUrl)
            : downloadUrl.Trim();

        var favoriteMatch = FavoriteUrlRegex().Match(normalizedUrl);
        if (favoriteMatch.Success)
        {
            request = new DownloadRequest(
                normalizedUrl,
                DownloadContentType.Favorites,
                favoriteMatch.Groups["FavoriteType"].Value.ToLowerInvariant());
            return true;
        }

        foreach (var regex in DownloadUrlRegexes)
        {
            var match = regex.Match(normalizedUrl);

            if (!match.Success)
            {
                continue;
            }

            var type = match.Groups["Type"].Value;
            var id = match.Groups["id"].Value.TrimEnd('/');

            if (!LinkTypes.Contains(type) || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            request = new DownloadRequest(
                normalizedUrl,
                MapContentType(type),
                id);
            return true;
        }

        return false;
    }

    private static DownloadContentType MapContentType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "album" => DownloadContentType.Album,
            "track" => DownloadContentType.Track,
            "playlist" or "playlists" => DownloadContentType.Playlist,
            "artist" or "interpreter" => DownloadContentType.Artist,
            "label" => DownloadContentType.Label,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported Qobuz URL type.")
        };
    }

    [GeneratedRegex(@"https:\/\/play\.qobuz\.com\/user\/library\/favorites\/(?<FavoriteType>albums|tracks)\/?(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex FavoriteUrlRegex();

    [GeneratedRegex(@"https:\/\/(?:.*?\.)?qobuz\.com\/(?<Type>.*?)\/(?<id>.*?)(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SimpleOpenUrlRegex();

    [GeneratedRegex(@"https:\/\/(?:.*?\.)?qobuz\.com\/(?:.*?)\/(?<Type>.*?)\/(?<Slug>.*?)\/(?<AlbumsTag>download-streaming-albums)\/(?<id>.*?)(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex StoreAlbumUrlRegex();

    [GeneratedRegex(@"https:\/\/(?:.*?\.)?qobuz\.com\/(?:.*?)\/(?<Type>.*?)\/(?<Slug>.*?)\/(?<id>.*?)(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex StoreUrlRegex();
}
