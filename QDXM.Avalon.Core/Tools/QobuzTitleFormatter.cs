using System.Text.RegularExpressions;

namespace QDXM.Avalon.Core.Tools;

public static partial class QobuzTitleFormatter
{
    public static string AlbumTitle(string? title, string? fallback = "")
    {
        return string.IsNullOrWhiteSpace(title)
            ? fallback ?? string.Empty
            : title.Trim();
    }

    public static string TrackTitle(string? title, string? fallback = "")
    {
        return string.IsNullOrWhiteSpace(title)
            ? fallback ?? string.Empty
            : title.Trim();
    }

    public static bool ContainsNormalizedVersion(string title, string version)
    {
        var normalizedTitle = NormalizeVersionText(title);
        var normalizedVersion = NormalizeVersionText(version);
        return normalizedVersion.Length > 0 && normalizedTitle.Contains(normalizedVersion, StringComparison.Ordinal);
    }

    public static string NormalizeVersionText(string value)
    {
        return NonAlphaNumericRegex()
            .Replace(value.ToLowerInvariant(), " ")
            .Trim();
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();
}
