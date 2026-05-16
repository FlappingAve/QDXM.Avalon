using System.Text.Json.Serialization;

namespace QDXM.Avalon.Core.Settings;

public sealed class AppSettings
{
    public const string DefaultFolderTemplate = @"{AlbumArtist}\({ReleaseYear}) {AlbumTitle} ({Version}) [{Quality}]";
    public const string DefaultFilenameTemplate = "{TrackNumberPadded} - {TrackTitle} ({Version})";
    public const string DefaultDiscFolderTemplate = "Disc {DiscNumberPadded} - {Work}";
    public const string DefaultPlaylistOrganization = "Keep playlist together";
    public const string UseStandardTemplatesPlaylistOrganization = "Use standard templates";
    public const string DefaultPlaylistFolderTemplate = @"Playlists\{PlaylistTitle}";
    public const string DefaultPlaylistFilenameTemplate = "{PlaylistNumberPadded} - {TrackArtist} - {TrackTitle} ({Version})";
    public const string DefaultDiscWorkHandling = "Inline";
    public const string DefaultDiscWorkSeparator = "&";
    public const string DuplicateFileSkip = "Skip";
    public const string DuplicateFileOverwrite = "Overwrite";
    public const string DuplicateFileKeepBoth = "Keep both";

    public string DownloadFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    [JsonIgnore]
    public string EffectiveDownloadFolder => string.IsNullOrWhiteSpace(DownloadFolder)
        ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        : DownloadFolder.Trim();

    [JsonIgnore]
    public string AppId { get; set; } = string.Empty;
    [JsonIgnore]
    public string AppSecret { get; set; } = string.Empty;
    [JsonIgnore]
    public string UserId { get; set; } = string.Empty;
    [JsonIgnore]
    public string UserAuthToken { get; set; } = string.Empty;
    public string FormatId { get; set; } = global::QDXM.Avalon.Core.Tools.QualityStringMappings.FlacHighestFormatId;
    public string SelectedQuality { get; set; } = global::QDXM.Avalon.Core.Tools.QualityStringMappings.FlacHighestLabel;
    public bool FallbackToMp3IfFlacUnavailable { get; set; }
    public string DuplicateFileBehavior { get; set; } = DuplicateFileOverwrite;
    public string FilenameTemplate { get; set; } = DefaultFilenameTemplate;
    public string FolderTemplate { get; set; } = DefaultFolderTemplate;
    public string DiscFolderTemplate { get; set; } = DefaultDiscFolderTemplate;
    public string FolderTemplatePresetId { get; set; } = "folder.default";
    public string FilenameTemplatePresetId { get; set; } = "filename.default";
    public string DiscFolderTemplatePresetId { get; set; } = "discFolder.default";
    public string PlaylistOrganization { get; set; } = DefaultPlaylistOrganization;
    public string PlaylistFolderTemplate { get; set; } = DefaultPlaylistFolderTemplate;
    public string PlaylistFilenameTemplate { get; set; } = DefaultPlaylistFilenameTemplate;
    public string PlaylistFolderTemplatePresetId { get; set; } = "playlistFolder.default";
    public string PlaylistFilenameTemplatePresetId { get; set; } = "playlistFilename.default";
    public TemplatePresetSettings TemplatePresets { get; set; } = new();
    public string DiscWorkHandling { get; set; } = DefaultDiscWorkHandling;
    public string DiscWorkSeparator { get; set; } = DefaultDiscWorkSeparator;
    public bool DiscWorkSeparatorNoSpaces { get; set; }
    public int MaxFileNameLength { get; set; } = 100;
    public bool SaveCoverArtFile { get; set; } = true;
    public string CoverArtSize { get; set; } = global::QDXM.Avalon.Core.Tools.CoverArtUrlSelector.RecommendedDisplayName;
    public bool DownloadGoodies { get; set; } = true;
    public TaggingOptions Tagging { get; set; } = new();

    public AppSettings CreateSnapshot()
    {
        return new AppSettings
        {
            DownloadFolder = DownloadFolder,
            AppId = AppId,
            AppSecret = AppSecret,
            UserId = UserId,
            UserAuthToken = UserAuthToken,
            FormatId = FormatId,
            SelectedQuality = SelectedQuality,
            FallbackToMp3IfFlacUnavailable = FallbackToMp3IfFlacUnavailable,
            DuplicateFileBehavior = DuplicateFileBehavior,
            FilenameTemplate = FilenameTemplate,
            FolderTemplate = FolderTemplate,
            DiscFolderTemplate = DiscFolderTemplate,
            FolderTemplatePresetId = FolderTemplatePresetId,
            FilenameTemplatePresetId = FilenameTemplatePresetId,
            DiscFolderTemplatePresetId = DiscFolderTemplatePresetId,
            PlaylistOrganization = PlaylistOrganization,
            PlaylistFolderTemplate = PlaylistFolderTemplate,
            PlaylistFilenameTemplate = PlaylistFilenameTemplate,
            PlaylistFolderTemplatePresetId = PlaylistFolderTemplatePresetId,
            PlaylistFilenameTemplatePresetId = PlaylistFilenameTemplatePresetId,
            TemplatePresets = TemplatePresets?.CreateSnapshot() ?? new TemplatePresetSettings(),
            DiscWorkHandling = DiscWorkHandling,
            DiscWorkSeparator = DiscWorkSeparator,
            DiscWorkSeparatorNoSpaces = DiscWorkSeparatorNoSpaces,
            MaxFileNameLength = MaxFileNameLength,
            SaveCoverArtFile = SaveCoverArtFile,
            CoverArtSize = CoverArtSize,
            DownloadGoodies = DownloadGoodies,
            Tagging = Tagging.CreateSnapshot()
        };
    }
}
