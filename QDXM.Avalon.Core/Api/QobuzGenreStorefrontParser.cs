using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.Core.Api;

public sealed partial class QobuzGenreStorefrontParser
{
    private static readonly HtmlParser Parser = new();

    public IReadOnlyList<SearchAlbumResult> ParseAlbums(string html, Uri pageUri)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        using var document = Parser.ParseDocument(html);
        return document.QuerySelectorAll(".product__item")
            .Select(item => ParseAlbum(item, pageUri))
            .Where(album => album is not null)
            .Cast<SearchAlbumResult>()
            .ToList();
    }

    private static SearchAlbumResult? ParseAlbum(IElement item, Uri pageUri)
    {
        var albumLink = item.QuerySelector("a[href*='/album/']");
        var href = albumLink?.GetAttribute("href") ?? string.Empty;
        var storeUrl = CreateAbsoluteUrl(pageUri, href);
        var albumId = GetAlbumId(item, storeUrl);
        var title = CleanText(item.QuerySelector(".product__name")?.GetAttribute("data-title")) ??
            CleanText(item.QuerySelector(".product__name")?.TextContent) ??
            CleanText(albumLink?.GetAttribute("title")) ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(albumId) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new SearchAlbumResult(
            AlbumId: albumId,
            Title: title,
            Version: string.Empty,
            Artist: CleanText(item.QuerySelector(".product__artist a")?.TextContent) ?? string.Empty,
            Quality: GetQuality(item),
            ReleaseDate: CleanText(item.QuerySelector(".product__data--release")?.TextContent) ?? string.Empty,
            Upc: null,
            ThumbnailUrl: CreateAbsoluteUrl(
                pageUri,
                item.QuerySelector(".product__cover")?.GetAttribute("data-src") ?? string.Empty),
            WebPlayerUrl: QobuzUrlBuilder.CreateOpenUrl("album", albumId),
            StoreUrl: storeUrl,
            TotalTracks: 0,
            TotalDiscs: 0,
            Explicit: false,
            Tracks: []);
    }

    private static string GetAlbumId(IElement item, string storeUrl)
    {
        var itemId = item.QuerySelector("[data-itemId]")?.GetAttribute("data-itemId");
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            return itemId.Trim();
        }

        if (!Uri.TryCreate(storeUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var segment = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        return segment?.Trim() ?? string.Empty;
    }

    private static string GetQuality(IElement item)
    {
        var qualityText = item.QuerySelectorAll(".album-quality__info")
            .Select(info => CleanText(info.TextContent))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Cast<string>()
            .ToList();

        var bitRate = qualityText.FirstOrDefault(text => text.Contains("bit", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("khz", StringComparison.OrdinalIgnoreCase));
        if (bitRate is null)
        {
            return string.Join(" ", qualityText);
        }

        var match = QualityRegex().Match(bitRate);
        if (!match.Success)
        {
            return bitRate;
        }

        return $"FLAC {match.Groups["Bits"].Value}/{match.Groups["Rate"].Value.Replace(',', '.')}";
    }

    private static string CreateAbsoluteUrl(Uri pageUri, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Uri.TryCreate(pageUri, value.Trim(), out var uri)
            ? uri.ToString()
            : value.Trim();
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(value, " ").Trim();
    }

    [GeneratedRegex(@"(?<Bits>\d+)\s*-\s*bit\s*/\s*(?<Rate>\d+(?:[\.,]\d+)?)\s*kHz", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualityRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
