using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Settings;
using TagLib;

namespace QDXM.Avalon.Core.Tools;

public static class AudioFileTagger
{
    public static void AddMetadata(
        string filePath,
        Track track,
        Album album,
        AppSettings settings,
        string? coverArtPath = null)
    {
        var tagging = settings.Tagging;
        using var tagFile = TagLib.File.Create(filePath);
        tagFile.RemoveTags(TagTypes.Id3v1);

        TagLib.Id3v2.Tag.DefaultVersion = 4;
        TagLib.Id3v2.Tag.ForceDefaultVersion = true;

        if (tagging.WriteCoverImageTag && !string.IsNullOrWhiteSpace(coverArtPath) && System.IO.File.Exists(coverArtPath))
        {
            tagFile.Tag.Pictures =
            [
                new Picture(coverArtPath)
                {
                    Type = PictureType.FrontCover
                }
            ];
        }

        if (tagging.WriteTrackTitleTag)
        {
            tagFile.Tag.Title = BuildTrackTitleTag(track, tagging);
        }

        if (tagging.WriteAlbumNameTag)
        {
            tagFile.Tag.Album = QobuzTitleFormatter.AlbumTitle(album.Title);
        }

        if (tagging.WriteAlbumArtistTag)
        {
            tagFile.Tag.AlbumArtists = BuildAlbumArtistTags(album);
        }

        if (tagging.WriteTrackArtistTag)
        {
            tagFile.Tag.Performers = BuildTrackArtistTags(track, album);
        }

        if (tagging.WriteComposerTag)
        {
            tagFile.Tag.Composers = BuildComposerTags(track, album);
        }

        if (tagging.WriteLabelTag)
        {
            tagFile.Tag.Publisher = album.Label?.Name;
        }

        var releaseDate = album.ReleaseDateOriginal ?? album.ReleaseDateDownload ?? album.ReleaseDateStream;
        if (tagging.WriteReleaseYearTag && releaseDate is not null)
        {
            tagFile.Tag.Year = (uint)releaseDate.Value.Year;
        }

        if (tagging.WriteGenreTag)
        {
            var genres = album.GenresList?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            tagFile.Tag.Genres = genres is { Length: > 0 }
                ? genres
                : SplitArtists([album.Genre?.Name], null);
        }

        if (tagging.WriteTrackNumberTag && track.TrackNumber is not null)
        {
            tagFile.Tag.Track = (uint)track.TrackNumber.Value;
        }

        if (tagging.WriteDiscNumberTag && track.MediaNumber is not null)
        {
            tagFile.Tag.Disc = (uint)track.MediaNumber.Value;
        }

        if (tagging.WriteDiscTotalTag && album.MediaCount is not null)
        {
            tagFile.Tag.DiscCount = (uint)album.MediaCount.Value;
        }

        if (tagging.WriteTrackTotalTag && album.TracksCount is not null)
        {
            tagFile.Tag.TrackCount = (uint)album.TracksCount.Value;
        }

        if (tagging.WriteCommentTag)
        {
            tagFile.Tag.Comment = tagging.CommentTag;
        }

        if (tagging.WriteCopyrightTag)
        {
            tagFile.Tag.Copyright = track.Copyright ?? album.Copyright;
        }

        if (tagging.WriteIsrcTag)
        {
            tagFile.Tag.ISRC = track.Isrc;
        }

        WriteFormatSpecificTags(tagFile, track, album, tagging, releaseDate);
        tagFile.Save();
    }

    private static void WriteFormatSpecificTags(
        TagLib.File tagFile,
        Track track,
        Album album,
        TaggingOptions tagging,
        DateTimeOffset? releaseDate)
    {
        if (tagFile.GetTag(TagTypes.Xiph, create: false) is TagLib.Ogg.XiphComment xiph)
        {
            WriteXiphBarcodeTag(xiph, album.Upc, tagging);

            if (tagging.WriteLabelTag && !string.IsNullOrWhiteSpace(album.Label?.Name))
            {
                xiph.SetField("LABEL", Decode(album.Label.Name));
            }

            if (tagging.WriteUrlTag)
            {
                xiph.SetField("URL", album.Url);
            }

            if (tagging.WriteReleaseDateTag && releaseDate is not null)
            {
                xiph.SetField("DATE", StringTools.FormatDateTimeOffset(releaseDate));
            }

            if (tagging.WriteReleaseYearTag && releaseDate is not null)
            {
                xiph.SetField("YEAR", releaseDate.Value.Year.ToString());
            }

            WriteXiphReleaseTypeTag(xiph, album.ReleaseType, tagging);

            WriteXiphVersionTag(xiph, track.Version, tagging);

            WriteXiphWorkTag(xiph, track.Work, tagging);

            WriteXiphRawQobuzCreditsTag(xiph, track.Performers, tagging);

            WriteXiphExplicitAdvisoryTag(xiph, track.ParentalWarning == true, tagging);
        }

        if (tagFile.GetTag(TagTypes.Id3v2, create: false) is TagLib.Id3v2.Tag id3)
        {
            WriteId3BarcodeTag(id3, album.Upc, tagging);

            if (tagging.WriteReleaseDateTag && releaseDate is not null)
            {
                id3.SetTextFrame("TDRL", StringTools.FormatDateTimeOffset(releaseDate));
            }

            if (tagging.WriteUrlTag && !string.IsNullOrWhiteSpace(album.Url))
            {
                id3.SetTextFrame("WCOM", album.Url);
            }

            WriteId3ReleaseTypeTag(id3, album.ReleaseType, tagging);

            WriteId3VersionTag(id3, track.Version, tagging);

            WriteId3WorkTag(id3, track.Work, tagging);

            WriteId3ExplicitAdvisoryTag(id3, track.ParentalWarning == true, tagging);
        }
    }

    internal static void WriteXiphWorkTag(TagLib.Ogg.XiphComment xiph, string? work, TaggingOptions tagging)
    {
        if (tagging.WriteWorkTag && !string.IsNullOrWhiteSpace(work))
        {
            xiph.SetField("WORK", Decode(work));
        }
    }

    internal static string BuildTrackTitleTag(Track track, TaggingOptions tagging)
    {
        var title = QobuzTitleFormatter.TrackTitle(track.Title);
        var version = track.Version?.Trim() ?? string.Empty;
        if (!tagging.IncludeVersionInTrackTitleTag ||
            string.IsNullOrWhiteSpace(version) ||
            QobuzTitleFormatter.ContainsNormalizedVersion(title, version))
        {
            return title;
        }

        return string.IsNullOrWhiteSpace(title)
            ? version
            : $"{title} ({version})";
    }

    internal static void WriteXiphVersionTag(TagLib.Ogg.XiphComment xiph, string? version, TaggingOptions tagging)
    {
        if (tagging.WriteVersionTag && !string.IsNullOrWhiteSpace(version))
        {
            xiph.SetField("VERSION", Decode(version));
        }
    }

    internal static void WriteXiphReleaseTypeTag(TagLib.Ogg.XiphComment xiph, string? releaseType, TaggingOptions tagging)
    {
        var normalizedReleaseType = NormalizeReleaseTypeTag(releaseType);
        if (tagging.WriteReleaseTypeTag && normalizedReleaseType is not null)
        {
            xiph.SetField("RELEASETYPE", normalizedReleaseType);
        }
    }

    internal static void WriteId3WorkTag(TagLib.Id3v2.Tag id3, string? work, TaggingOptions tagging)
    {
        if (tagging.WriteWorkTag && !string.IsNullOrWhiteSpace(work))
        {
            SetUserTextFrame(id3, "WORK", Decode(work));
        }
    }

    internal static void WriteId3VersionTag(TagLib.Id3v2.Tag id3, string? version, TaggingOptions tagging)
    {
        if (tagging.WriteVersionTag && !string.IsNullOrWhiteSpace(version))
        {
            SetUserTextFrame(id3, "VERSION", Decode(version));
        }
    }

    internal static void WriteId3ReleaseTypeTag(TagLib.Id3v2.Tag id3, string? releaseType, TaggingOptions tagging)
    {
        var normalizedReleaseType = NormalizeReleaseTypeTag(releaseType);
        if (tagging.WriteReleaseTypeTag && normalizedReleaseType is not null)
        {
            SetUserTextFrame(id3, "RELEASETYPE", normalizedReleaseType);
        }
    }

    internal static void WriteId3BarcodeTag(TagLib.Id3v2.Tag id3, string? upc, TaggingOptions tagging)
    {
        if (tagging.WriteUpcTag && !string.IsNullOrWhiteSpace(upc))
        {
            SetUserTextFrame(id3, "BARCODE", Decode(upc));
        }
    }

    internal static void WriteId3ExplicitAdvisoryTag(TagLib.Id3v2.Tag id3, bool explicitTrack, TaggingOptions tagging)
    {
        if (tagging.WriteExplicitTag && explicitTrack)
        {
            SetUserTextFrame(id3, "ITUNESADVISORY", "1");
        }
    }

    internal static void WriteXiphBarcodeTag(TagLib.Ogg.XiphComment xiph, string? upc, TaggingOptions tagging)
    {
        if (tagging.WriteUpcTag && !string.IsNullOrWhiteSpace(upc))
        {
            xiph.SetField("BARCODE", Decode(upc));
        }
    }

    internal static void WriteXiphExplicitAdvisoryTag(TagLib.Ogg.XiphComment xiph, bool explicitTrack, TaggingOptions tagging)
    {
        if (tagging.WriteExplicitTag && explicitTrack)
        {
            xiph.SetField("ITUNESADVISORY", "1");
        }
    }

    internal static void WriteXiphRawQobuzCreditsTag(TagLib.Ogg.XiphComment xiph, string? performers, TaggingOptions tagging)
    {
        if (tagging.WriteRawQobuzCreditsTag && !string.IsNullOrWhiteSpace(performers))
        {
            xiph.SetField("INVOLVEDPEOPLE", Decode(performers));
        }
    }

    internal static string[] BuildAlbumArtistTags(Album album)
    {
        return SplitArtists([album.Artist?.Name], null);
    }

    internal static string[] BuildTrackArtistTags(Track track, Album album)
    {
        return SplitArtists([track.Performer?.Name], album.Artist?.Name);
    }

    internal static string[] BuildComposerTags(Track track, Album album)
    {
        return SplitArtists([track.Composer?.Name], album.Composer?.Name);
    }

    private static void SetUserTextFrame(TagLib.Id3v2.Tag id3, string description, string value)
    {
        var frame = TagLib.Id3v2.UserTextInformationFrame.Get(id3, description, create: true);
        frame.Text = [value];
    }

    private static string? NormalizeReleaseTypeTag(string? releaseType)
    {
        if (string.IsNullOrWhiteSpace(releaseType))
        {
            return null;
        }

        var decoded = Decode(releaseType).Trim();
        if (decoded.Equals("epmini", StringComparison.OrdinalIgnoreCase))
        {
            return "ep";
        }

        return decoded.ToLowerInvariant();
    }

    private static string[] SplitArtists(IEnumerable<string?> names, string? fallback)
    {
        var artists = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => Decode(name!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (artists.Length > 0)
        {
            return artists;
        }

        return string.IsNullOrWhiteSpace(fallback) ? [] : [Decode(fallback)];
    }

    private static string Decode(string? value)
    {
        return StringTools.DecodeEncodedNonAsciiCharacters(value) ?? string.Empty;
    }
}
