using System.Text.RegularExpressions;
using QobuzApiSharp.Models.Content;

namespace QDXM.Avalon.Core.Tools;

public static partial class CoverArtUrlSelector
{
    public const string OriginalDisplayName = "Original (Large File Size!)";
    public const string MaxDisplayName = "Max (Large File Size!)";
    public const string RecommendedDisplayName = "600 px (Recommended)";
    private static readonly string[] FallbackArtSizes = ["org", "max", "600", "300", "230", "150", "100", "50"];

    public static string GetBestImageUrl(Image? image, string? fallback = null)
    {
        return image?.Mega ??
            image?.Extralarge ??
            image?.Large ??
            image?.Medium ??
            image?.Small ??
            image?.Thumbnail ??
            fallback ??
            string.Empty;
    }

    public static string GetImageUrlForSize(Image? image, string? artSize, string? fallback = null)
    {
        var normalizedSize = NormalizeArtSize(artSize);

        var directUrl = normalizedSize switch
        {
            "600" => image?.Large,
            "230" => image?.Small,
            "50" => image?.Thumbnail,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(directUrl))
        {
            return directUrl;
        }

        return TryReplaceImageSize(GetBestImageUrl(image, fallback), normalizedSize);
    }

    public static string GetImageUrlForSize(string? imageUrl, string? artSize)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        return TryReplaceImageSize(imageUrl, NormalizeArtSize(artSize));
    }

    public static string NormalizeArtSize(string? artSize)
    {
        if (string.IsNullOrWhiteSpace(artSize))
        {
            return "600";
        }

        var trimmed = artSize.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower is "org" or "original" || lower.Contains("original"))
        {
            return "org";
        }

        if (lower is "max" or "maximum" || lower.Contains("max"))
        {
            return "max";
        }

        if (lower is "large")
        {
            return "600";
        }

        if (lower is "small")
        {
            return "230";
        }

        if (lower is "thumbnail")
        {
            return "50";
        }

        var sizeMatch = ArtSizeNumberRegex().Match(trimmed);
        return sizeMatch.Success ? sizeMatch.Value : "600";
    }

    public static string GetArtSizeDisplayName(string? artSize)
    {
        return NormalizeArtSize(artSize) switch
        {
            "org" => OriginalDisplayName,
            "max" => MaxDisplayName,
            "300" => "300 px",
            "230" => "230 px",
            "150" => "150 px",
            "100" => "100 px",
            "50" => "50 px",
            _ => RecommendedDisplayName
        };
    }

    public static IReadOnlyList<string> GetFallbackArtSizes(string? artSize)
    {
        var normalizedSize = NormalizeArtSize(artSize);
        var startIndex = Array.IndexOf(FallbackArtSizes, normalizedSize);
        if (startIndex < 0)
        {
            startIndex = Array.IndexOf(FallbackArtSizes, "600");
        }

        return FallbackArtSizes
            .Skip(startIndex)
            .ToArray();
    }

    private static string TryReplaceImageSize(string imageUrl, string artSize)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        return QobuzImageSizeRegex().Replace(imageUrl, $"_{artSize}", 1);
    }

    [GeneratedRegex(@"_(?:org|max|\d+)(?=\.[a-z0-9]+(?:[?#].*)?$)", RegexOptions.IgnoreCase)]
    private static partial Regex QobuzImageSizeRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex ArtSizeNumberRegex();
}
