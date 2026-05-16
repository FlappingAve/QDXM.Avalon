namespace QDXM.Avalon.Core.Settings;

public sealed class TaggingOptions
{
    public bool WriteAlbumNameTag { get; set; } = true;
    public bool WriteAlbumArtistTag { get; set; } = true;
    public bool WriteTrackTitleTag { get; set; } = true;
    public bool WriteTrackArtistTag { get; set; } = true;
    public bool WriteTrackNumberTag { get; set; } = true;
    public bool WriteTrackTotalTag { get; set; } = true;
    public bool WriteDiscNumberTag { get; set; } = true;
    public bool WriteDiscTotalTag { get; set; } = true;
    public bool WriteReleaseYearTag { get; set; } = true;
    public bool WriteReleaseDateTag { get; set; } = true;
    public bool WriteVersionTag { get; set; } = true;
    public bool WriteWorkTag { get; set; } = true;
    public bool WriteGenreTag { get; set; } = true;
    public bool WriteComposerTag { get; set; } = true;
    public bool WriteCopyrightTag { get; set; } = true;
    public bool WriteIsrcTag { get; set; } = true;
    public bool WriteReleaseTypeTag { get; set; } = true;
    public bool WriteUpcTag { get; set; } = true;
    public bool WriteExplicitTag { get; set; } = true;
    public bool WriteCommentTag { get; set; }
    public bool WriteCoverImageTag { get; set; } = true;
    public bool WriteLabelTag { get; set; } = true;
    public bool WriteRawQobuzCreditsTag { get; set; }
    public bool WriteUrlTag { get; set; }
    public string CommentTag { get; set; } = string.Empty;
    public string ArtSize { get; set; } = "600";

    public TaggingOptions CreateSnapshot()
    {
        return new TaggingOptions
        {
            WriteAlbumNameTag = WriteAlbumNameTag,
            WriteAlbumArtistTag = WriteAlbumArtistTag,
            WriteTrackTitleTag = WriteTrackTitleTag,
            WriteTrackArtistTag = WriteTrackArtistTag,
            WriteTrackNumberTag = WriteTrackNumberTag,
            WriteTrackTotalTag = WriteTrackTotalTag,
            WriteDiscNumberTag = WriteDiscNumberTag,
            WriteDiscTotalTag = WriteDiscTotalTag,
            WriteReleaseYearTag = WriteReleaseYearTag,
            WriteReleaseDateTag = WriteReleaseDateTag,
            WriteVersionTag = WriteVersionTag,
            WriteWorkTag = WriteWorkTag,
            WriteGenreTag = WriteGenreTag,
            WriteComposerTag = WriteComposerTag,
            WriteCopyrightTag = WriteCopyrightTag,
            WriteIsrcTag = WriteIsrcTag,
            WriteReleaseTypeTag = WriteReleaseTypeTag,
            WriteUpcTag = WriteUpcTag,
            WriteExplicitTag = WriteExplicitTag,
            WriteCommentTag = WriteCommentTag,
            WriteCoverImageTag = WriteCoverImageTag,
            WriteLabelTag = WriteLabelTag,
            WriteRawQobuzCreditsTag = WriteRawQobuzCreditsTag,
            WriteUrlTag = WriteUrlTag,
            CommentTag = CommentTag,
            ArtSize = ArtSize
        };
    }
}
