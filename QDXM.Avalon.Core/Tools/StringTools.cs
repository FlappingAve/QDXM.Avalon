using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QDXM.Avalon.Core.Tools;

public static partial class StringTools
{
    public const int PlaylistTitleSegmentMaxLength = 64;

    public static string? DecodeEncodedNonAsciiCharacters(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return EncodedUnicodeRegex().Replace(
            value,
            match => ((char)int.Parse(match.Groups["Value"].Value, NumberStyles.HexNumber)).ToString());
    }

    public static string? GetSafeFilename(string? filename)
    {
        if (filename is null)
        {
            return null;
        }

        var result = TrailingDotsAndSpacesRegex().Replace(filename.Trim(), string.Empty);
        var safeName = string.Join(" ", result.Split(Path.GetInvalidFileNameChars()));
        return WhitespaceRegex().Replace(safeName, " ").Trim();
    }

    public static string GetSafePlaylistTitleSegment(string? title, string? playlistId)
    {
        var cleaned = SanitizePlaylistTitleSegment(title, PlaylistTitleSegmentMaxLength);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            return cleaned;
        }

        var fallback = string.IsNullOrWhiteSpace(playlistId)
            ? "Playlist"
            : $"Playlist {playlistId.Trim()}";

        return SanitizePlaylistTitleSegment(fallback, PlaylistTitleSegmentMaxLength) ?? "Playlist";
    }

    public static string TrimToMaxLength(string? text, int maxLength = 36)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = text.Trim();
        return result[..Math.Min(result.Length, maxLength)].Trim();
    }

    public static string FormatDateTimeOffset(DateTimeOffset? dateTimeOffset)
    {
        return dateTimeOffset?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    public static string FormatWholeOrSingleDecimal(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.01
            ? Math.Round(value).ToString("0")
            : value.ToString("0.#");
    }

    public static void AppendTreeLeaf(
        StringBuilder builder,
        IReadOnlyList<bool> ancestorHasMoreSiblings,
        string text,
        bool isLast)
    {
        foreach (var hasMoreSiblings in ancestorHasMoreSiblings)
        {
            builder.Append(hasMoreSiblings ? "\u2502  " : "   ");
        }

        builder.Append(isLast ? "\u2514\u2500 " : "\u251c\u2500 ");
        builder.AppendLine(text);
    }

    public static List<string> GetRelativeSegments(string baseFolder, string destination)
    {
        try
        {
            var relative = Path.GetRelativePath(baseFolder, destination);
            if (relative == ".")
            {
                return [];
            }

            if (!string.IsNullOrWhiteSpace(relative) &&
                !relative.StartsWith("..", StringComparison.Ordinal))
            {
                return SplitPathSegments(relative);
            }
        }
        catch
        {
            // Fall through to a simple destination split.
        }

        return SplitPathSegments(destination);
    }

    private static List<string> SplitPathSegments(string path)
    {
        return path
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static string? SanitizePlaylistTitleSegment(string? title, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var withoutTags = HtmlLikeTagRegex().Replace(title, " ");
        var withoutUnsafeUnicode = RemoveControlCharactersAndEmoji(withoutTags);
        var safe = GetSafeFilename(withoutUnsafeUnicode);
        if (string.IsNullOrWhiteSpace(safe))
        {
            return null;
        }

        safe = TrimToMaxLength(safe, maxLength);
        safe = GetSafeFilename(safe);

        return string.IsNullOrWhiteSpace(safe) ? null : safe;
    }

    private static string RemoveControlCharactersAndEmoji(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (RuneShouldBeRemovedFromPlaylistTitle(rune))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    private static bool RuneShouldBeRemovedFromPlaylistTitle(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.OtherSymbol or UnicodeCategory.Surrogate ||
            rune.Value is >= 0xFE00 and <= 0xFE0F ||
            rune.Value is >= 0x1F3FB and <= 0x1F3FF ||
            rune.Value == 0x20E3;
    }

    [GeneratedRegex(@"\\u(?<Value>[a-zA-Z0-9]{4})")]
    private static partial Regex EncodedUnicodeRegex();

    [GeneratedRegex(@"[. ]+$")]
    private static partial Regex TrailingDotsAndSpacesRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlLikeTagRegex();
}
