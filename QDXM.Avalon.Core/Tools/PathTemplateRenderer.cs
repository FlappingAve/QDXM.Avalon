using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Settings;

namespace QDXM.Avalon.Core.Tools;

public static partial class PathTemplateRenderer
{
    private const string DefaultFolderTemplate = AppSettings.DefaultFolderTemplate;
    private const string DefaultFilenameTemplate = AppSettings.DefaultFilenameTemplate;
    private const string DefaultDiscFolderTemplate = AppSettings.DefaultDiscFolderTemplate;
    private const string DefaultPlaylistFolderTemplate = AppSettings.DefaultPlaylistFolderTemplate;
    private const string DefaultPlaylistFilenameTemplate = AppSettings.DefaultPlaylistFilenameTemplate;
    private static readonly ConcurrentDictionary<string, IReadOnlyList<SegmentToken>> SegmentTokenCache = new(StringComparer.Ordinal);

    public static string RenderAlbumDestination(
        string baseFolder,
        string? folderTemplate,
        Album album,
        string albumArtist,
        string albumTitle,
        string quality)
    {
        var values = BuildValues(album, null, albumArtist, albumTitle, quality, totalTracks: album.TracksCount ?? 0);
        return RenderDestination(baseFolder, folderTemplate, DefaultFolderTemplate, "Unknown Album", values);
    }

    public static string RenderPlaylistDestination(
        string baseFolder,
        string? playlistFolderTemplate,
        string? playlistId,
        string? playlistTitle,
        string? playlistOwner,
        Track? track,
        Album? album,
        string albumArtist,
        string albumTitle,
        string quality,
        int playlistNumber,
        int playlistTotalTracks)
    {
        var values = BuildPlaylistValues(
            playlistId,
            playlistTitle,
            playlistOwner,
            track,
            album,
            albumArtist,
            albumTitle,
            quality,
            playlistNumber,
            playlistTotalTracks);

        return RenderDestination(baseFolder, playlistFolderTemplate, DefaultPlaylistFolderTemplate, "Playlist", values);
    }

    public static string RenderAlbumDestinationPreview(
        string baseFolder,
        string? folderTemplate,
        string albumArtist,
        string albumTitle,
        string quality,
        string releaseDate,
        int totalTracks,
        string version = "",
        string releaseType = "",
        string label = "",
        string upc = "",
        int totalDiscs = 0,
        bool explicitAdvisory = false)
    {
        var values = BuildPreviewValues(
            albumArtist,
            albumTitle,
            quality,
            releaseDate,
            totalTracks,
            trackNumber: 0,
            trackTitle: string.Empty,
            version,
            discNumber: 0,
            totalDiscs,
            releaseType,
            label,
            upc,
            explicitAdvisory: explicitAdvisory);

        return RenderDestination(baseFolder, folderTemplate, DefaultFolderTemplate, "Unknown Album", values);
    }

    private static string RenderDestination(
        string baseFolder,
        string? folderTemplate,
        string defaultFolderTemplate,
        string fallbackFolderName,
        IReadOnlyDictionary<string, string> values)
    {
        var template = GetFolderTemplate(folderTemplate, defaultFolderTemplate);
        if (string.IsNullOrWhiteSpace(template))
        {
            return baseFolder;
        }

        var segments = template
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => RenderSegment(segment, values))
            .Select(segment => StringTools.GetSafeFilename(segment))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Cast<string>()
            .ToList();

        if (segments.Count == 0)
        {
            segments.Add(StringTools.GetSafeFilename(RenderSegment(defaultFolderTemplate, values)) ?? fallbackFolderName);
        }

        return Path.Combine([baseFolder, .. segments]);
    }

    public static string RenderAudioFilename(
        string? filenameTemplate,
        Track track,
        Album album,
        string albumArtist,
        string albumTitle,
        string quality,
        int totalTracks,
        string extension,
        int maxFileNameLength,
        int? trackNumberPaddingWidth = null)
    {
        var values = BuildValues(album, track, albumArtist, albumTitle, quality, totalTracks, trackNumberPaddingWidth);
        var template = string.IsNullOrWhiteSpace(filenameTemplate)
            ? DefaultFilenameTemplate
            : filenameTemplate.Trim();

        var rendered = RenderSegment(template, values);
        if (string.IsNullOrWhiteSpace(rendered))
        {
            rendered = GetFallbackTrackFilename(values);
        }

        var safeName = StringTools.GetSafeFilename(rendered) ?? "Track";
        var maxBaseLength = Math.Max(1, maxFileNameLength - extension.Length);
        safeName = StringTools.TrimToMaxLength(safeName, maxBaseLength);

        return safeName + extension;
    }

    public static string RenderPlaylistAudioFilename(
        string? playlistFilenameTemplate,
        Track track,
        Album album,
        string albumArtist,
        string albumTitle,
        string quality,
        string? playlistId,
        string? playlistTitle,
        string? playlistOwner,
        int playlistNumber,
        int playlistTotalTracks,
        string extension,
        int maxFileNameLength)
    {
        var values = BuildPlaylistValues(
            playlistId,
            playlistTitle,
            playlistOwner,
            track,
            album,
            albumArtist,
            albumTitle,
            quality,
            playlistNumber,
            playlistTotalTracks);
        var template = string.IsNullOrWhiteSpace(playlistFilenameTemplate)
            ? DefaultPlaylistFilenameTemplate
            : playlistFilenameTemplate.Trim();

        var rendered = RenderSegment(template, values);
        if (string.IsNullOrWhiteSpace(rendered))
        {
            rendered = $"{values["PlaylistNumberPadded"]} - {values["TrackTitle"]}";
        }

        var safeName = StringTools.GetSafeFilename(rendered) ?? "Track";
        var maxBaseLength = Math.Max(1, maxFileNameLength - extension.Length);
        safeName = StringTools.TrimToMaxLength(safeName, maxBaseLength);

        return safeName + extension;
    }

    public static string RenderAudioFilenamePreview(
        string? filenameTemplate,
        string albumArtist,
        string albumTitle,
        string quality,
        string releaseDate,
        int totalTracks,
        int trackNumber,
        string trackTitle,
        string version,
        int discNumber,
        int totalDiscs,
        string extension,
        int maxFileNameLength,
        string releaseType = "",
        string label = "",
        string upc = "",
        string isrc = "",
        bool explicitAdvisory = false,
        int? trackNumberPaddingWidth = null)
    {
        var values = BuildPreviewValues(
            albumArtist,
            albumTitle,
            quality,
            releaseDate,
            totalTracks,
            trackNumber,
            trackTitle,
            version,
            discNumber,
            totalDiscs,
            releaseType,
            label,
            upc,
            isrc,
            explicitAdvisory,
            trackNumberPaddingWidth: trackNumberPaddingWidth);
        var template = string.IsNullOrWhiteSpace(filenameTemplate)
            ? DefaultFilenameTemplate
            : filenameTemplate.Trim();

        var rendered = RenderSegment(template, values);
        if (string.IsNullOrWhiteSpace(rendered))
        {
            rendered = GetFallbackTrackFilename(values);
        }

        var safeName = StringTools.GetSafeFilename(rendered) ?? "Track";
        var maxBaseLength = Math.Max(1, maxFileNameLength - extension.Length);
        safeName = StringTools.TrimToMaxLength(safeName, maxBaseLength);

        return safeName + extension;
    }

    private static string GetFallbackTrackFilename(IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue("TrackNumberPadded", out var trackNumber);
        values.TryGetValue("TrackTitle", out var trackTitle);

        if (string.IsNullOrWhiteSpace(trackTitle))
        {
            trackTitle = "Track";
        }

        return string.IsNullOrWhiteSpace(trackNumber)
            ? trackTitle
            : $"{trackNumber} - {trackTitle}";
    }

    public static IReadOnlyList<string> RenderDiscFolderSegments(
        string? discFolderTemplate,
        string? workHandling,
        string? workSeparator,
        bool workSeparatorNoSpaces,
        Track track,
        Album album,
        string albumArtist,
        string albumTitle,
        string quality,
        int totalTracks,
        int? trackNumberPaddingWidth = null)
    {
        var values = BuildValues(album, track, albumArtist, albumTitle, quality, totalTracks);
        var composerByWork = GetFirstComposersByWork(album.Tracks?.Items, track.Work, GetTrackComposer(track));
        var works = GetWorksForDisc(album.Tracks?.Items, track.MediaNumber ?? 0, track.Work);
        return RenderDiscFolderSegments(
            discFolderTemplate,
            workHandling,
            workSeparator,
            workSeparatorNoSpaces,
            values,
            works,
            track.Work,
            composerByWork);
    }

    public static IReadOnlyList<string> RenderDiscFolderSegmentsPreview(
        string? discFolderTemplate,
        string? workHandling,
        string? workSeparator,
        bool workSeparatorNoSpaces,
        string albumArtist,
        string albumTitle,
        string quality,
        string releaseDate,
        int totalTracks,
        int trackNumber,
        string trackTitle,
        string version,
        int discNumber,
        int totalDiscs,
        IReadOnlyList<string>? works,
        string? currentWork,
        string workComposer = "Example Work Composer")
    {
        var values = BuildPreviewValues(
            albumArtist,
            albumTitle,
            quality,
            releaseDate,
            totalTracks,
            trackNumber,
            trackTitle,
            version,
            discNumber,
            totalDiscs,
            work: currentWork ?? string.Empty);
        var composerByWork = NormalizeWorks(works, currentWork)
            .ToDictionary(work => work, _ => workComposer, StringComparer.OrdinalIgnoreCase);

        return RenderDiscFolderSegments(
            discFolderTemplate,
            workHandling,
            workSeparator,
            workSeparatorNoSpaces,
            values,
            NormalizeWorks(works, currentWork),
            currentWork,
            composerByWork);
    }

    public static string FormatQualityForPath(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
        {
            return string.Empty;
        }

        var flacMatch = FlacQualityRegex().Match(quality);
        if (flacMatch.Success)
        {
            return $"FLAC ({flacMatch.Groups["BitDepth"].Value}bit-{flacMatch.Groups["SampleRate"].Value}kHz)";
        }

        var mp3Match = Mp3QualityRegex().Match(quality);
        if (mp3Match.Success)
        {
            return $"MP3 ({mp3Match.Groups["Bitrate"].Value}kbps)";
        }

        return quality.Trim();
    }

    public static int GetTrackNumberPaddingWidth(
        IReadOnlyList<(int TrackNumber, int DiscNumber)> tracks,
        int currentDiscNumber,
        string? discFolderTemplate)
    {
        if (tracks.Count == 0)
        {
            return 2;
        }

        var useDiscScope = ShouldUseDiscScopedTrackPadding(tracks, discFolderTemplate);
        var scope = useDiscScope && currentDiscNumber > 0
            ? tracks.Where(track => track.DiscNumber == currentDiscNumber).ToArray()
            : tracks;
        var highestTrackNumber = scope
            .Select(track => track.TrackNumber)
            .DefaultIfEmpty(0)
            .Max();

        return GetTrackNumberWidth(scope.Count, highestTrackNumber);
    }

    private static IReadOnlyList<string> RenderDiscFolderSegments(
        string? discFolderTemplate,
        string? workHandling,
        string? workSeparator,
        bool workSeparatorNoSpaces,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> works,
        string? currentWork,
        IReadOnlyDictionary<string, string>? composerByWork)
    {
        var template = GetDiscFolderTemplate(discFolderTemplate);
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        var usesWork = ContainsAnyField(template, ["Work"]);
        var effectiveValues = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        effectiveValues["WorkComposer"] = string.Empty;

        if (!usesWork)
        {
            return RenderSingleFolderSegment(template, effectiveValues);
        }

        var mode = NormalizeWorkHandling(workHandling);
        if (mode == "Inline or Folders")
        {
            mode = works.Count > 1 ? "Folders" : "Inline";
        }

        if (mode == "Folders")
        {
            effectiveValues["Work"] = string.Empty;
            effectiveValues["WorkComposer"] = string.Empty;
            var segments = RenderSingleFolderSegment(template, effectiveValues).ToList();
            var workValues = new Dictionary<string, string>(effectiveValues, StringComparer.OrdinalIgnoreCase)
            {
                ["Work"] = currentWork ?? string.Empty
            };
            SetComposerForWork(workValues, composerByWork, currentWork);
            var workFolder = RenderWorkFolderSegment(template, workValues);
            if (!string.IsNullOrWhiteSpace(workFolder))
            {
                segments.Add(workFolder);
            }

            return segments;
        }

        effectiveValues["Work"] = JoinWorks(works, workSeparator, workSeparatorNoSpaces);
        effectiveValues["WorkComposer"] = JoinComposers(works, composerByWork, workSeparator, workSeparatorNoSpaces);
        return RenderSingleFolderSegment(template, effectiveValues);
    }

    private static string RenderWorkFolderSegment(
        string template,
        IReadOnlyDictionary<string, string> values)
    {
        if (!ContainsAnyField(template, ["WorkComposer"]))
        {
            return StringTools.GetSafeFilename(values.GetValueOrDefault("Work", string.Empty)) ?? string.Empty;
        }

        var tokens = GetSegmentTokens(template);
        var firstWorkField = -1;
        var lastWorkField = -1;
        for (var index = 0; index < tokens.Count; index++)
        {
            if (!tokens[index].IsField || !IsWorkFolderField(tokens[index].FieldName))
            {
                continue;
            }

            if (firstWorkField < 0)
            {
                firstWorkField = index;
            }

            lastWorkField = index;
        }

        if (firstWorkField < 0)
        {
            return string.Empty;
        }

        var firstToken = firstWorkField;
        if (firstToken > 0 &&
            !tokens[firstToken - 1].IsField &&
            IsOpeningWrapperLiteral(tokens[firstToken - 1].Text))
        {
            firstToken--;
        }

        var lastToken = lastWorkField;
        if (lastToken + 1 < tokens.Count && !tokens[lastToken + 1].IsField)
        {
            lastToken++;
        }

        var workTokens = tokens
            .Skip(firstToken)
            .Take(lastToken - firstToken + 1)
            .ToArray();
        var rendered = CleanRenderedSegment(RenderTokens(workTokens, values));
        return StringTools.GetSafeFilename(rendered) ?? string.Empty;
    }

    private static IReadOnlyList<string> RenderSingleFolderSegment(
        string template,
        IReadOnlyDictionary<string, string> values)
    {
        var rendered = RenderSegment(template, values);
        var safe = string.IsNullOrWhiteSpace(rendered)
            ? string.Empty
            : StringTools.GetSafeFilename(rendered) ?? string.Empty;
        return string.IsNullOrWhiteSpace(safe) ? [] : [safe];
    }

    private static string GetDiscFolderTemplate(string? discFolderTemplate)
    {
        if (discFolderTemplate is null)
        {
            return DefaultDiscFolderTemplate;
        }

        return discFolderTemplate.Trim();
    }

    private static string GetFolderTemplate(string? folderTemplate, string defaultFolderTemplate)
    {
        if (folderTemplate is null)
        {
            return defaultFolderTemplate;
        }

        return folderTemplate.Trim();
    }

    private static bool ShouldUseDiscScopedTrackPadding(
        IReadOnlyList<(int TrackNumber, int DiscNumber)> tracks,
        string? discFolderTemplate)
    {
        if (string.IsNullOrWhiteSpace(discFolderTemplate))
        {
            return false;
        }

        var discGroups = tracks
            .Where(track => track.DiscNumber > 0)
            .GroupBy(track => track.DiscNumber)
            .ToList();
        if (discGroups.Count == 0)
        {
            return false;
        }

        return discGroups.All(group =>
        {
            var positiveTrackNumbers = group
                .Select(track => track.TrackNumber)
                .Where(trackNumber => trackNumber > 0)
                .ToArray();
            return positiveTrackNumbers.Length == 0 || positiveTrackNumbers.Min() == 1;
        });
    }

    private static string NormalizeWorkHandling(string? value)
    {
        return value switch
        {
            "Folders" => "Folders",
            "Inline or Folders" => "Inline or Folders",
            _ => "Inline"
        };
    }

    private static string JoinWorks(IReadOnlyList<string> works, string? separator, bool noSpaces)
    {
        if (works.Count == 0)
        {
            return string.Empty;
        }

        var effectiveSeparator = string.IsNullOrEmpty(separator)
            ? AppSettings.DefaultDiscWorkSeparator
            : separator;
        var joiner = noSpaces ? effectiveSeparator : $" {effectiveSeparator} ";
        return string.Join(joiner, works);
    }

    private static IReadOnlyList<string> GetWorksForDisc(IEnumerable<Track>? tracks, int discNumber, string? fallbackWork)
    {
        var works = tracks?
            .Where(track => discNumber <= 0 || track.MediaNumber == discNumber)
            .Select(track => track.Work)
            .ToArray();

        return NormalizeWorks(works, fallbackWork);
    }

    private static IReadOnlyList<string> NormalizeWorks(IEnumerable<string>? works, string? fallbackWork)
    {
        var normalized = (works ?? [])
            .Where(work => !string.IsNullOrWhiteSpace(work))
            .Select(work => work.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(fallbackWork))
        {
            normalized.Add(fallbackWork.Trim());
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, string> GetFirstComposersByWork(
        IEnumerable<Track>? tracks,
        string? fallbackWork,
        string fallbackComposer)
    {
        var composers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in tracks ?? [])
        {
            var work = track.Work?.Trim();
            if (string.IsNullOrWhiteSpace(work) ||
                composers.ContainsKey(work))
            {
                continue;
            }

            composers[work] = GetTrackComposer(track);
        }

        fallbackWork = fallbackWork?.Trim();
        if (!string.IsNullOrWhiteSpace(fallbackWork) &&
            !composers.ContainsKey(fallbackWork))
        {
            composers[fallbackWork] = fallbackComposer;
        }

        return composers;
    }

    private static string GetTrackComposer(Track? track)
    {
        return track?.Composer?.Name ?? string.Empty;
    }

    private static void SetComposerForWork(
        IDictionary<string, string> values,
        IReadOnlyDictionary<string, string>? composerByWork,
        string? work)
    {
        var fallbackComposer = values.TryGetValue("WorkComposer", out var existingComposer)
            ? existingComposer
            : string.Empty;
        values["WorkComposer"] = !string.IsNullOrWhiteSpace(work) &&
            composerByWork?.TryGetValue(work, out var composer) == true
                ? composer
                : fallbackComposer;
    }

    private static string JoinComposers(
        IReadOnlyList<string> works,
        IReadOnlyDictionary<string, string>? composerByWork,
        string? separator,
        bool noSpaces)
    {
        var composers = works
            .Select(work => composerByWork?.TryGetValue(work, out var composer) == true ? composer : string.Empty)
            .Where(composer => !string.IsNullOrWhiteSpace(composer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JoinWorks(composers, separator, noSpaces);
    }

    private static string PadPositiveNumber(int value)
    {
        return value > 0 ? value.ToString().PadLeft(2, '0') : string.Empty;
    }

    private static int GetTrackNumberWidth(int totalTracks, int? trackNumberPaddingWidth)
    {
        return Math.Max(2, Math.Max(totalTracks, trackNumberPaddingWidth ?? 0).ToString().Length);
    }

    private static string FormatReleaseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("epmini", StringComparison.OrdinalIgnoreCase))
        {
            return "EP";
        }

        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static Dictionary<string, string> BuildValues(
        Album? album,
        Track? track,
        string albumArtist,
        string albumTitle,
        string quality,
        int totalTracks,
        int? trackNumberPaddingWidth = null)
    {
        var releaseDate = album?.ReleaseDateOriginal ?? album?.ReleaseDateDownload ?? album?.ReleaseDateStream;
        var releaseYear = releaseDate?.Year.ToString() ?? string.Empty;
        var version = track is null
            ? album?.Version ?? string.Empty
            : track.Version ?? string.Empty;
        var trackTitle = QobuzTitleFormatter.TrackTitle(track?.Title);
        var trackNumber = track?.TrackNumber ?? 0;
        var discNumber = track?.MediaNumber ?? 0;
        var discTotal = album?.MediaCount ?? 0;
        var trackNumberWidth = GetTrackNumberWidth(totalTracks, trackNumberPaddingWidth);
        var qualityPath = FormatQualityForPath(quality);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlbumArtist"] = albumArtist,
            ["TrackArtist"] = track?.Performer?.Name ?? albumArtist,
            ["AlbumTitle"] = albumTitle,
            ["AlbumComposer"] = album?.Composer?.Name ?? string.Empty,
            ["TrackComposer"] = GetTrackComposer(track),
            ["TrackTitle"] = trackTitle,
            ["Quality"] = qualityPath,
            ["QualityRaw"] = quality,
            ["ReleaseDate"] = StringTools.FormatDateTimeOffset(releaseDate),
            ["ReleaseYear"] = releaseYear,
            ["Version"] = version,
            ["ReleaseType"] = FormatReleaseType(album?.ReleaseType),
            ["UPC"] = album?.Upc ?? string.Empty,
            ["Label"] = album?.Label?.Name ?? string.Empty,
            ["ISRC"] = track?.Isrc ?? string.Empty,
            ["ExplicitAdvisory"] = track?.ParentalWarning == true || album?.ParentalWarning == true ? "Explicit" : string.Empty,
            ["DiscNumber"] = discNumber > 0 ? discNumber.ToString() : string.Empty,
            ["DiscNumberPadded"] = PadPositiveNumber(discNumber),
            ["TotalDiscs"] = discTotal > 0 ? discTotal.ToString() : string.Empty,
            ["TotalDiscsPadded"] = PadPositiveNumber(discTotal),
            ["TrackNumber"] = trackNumber > 0 ? trackNumber.ToString() : string.Empty,
            ["TrackNumberPadded"] = trackNumber > 0 ? trackNumber.ToString().PadLeft(trackNumberWidth, '0') : string.Empty,
            ["TotalTracks"] = totalTracks > 0 ? totalTracks.ToString() : string.Empty,
            ["TotalTracksPadded"] = totalTracks > 0 ? totalTracks.ToString().PadLeft(trackNumberWidth, '0') : string.Empty,
            ["Work"] = track?.Work ?? string.Empty
        };
    }

    private static Dictionary<string, string> BuildPlaylistValues(
        string? playlistId,
        string? playlistTitle,
        string? playlistOwner,
        Track? track,
        Album? album,
        string albumArtist,
        string albumTitle,
        string quality,
        int playlistNumber,
        int playlistTotalTracks)
    {
        var values = BuildValues(
            album,
            track,
            albumArtist,
            albumTitle,
            quality,
            totalTracks: album?.TracksCount ?? 0);
        var playlistNumberWidth = GetPlaylistNumberWidth(playlistTotalTracks);

        values["PlaylistTitle"] = StringTools.GetSafePlaylistTitleSegment(playlistTitle, playlistId);
        values["PlaylistOwner"] = string.IsNullOrWhiteSpace(playlistOwner) ? "Unknown Owner" : playlistOwner.Trim();
        values["PlaylistNumber"] = playlistNumber > 0 ? playlistNumber.ToString() : string.Empty;
        values["PlaylistNumberPadded"] = playlistNumber > 0 ? playlistNumber.ToString().PadLeft(playlistNumberWidth, '0') : string.Empty;
        values["PlaylistTotalTracks"] = playlistTotalTracks > 0 ? playlistTotalTracks.ToString() : string.Empty;
        values["PlaylistTotalTracksPadded"] = playlistTotalTracks > 0 ? playlistTotalTracks.ToString().PadLeft(playlistNumberWidth, '0') : string.Empty;

        return values;
    }

    private static int GetPlaylistNumberWidth(int playlistTotalTracks)
    {
        return Math.Max(2, playlistTotalTracks.ToString().Length);
    }

    private static Dictionary<string, string> BuildPreviewValues(
        string albumArtist,
        string albumTitle,
        string quality,
        string releaseDate,
        int totalTracks,
        int trackNumber,
        string trackTitle,
        string version,
        int discNumber,
        int totalDiscs,
        string releaseType = "",
        string label = "",
        string upc = "",
        string isrc = "",
        bool explicitAdvisory = false,
        string work = "",
        string albumComposer = "Example Album Composer",
        string trackComposer = "Example Track Composer",
        int? trackNumberPaddingWidth = null)
    {
        var releaseYear = releaseDate.Length >= 4 ? releaseDate[..4] : string.Empty;
        var trackNumberWidth = GetTrackNumberWidth(totalTracks, trackNumberPaddingWidth);
        var qualityPath = FormatQualityForPath(quality);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlbumArtist"] = albumArtist,
            ["TrackArtist"] = albumArtist,
            ["AlbumTitle"] = albumTitle,
            ["AlbumComposer"] = albumComposer,
            ["TrackComposer"] = trackComposer,
            ["TrackTitle"] = trackTitle,
            ["Quality"] = qualityPath,
            ["QualityRaw"] = quality,
            ["ReleaseDate"] = releaseDate,
            ["ReleaseYear"] = releaseYear,
            ["Version"] = version,
            ["ReleaseType"] = FormatReleaseType(releaseType),
            ["UPC"] = upc,
            ["Label"] = label,
            ["ISRC"] = isrc,
            ["ExplicitAdvisory"] = explicitAdvisory ? "Explicit" : string.Empty,
            ["DiscNumber"] = discNumber > 0 ? discNumber.ToString() : string.Empty,
            ["DiscNumberPadded"] = PadPositiveNumber(discNumber),
            ["TotalDiscs"] = totalDiscs > 0 ? totalDiscs.ToString() : string.Empty,
            ["TotalDiscsPadded"] = PadPositiveNumber(totalDiscs),
            ["TrackNumber"] = trackNumber > 0 ? trackNumber.ToString() : string.Empty,
            ["TrackNumberPadded"] = trackNumber > 0 ? trackNumber.ToString().PadLeft(trackNumberWidth, '0') : string.Empty,
            ["TotalTracks"] = totalTracks > 0 ? totalTracks.ToString() : string.Empty,
            ["TotalTracksPadded"] = totalTracks > 0 ? totalTracks.ToString().PadLeft(trackNumberWidth, '0') : string.Empty,
            ["Work"] = work
        };
    }

    private static string RenderSegment(string template, IReadOnlyDictionary<string, string> values)
    {
        var effectiveValues = GetSegmentValues(template, values);
        return CleanRenderedSegment(RenderTokens(GetSegmentTokens(template), effectiveValues));
    }

    private static string CleanRenderedSegment(string rendered)
    {
        rendered = WhitespaceRegex().Replace(rendered, " ");
        rendered = EmptyBracketRegex().Replace(rendered, string.Empty);
        rendered = EmptyParenthesesRegex().Replace(rendered, string.Empty);
        rendered = WhitespaceRegex().Replace(rendered, " ");
        rendered = EmptySeparatorRegex().Replace(rendered, string.Empty);

        return rendered.Trim(' ', '-', '_', '.');
    }

    private static IReadOnlyList<SegmentToken> GetSegmentTokens(string template)
    {
        return SegmentTokenCache.GetOrAdd(template, ParseSegmentTokens);
    }

    private static IReadOnlyList<SegmentToken> ParseSegmentTokens(string template)
    {
        var tokens = new List<SegmentToken>();
        var index = 0;

        while (index < template.Length)
        {
            var openIndex = template.IndexOf('{', index);
            if (openIndex < 0)
            {
                tokens.Add(SegmentToken.Literal(template[index..]));
                break;
            }

            if (openIndex > index)
            {
                tokens.Add(SegmentToken.Literal(template[index..openIndex]));
            }

            var closeIndex = template.IndexOf('}', openIndex + 1);
            if (closeIndex < 0)
            {
                tokens.Add(SegmentToken.Literal(template[openIndex..]));
                break;
            }

            var fieldName = template[(openIndex + 1)..closeIndex];
            var originalText = template[openIndex..(closeIndex + 1)];
            tokens.Add(SegmentToken.Field(fieldName, originalText));
            index = closeIndex + 1;
        }

        return tokens;
    }

    private static string RenderTokens(IReadOnlyList<SegmentToken> tokens, IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder();

        foreach (var token in tokens)
        {
            if (!token.IsField)
            {
                builder.Append(token.Text);
                continue;
            }

            builder.Append(values.TryGetValue(token.FieldName, out var value)
                ? value
                : token.Text);
        }

        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, string> GetSegmentValues(
        string segmentTemplate,
        IReadOnlyDictionary<string, string> values)
    {
        if (!ContainsAnyField(segmentTemplate, ["Version"]) ||
            !values.TryGetValue("Version", out var version) ||
            string.IsNullOrWhiteSpace(version))
        {
            return values;
        }

        var albumTitleForComparison = values.TryGetValue("AlbumTitle", out var albumTitle) ? albumTitle : string.Empty;
        var trackTitleForComparison = values.TryGetValue("TrackTitle", out var trackTitle) ? trackTitle : string.Empty;

        var titleAlreadyContainsVersion =
            ContainsAnyField(segmentTemplate, ["AlbumTitle"]) &&
            QobuzTitleFormatter.ContainsNormalizedVersion(albumTitleForComparison, version);
        var trackAlreadyContainsVersion =
            ContainsAnyField(segmentTemplate, ["TrackTitle"]) &&
            QobuzTitleFormatter.ContainsNormalizedVersion(trackTitleForComparison, version);

        if (!titleAlreadyContainsVersion && !trackAlreadyContainsVersion)
        {
            return values;
        }

        return values.ToDictionary(
            pair => pair.Key,
            pair => IsVersionField(pair.Key) ? string.Empty : pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool ContainsAnyField(string template, IEnumerable<string> fieldNames)
    {
        return fieldNames.Any(fieldName =>
            Regex.IsMatch(template, $@"\{{{Regex.Escape(fieldName)}\}}", RegexOptions.IgnoreCase));
    }

    private static bool IsVersionField(string fieldName)
    {
        return fieldName.Equals("Version", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkFolderField(string fieldName)
    {
        return fieldName.Equals("Work", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("WorkComposer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpeningWrapperLiteral(string text)
    {
        return text.Trim() is "(" or "[" or "{";
    }

    private readonly record struct SegmentToken(bool IsField, string Text, string FieldName)
    {
        public static SegmentToken Literal(string text)
        {
            return new SegmentToken(false, text, string.Empty);
        }

        public static SegmentToken Field(string fieldName, string originalText)
        {
            return new SegmentToken(true, originalText, fieldName);
        }
    }

    [GeneratedRegex(@"FLAC\s+(?<BitDepth>\d+(?:\.\d+)?)\/(?<SampleRate>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex FlacQualityRegex();

    [GeneratedRegex(@"MP3\s+(?<Bitrate>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Mp3QualityRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex EmptyBracketRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex EmptyParenthesesRegex();

    [GeneratedRegex(@"(?:^|\s)-\s(?:$|\s)")]
    private static partial Regex EmptySeparatorRegex();

}
