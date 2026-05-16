using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Tests;

public sealed class AudioFileTaggerTests
{
    [Fact]
    public void AudioFileTagger_WritesXiphWorkField()
    {
        var xiph = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphWorkTag(xiph, "Example Work", new TaggingOptions());

        Assert.Equal("Example Work", xiph.GetFirstField("WORK"));
        Assert.Null(xiph.GetFirstField("GROUPING"));
    }

    [Fact]
    public void AudioFileTagger_WritesId3WorkUserTextFrame()
    {
        var id3 = new TagLib.Id3v2.Tag();

        AudioFileTagger.WriteId3WorkTag(id3, "Example Work", new TaggingOptions());

        var frame = TagLib.Id3v2.UserTextInformationFrame.Get(id3, "WORK", create: false);
        Assert.NotNull(frame);
        Assert.Equal(["Example Work"], frame!.Text);
        Assert.Null(TagLib.Id3v2.UserTextInformationFrame.Get(id3, "GROUPING", create: false));
    }

    [Fact]
    public void AudioFileTagger_WritesXiphReleaseTypeField()
    {
        var xiph = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphReleaseTypeTag(xiph, "epmini", new TaggingOptions());

        Assert.Equal("ep", xiph.GetFirstField("RELEASETYPE"));
        Assert.Null(xiph.GetFirstField("MEDIATYPE"));
    }

    [Fact]
    public void AudioFileTagger_WritesUpcAsXiphBarcodeField()
    {
        var xiph = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphBarcodeTag(xiph, "0060251772142", new TaggingOptions());

        Assert.Equal("0060251772142", xiph.GetFirstField("BARCODE"));
        Assert.Null(xiph.GetFirstField("UPC"));
    }

    [Fact]
    public void AudioFileTagger_WritesUpcAsId3BarcodeUserTextFrame()
    {
        var id3 = new TagLib.Id3v2.Tag();

        AudioFileTagger.WriteId3BarcodeTag(id3, "0060251772142", new TaggingOptions());

        var frame = TagLib.Id3v2.UserTextInformationFrame.Get(id3, "BARCODE", create: false);
        Assert.NotNull(frame);
        Assert.Equal(["0060251772142"], frame!.Text);
    }

    [Fact]
    public void AudioFileTagger_WritesId3ReleaseTypeUserTextFrame()
    {
        var id3 = new TagLib.Id3v2.Tag();

        AudioFileTagger.WriteId3ReleaseTypeTag(id3, "Album", new TaggingOptions());

        var frame = TagLib.Id3v2.UserTextInformationFrame.Get(id3, "RELEASETYPE", create: false);
        Assert.NotNull(frame);
        Assert.Equal(["album"], frame!.Text);
        Assert.Null(id3.GetTextAsString("TMED"));
    }

    [Fact]
    public void AudioFileTagger_SkipsWorkWhenDisabled()
    {
        var xiph = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphWorkTag(xiph, "Example Work", new TaggingOptions { WriteWorkTag = false });

        Assert.Null(xiph.GetFirstField("WORK"));
    }

    [Fact]
    public void AudioFileTagger_SkipsReleaseTypeWhenDisabled()
    {
        var xiph = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphReleaseTypeTag(xiph, "album", new TaggingOptions { WriteReleaseTypeTag = false });

        Assert.Null(xiph.GetFirstField("RELEASETYPE"));
    }

    [Fact]
    public void AudioFileTagger_UsesDedicatedTrackFieldsBeforeFallbacks()
    {
        var album = new Album
        {
            Artist = new Artist { Name = "Album Artist" },
            Composer = new Artist { Name = "Album Composer" }
        };
        var track = new Track
        {
            Performer = new Artist { Name = "Track Artist" },
            Composer = new Artist { Name = "Track Composer" }
        };

        Assert.Equal(["Track Artist"], AudioFileTagger.BuildTrackArtistTags(track, album));
        Assert.Equal(["Track Composer"], AudioFileTagger.BuildComposerTags(track, album));
    }

    [Fact]
    public void AudioFileTagger_FallsBackToAlbumArtistAndComposer()
    {
        var album = new Album
        {
            Artist = new Artist { Name = "Album Artist" },
            Composer = new Artist { Name = "Album Composer" }
        };
        var track = new Track();

        Assert.Equal(["Album Artist"], AudioFileTagger.BuildTrackArtistTags(track, album));
        Assert.Equal(["Album Composer"], AudioFileTagger.BuildComposerTags(track, album));
    }

    [Fact]
    public void AudioFileTagger_WritesExplicitAdvisoryOnlyWhenTrackIsExplicit()
    {
        var explicitTrack = new TagLib.Ogg.XiphComment();
        var cleanTrack = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphExplicitAdvisoryTag(explicitTrack, explicitTrack: true, new TaggingOptions());
        AudioFileTagger.WriteXiphExplicitAdvisoryTag(cleanTrack, explicitTrack: false, new TaggingOptions());

        Assert.Equal("1", explicitTrack.GetFirstField("ITUNESADVISORY"));
        Assert.Null(cleanTrack.GetFirstField("ITUNESADVISORY"));
    }

    [Fact]
    public void AudioFileTagger_WritesId3ExplicitAdvisoryOnlyWhenTrackIsExplicit()
    {
        var explicitTrack = new TagLib.Id3v2.Tag();
        var cleanTrack = new TagLib.Id3v2.Tag();

        AudioFileTagger.WriteId3ExplicitAdvisoryTag(explicitTrack, explicitTrack: true, new TaggingOptions());
        AudioFileTagger.WriteId3ExplicitAdvisoryTag(cleanTrack, explicitTrack: false, new TaggingOptions());

        var frame = TagLib.Id3v2.UserTextInformationFrame.Get(explicitTrack, "ITUNESADVISORY", create: false);
        Assert.NotNull(frame);
        Assert.Equal(["1"], frame!.Text);
        Assert.Null(TagLib.Id3v2.UserTextInformationFrame.Get(cleanTrack, "ITUNESADVISORY", create: false));
    }

    [Fact]
    public void AudioFileTagger_SkipsExplicitAdvisoryWhenDisabled()
    {
        var xiph = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphExplicitAdvisoryTag(xiph, explicitTrack: true, new TaggingOptions { WriteExplicitTag = false });

        Assert.Null(xiph.GetFirstField("ITUNESADVISORY"));
    }

    [Fact]
    public void AudioFileTagger_WritesRawQobuzCreditsToXiphOnlyWhenEnabled()
    {
        var enabled = new TagLib.Ogg.XiphComment();
        var disabled = new TagLib.Ogg.XiphComment();

        AudioFileTagger.WriteXiphRawQobuzCreditsTag(
            enabled,
            "Example Artist, MainArtist - Example Producer, Producer",
            new TaggingOptions { WriteRawQobuzCreditsTag = true });
        AudioFileTagger.WriteXiphRawQobuzCreditsTag(
            disabled,
            "Example Artist, MainArtist - Example Producer, Producer",
            new TaggingOptions());

        Assert.Equal("Example Artist, MainArtist - Example Producer, Producer", enabled.GetFirstField("INVOLVEDPEOPLE"));
        Assert.Null(disabled.GetFirstField("INVOLVEDPEOPLE"));
    }
}
