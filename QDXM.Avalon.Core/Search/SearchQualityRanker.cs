using System.Globalization;
using System.Text.RegularExpressions;

namespace QDXM.Avalon.Core.Search;

public static class SearchQualityRanker
{
    private static readonly Regex FlacQualityPattern = new(@"(?<bitDepth>\d+)\s*/\s*(?<samplingRate>\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex Mp3QualityPattern = new(@"MP3\s*(?<bitrate>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static int Rank(string quality)
    {
        var flacMatch = FlacQualityPattern.Match(quality);
        if (flacMatch.Success &&
            int.TryParse(flacMatch.Groups["bitDepth"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitDepth) &&
            decimal.TryParse(flacMatch.Groups["samplingRate"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var samplingRate))
        {
            return (bitDepth * 10000) + (int)(samplingRate * 10);
        }

        var mp3Match = Mp3QualityPattern.Match(quality);
        if (mp3Match.Success &&
            int.TryParse(mp3Match.Groups["bitrate"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitrate))
        {
            return bitrate;
        }

        return string.IsNullOrWhiteSpace(quality) ? 0 : 1;
    }
}
