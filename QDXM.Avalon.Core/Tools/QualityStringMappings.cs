using QobuzApiSharp.Models.Content;

namespace QDXM.Avalon.Core.Tools;

public static class QualityStringMappings
{
    public const string FlacHighestFormatId = "27";
    public const string FlacHiResFormatId = "7";
    public const string FlacCdFormatId = "6";
    public const string Mp3FormatId = "5";
    public const string FlacHighestLabel = "FLAC (Highest Available)";
    public const string FlacUnknownQualityLabel = "FLAC (Unknown Quality)";
    public const string Mp3Label = "MP3 320";

    public static string GetFormatIdFromQualityLabel(string? qualityLabel)
    {
        return qualityLabel?.Trim().ToUpperInvariant() switch
        {
            "MP3 320" => Mp3FormatId,
            "FLAC (HIGHEST AVAILABLE)" => FlacHighestFormatId,
            _ => string.Empty
        };
    }

    public static string GetQualityLabelFromFormatId(string formatId)
    {
        return formatId switch
        {
            Mp3FormatId => Mp3Label,
            FlacHighestFormatId => FlacHighestLabel,
            _ => string.Empty
        };
    }

    public static string GetAudioExtension(string formatId)
    {
        return formatId == "5" ? ".mp3" : ".flac";
    }

    public static (string DisplayQuality, string PathQuality) GetEffectiveQuality(string formatId, Album album)
    {
        if (formatId == "5")
        {
            return (Mp3Label, "MP3");
        }

        var albumQuality = FormatFlacQuality(album.MaximumBitDepth, album.MaximumSamplingRate);
        return string.IsNullOrWhiteSpace(albumQuality.DisplayQuality)
            ? (FlacUnknownQualityLabel, FlacUnknownQualityLabel)
            : albumQuality;
    }

    public static AudioQualityDescriptor GetActualQuality(FileUrl fileUrl, string requestedFormatId)
    {
        var formatId = fileUrl.FormatId > 0
            ? fileUrl.FormatId.ToString()
            : requestedFormatId;
        var extension = GetAudioExtension(formatId);
        if (extension == ".mp3")
        {
            return new AudioQualityDescriptor(formatId, Mp3Label, "MP3", extension);
        }

        var quality = FormatFlacQuality(
            fileUrl.BitDepth > 0 ? fileUrl.BitDepth : null,
            fileUrl.SamplingRate > 0 ? fileUrl.SamplingRate : null);
        if (!string.IsNullOrWhiteSpace(quality.DisplayQuality))
        {
            return new AudioQualityDescriptor(formatId, quality.DisplayQuality, quality.PathQuality, extension);
        }

        var fallback = GetCandidateQuality(formatId);
        return new AudioQualityDescriptor(formatId, fallback.DisplayQuality, fallback.PathQuality, extension);
    }

    public static string GetCandidateQualityLabel(string formatId)
    {
        return GetCandidateQuality(formatId).DisplayQuality;
    }

    public static QualityDisplayText GetDisplayText(string? quality)
    {
        var full = quality ?? string.Empty;
        var compact = full.StartsWith("FLAC", StringComparison.OrdinalIgnoreCase)
            ? "FLAC"
            : full;

        return new QualityDisplayText(full, compact);
    }

    private static (string DisplayQuality, string PathQuality) GetCandidateQuality(string formatId)
    {
        return formatId switch
        {
            Mp3FormatId => (Mp3Label, "MP3"),
            FlacCdFormatId or FlacHiResFormatId or FlacHighestFormatId => (FlacUnknownQualityLabel, FlacUnknownQualityLabel),
            _ => ($"format {formatId}", $"format {formatId}")
        };
    }

    private static (string DisplayQuality, string PathQuality) FormatFlacQuality(double? bitDepth, double? sampleRate)
    {
        if (bitDepth is null || sampleRate is null)
        {
            return (string.Empty, string.Empty);
        }

        var bitDepthText = StringTools.FormatWholeOrSingleDecimal(bitDepth.Value);
        var sampleRateText = StringTools.FormatWholeOrSingleDecimal(sampleRate.Value);
        return ($"FLAC {bitDepthText}/{sampleRateText}", $"FLAC ({bitDepthText}bit-{sampleRateText}kHz)");
    }

}

public sealed record AudioQualityDescriptor(
    string FormatId,
    string DisplayQuality,
    string PathQuality,
    string Extension);

public sealed record QualityDisplayText(
    string Full,
    string Compact);
