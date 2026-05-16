using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.ViewModels;

public partial class TagsViewModel : ViewModelBase
{
    private readonly ISettingsStore settingsStore;

    public TagsViewModel()
        : this(new JsonSettingsStore())
    {
    }

    public TagsViewModel(ISettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        Settings = settingsStore.Current;
        FolderTemplateSlot = CreateTemplateSlot(TemplatePresetSlots.Folder, "Folder", allowBlankTemplate: true);
        FilenameTemplateSlot = CreateTemplateSlot(TemplatePresetSlots.Filename, "Filename", allowBlankTemplate: false);
        DiscFolderTemplateSlot = CreateTemplateSlot(
            TemplatePresetSlots.DiscFolder,
            "Disc Folder",
            allowBlankTemplate: true,
            ValidateDiscFolderTemplate);
        PlaylistFolderTemplateSlot = CreateTemplateSlot(TemplatePresetSlots.PlaylistFolder, "Playlist Folder", allowBlankTemplate: false);
        PlaylistFilenameTemplateSlot = CreateTemplateSlot(TemplatePresetSlots.PlaylistFilename, "Playlist Filename", allowBlankTemplate: false);
        TemplateSlots =
        [
            FolderTemplateSlot,
            FilenameTemplateSlot,
            DiscFolderTemplateSlot,
            PlaylistFolderTemplateSlot,
            PlaylistFilenameTemplateSlot
        ];
        foreach (var slot in TemplateSlots)
        {
            SubscribeTemplateSlot(slot);
        }

        RefreshFromSettings();
    }

    public event EventHandler<AppSettings>? SettingsSaved;

    public AppSettings Settings { get; }

    public TemplatePresetSlotViewModel FolderTemplateSlot { get; }

    public TemplatePresetSlotViewModel FilenameTemplateSlot { get; }

    public TemplatePresetSlotViewModel DiscFolderTemplateSlot { get; }

    public TemplatePresetSlotViewModel PlaylistFolderTemplateSlot { get; }

    public TemplatePresetSlotViewModel PlaylistFilenameTemplateSlot { get; }

    private IReadOnlyList<TemplatePresetSlotViewModel> TemplateSlots { get; }

    public IReadOnlyList<string> PlaylistOrganizationOptions { get; } =
    [
        AppSettings.DefaultPlaylistOrganization,
        AppSettings.UseStandardTemplatesPlaylistOrganization
    ];

    public IReadOnlyList<string> WorkHandlingOptions { get; } =
    [
        "Inline",
        "Folders",
        "Inline or Folders"
    ];

    [ObservableProperty]
    private string folderTemplate = string.Empty;

    [ObservableProperty]
    private string filenameTemplate = string.Empty;

    [ObservableProperty]
    private string discFolderTemplate = string.Empty;

    [ObservableProperty]
    private string playlistOrganization = AppSettings.DefaultPlaylistOrganization;

    [ObservableProperty]
    private string playlistFolderTemplate = AppSettings.DefaultPlaylistFolderTemplate;

    [ObservableProperty]
    private string playlistFilenameTemplate = AppSettings.DefaultPlaylistFilenameTemplate;

    [ObservableProperty]
    private string discWorkHandling = AppSettings.DefaultDiscWorkHandling;

    [ObservableProperty]
    private string discWorkSeparator = AppSettings.DefaultDiscWorkSeparator;

    [ObservableProperty]
    private bool discWorkSeparatorNoSpaces;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreFolderFieldsCollapsed))]
    private bool isFolderFieldsExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreFilenameFieldsCollapsed))]
    private bool isFilenameFieldsExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreDiscFieldsCollapsed))]
    private bool isDiscFieldsExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArePlaylistFolderFieldsCollapsed))]
    private bool isPlaylistFolderFieldsExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArePlaylistFilenameFieldsCollapsed))]
    private bool isPlaylistFilenameFieldsExpanded;

    public bool AreFolderFieldsCollapsed => !IsFolderFieldsExpanded;

    public bool AreFilenameFieldsCollapsed => !IsFilenameFieldsExpanded;

    public bool AreDiscFieldsCollapsed => !IsDiscFieldsExpanded;

    public bool ArePlaylistFolderFieldsCollapsed => !IsPlaylistFolderFieldsExpanded;

    public bool ArePlaylistFilenameFieldsCollapsed => !IsPlaylistFilenameFieldsExpanded;

    public bool ShowsWorkHandling =>
        TemplateUsesWork(GetActiveTemplate(DiscFolderTemplateSlot, DiscFolderTemplate));

    public bool HasSelectedDiscWorkComposerTemplateWarning =>
        !DiscFolderTemplateSlot.IsEditorOpen && TemplateUsesWorkAndWorkComposer(DiscFolderTemplate);

    public bool HasDraftDiscWorkComposerTemplateWarning =>
        DiscFolderTemplateSlot.IsEditorOpen && TemplateUsesWorkAndWorkComposer(DiscFolderTemplateSlot.DraftTemplate);

    public string DiscWorkComposerTemplateWarningText =>
        "Composer is taken from the first track found for each work.";

    [ObservableProperty]
    private string statusText = "Tags ready";

    [ObservableProperty]
    private string combinedTemplatePreview = string.Empty;

    [ObservableProperty]
    private string playlistTemplatePreview = string.Empty;

    public bool HasFilenameTemplateError => string.IsNullOrWhiteSpace(FilenameTemplate);

    public string FilenameTemplateErrorText => HasFilenameTemplateError
        ? "Filename template is required."
        : string.Empty;

    public bool HasFolderTemplateCoverWarning => string.IsNullOrWhiteSpace(FolderTemplate);

    public string FolderTemplateCoverWarningText => HasFolderTemplateCoverWarning
        ? "Cover art won't be saved when Folder Template is blank."
        : string.Empty;

    public bool UsesPlaylistTemplates => PlaylistOrganization == AppSettings.DefaultPlaylistOrganization;

    public bool UsesStandardTemplatesForPlaylists => !UsesPlaylistTemplates;

    public void RefreshFromSettings()
    {
        foreach (var slot in TemplatePresetCatalog.AllSlots)
        {
            TemplatePresetCatalog.ApplyResolvedPreset(Settings, slot);
        }

        foreach (var slot in TemplateSlots)
        {
            slot.Refresh(Settings);
        }

        FolderTemplate = FolderTemplateSlot.SelectedTemplate;
        FilenameTemplate = FilenameTemplateSlot.SelectedTemplate;
        DiscFolderTemplate = DiscFolderTemplateSlot.SelectedTemplate;
        PlaylistOrganization = IsKnownPlaylistOrganization(Settings.PlaylistOrganization)
            ? Settings.PlaylistOrganization
            : AppSettings.DefaultPlaylistOrganization;
        PlaylistFolderTemplate = PlaylistFolderTemplateSlot.SelectedTemplate;
        PlaylistFilenameTemplate = PlaylistFilenameTemplateSlot.SelectedTemplate;
        DiscWorkHandling = Settings.DiscWorkHandling;
        DiscWorkSeparator = Settings.DiscWorkSeparator;
        DiscWorkSeparatorNoSpaces = Settings.DiscWorkSeparatorNoSpaces;
        UpdatePreview();
    }

    public int InsertTemplateField(string target, string field, int caretIndex)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return caretIndex;
        }

        var token = field.StartsWith('{') && field.EndsWith('}')
            ? field
            : $"{{{field}}}";

        switch (target)
        {
            case "Folder":
                (FolderTemplateSlot.DraftTemplate, caretIndex) = InsertAt(FolderTemplateSlot.DraftTemplate, token, caretIndex);
                break;
            case "Filename":
                (FilenameTemplateSlot.DraftTemplate, caretIndex) = InsertAt(FilenameTemplateSlot.DraftTemplate, token, caretIndex);
                break;
            case "Disc":
                (DiscFolderTemplateSlot.DraftTemplate, caretIndex) = InsertAt(DiscFolderTemplateSlot.DraftTemplate, token, caretIndex);
                break;
            case "PlaylistFolder":
                (PlaylistFolderTemplateSlot.DraftTemplate, caretIndex) = InsertAt(PlaylistFolderTemplateSlot.DraftTemplate, token, caretIndex);
                break;
            case "PlaylistFilename":
                (PlaylistFilenameTemplateSlot.DraftTemplate, caretIndex) = InsertAt(PlaylistFilenameTemplateSlot.DraftTemplate, token, caretIndex);
                break;
        }

        return caretIndex;
    }

    partial void OnFolderTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(HasFolderTemplateCoverWarning));
        OnPropertyChanged(nameof(FolderTemplateCoverWarningText));
        UpdatePreview();
    }

    partial void OnFilenameTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(HasFilenameTemplateError));
        OnPropertyChanged(nameof(FilenameTemplateErrorText));
        SaveCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }

    partial void OnDiscFolderTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(ShowsWorkHandling));
        NotifyDiscWorkComposerWarningChanged();
        UpdatePreview();
    }

    partial void OnPlaylistFolderTemplateChanged(string value)
    {
        UpdatePlaylistPreview();
    }

    partial void OnPlaylistFilenameTemplateChanged(string value)
    {
        UpdatePlaylistPreview();
    }

    partial void OnPlaylistOrganizationChanged(string value)
    {
        OnPropertyChanged(nameof(UsesPlaylistTemplates));
        OnPropertyChanged(nameof(UsesStandardTemplatesForPlaylists));
    }

    partial void OnDiscWorkHandlingChanged(string value) => UpdatePreview();

    partial void OnDiscWorkSeparatorChanged(string value) => UpdatePreview();

    partial void OnDiscWorkSeparatorNoSpacesChanged(bool value) => UpdatePreview();

    [RelayCommand]
    private async Task SaveTemplatePreset(TemplatePresetSlotViewModel? slot)
    {
        if (slot is null || !slot.CommitDraft())
        {
            return;
        }

        ApplyTemplateSlotToSettings(slot);
        await settingsStore.SaveTemplatePresetSlotAsync(slot.Slot, slot.SelectedPresetId, slot.UserPresets);
        StatusText = $"{slot.DisplayName} template saved";
    }

    [RelayCommand]
    private void EditTemplatePreset(TemplatePresetSlotViewModel? slot)
    {
        if (slot is null)
        {
            return;
        }

        CloseOtherTemplateEditors(slot);
        CollapseTemplateFields(slot);
        slot.BeginEdit();
    }

    [RelayCommand]
    private void NewTemplatePreset(TemplatePresetSlotViewModel? slot)
    {
        if (slot is null)
        {
            return;
        }

        CloseOtherTemplateEditors(slot);
        CollapseTemplateFields(slot);
        slot.BeginNew();
    }

    [RelayCommand]
    private void CancelTemplatePreset(TemplatePresetSlotViewModel? slot)
    {
        if (slot is null)
        {
            return;
        }

        slot.CancelEdit();
        CollapseTemplateFields(slot);
    }

    [RelayCommand]
    private async Task DeleteTemplatePreset(TemplatePresetSlotViewModel? slot)
    {
        if (slot is null || !slot.DeleteSelected())
        {
            return;
        }

        ApplyTemplateSlotToSettings(slot);
        await settingsStore.SaveTemplatePresetSlotAsync(slot.Slot, slot.SelectedPresetId, slot.UserPresets);
        StatusText = $"{slot.DisplayName} template deleted";
    }

    private TemplatePresetSlotViewModel CreateTemplateSlot(
        string slot,
        string displayName,
        bool allowBlankTemplate,
        Func<string, string?>? templateValidator = null)
    {
        return new TemplatePresetSlotViewModel(
            slot,
            displayName,
            allowBlankTemplate,
            OnTemplateSlotActivePresetChanged,
            templateValidator);
    }

    private void SubscribeTemplateSlot(TemplatePresetSlotViewModel slot)
    {
        slot.PropertyChanged += (_, args) =>
        {
            if (!IsPreviewTemplateProperty(args.PropertyName))
            {
                return;
            }

            if (slot == DiscFolderTemplateSlot)
            {
                OnPropertyChanged(nameof(ShowsWorkHandling));
                NotifyDiscWorkComposerWarningChanged();
            }

            if (slot == PlaylistFolderTemplateSlot || slot == PlaylistFilenameTemplateSlot)
            {
                UpdatePlaylistPreview();
                return;
            }

            UpdatePreview();
        };
    }

    private void CloseOtherTemplateEditors(TemplatePresetSlotViewModel activeSlot)
    {
        foreach (var slot in TemplateSlots.Where(slot => slot != activeSlot))
        {
            slot.CancelEdit();
            CollapseTemplateFields(slot);
        }
    }

    private void CollapseTemplateFields(TemplatePresetSlotViewModel slot)
    {
        switch (slot.Slot)
        {
            case TemplatePresetSlots.Folder:
                IsFolderFieldsExpanded = false;
                break;
            case TemplatePresetSlots.Filename:
                IsFilenameFieldsExpanded = false;
                break;
            case TemplatePresetSlots.DiscFolder:
                IsDiscFieldsExpanded = false;
                break;
            case TemplatePresetSlots.PlaylistFolder:
                IsPlaylistFolderFieldsExpanded = false;
                break;
            case TemplatePresetSlots.PlaylistFilename:
                IsPlaylistFilenameFieldsExpanded = false;
                break;
        }
    }

    private void OnTemplateSlotActivePresetChanged(TemplatePresetSlotViewModel slot)
    {
        ApplyTemplateSlotToSettings(slot);
    }

    private void ApplyTemplateSlotToSettings(TemplatePresetSlotViewModel slot)
    {
        TemplatePresetCatalog.SetSelectedPresetId(Settings, slot.Slot, slot.SelectedPresetId);
        TemplatePresetCatalog.SetUserPresets(Settings, slot.Slot, slot.UserPresets);
        TemplatePresetCatalog.SetTemplateValue(Settings, slot.Slot, slot.SelectedTemplate);

        switch (slot.Slot)
        {
            case TemplatePresetSlots.Folder:
                FolderTemplate = slot.SelectedTemplate;
                IsFolderFieldsExpanded = false;
                break;
            case TemplatePresetSlots.Filename:
                FilenameTemplate = slot.SelectedTemplate;
                IsFilenameFieldsExpanded = false;
                break;
            case TemplatePresetSlots.DiscFolder:
                DiscFolderTemplate = slot.SelectedTemplate;
                IsDiscFieldsExpanded = false;
                OnPropertyChanged(nameof(ShowsWorkHandling));
                NotifyDiscWorkComposerWarningChanged();
                break;
            case TemplatePresetSlots.PlaylistFolder:
                PlaylistFolderTemplate = slot.SelectedTemplate;
                IsPlaylistFolderFieldsExpanded = false;
                break;
            case TemplatePresetSlots.PlaylistFilename:
                PlaylistFilenameTemplate = slot.SelectedTemplate;
                IsPlaylistFilenameFieldsExpanded = false;
                break;
        }
    }

    [RelayCommand]
    private void ToggleFolderFields()
    {
        IsFolderFieldsExpanded = !IsFolderFieldsExpanded;
    }

    [RelayCommand]
    private void ToggleFilenameFields()
    {
        IsFilenameFieldsExpanded = !IsFilenameFieldsExpanded;
    }

    [RelayCommand]
    private void ToggleDiscFields()
    {
        IsDiscFieldsExpanded = !IsDiscFieldsExpanded;
    }

    [RelayCommand]
    private void TogglePlaylistFolderFields()
    {
        IsPlaylistFolderFieldsExpanded = !IsPlaylistFolderFieldsExpanded;
    }

    [RelayCommand]
    private void TogglePlaylistFilenameFields()
    {
        IsPlaylistFilenameFieldsExpanded = !IsPlaylistFilenameFieldsExpanded;
    }

    private void UpdatePreview()
    {
        try
        {
            var folderTemplate = GetActiveTemplate(FolderTemplateSlot, FolderTemplate);
            var baseFolder = Settings.EffectiveDownloadFolder;
            var context = CreateStandardPreviewContext();
            var works = new[] { "Example Work No. 1", "Example Work No. 2" };

            var albumDestination = PathTemplateRenderer.RenderAlbumDestinationPreview(
                baseFolder,
                folderTemplate,
                context.AlbumArtist,
                context.AlbumTitle,
                context.Quality,
                context.ReleaseDate,
                context.TotalTracks,
                context.Version,
                context.ReleaseType,
                context.Label,
                context.Upc,
                context.TotalDiscs,
                context.ExplicitAdvisory);
            var folderSegments = StringTools.GetRelativeSegments(baseFolder, albumDestination);

            var builder = new StringBuilder();
            builder.AppendLine(baseFolder);
            var ancestors = new List<bool>();
            foreach (var segment in folderSegments)
            {
                StringTools.AppendTreeLeaf(builder, ancestors, segment, isLast: true);
                ancestors.Add(false);
            }

            AppendWorkPreview(
                builder,
                ancestors,
                ShowsWorkHandling,
                context,
                works);
            CombinedTemplatePreview = builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            CombinedTemplatePreview = $"Preview failed: {ex.Message}";
        }

        UpdatePlaylistPreview();
    }

    private void UpdatePlaylistPreview()
    {
        try
        {
            var playlistFolderTemplate = GetActiveTemplate(PlaylistFolderTemplateSlot, PlaylistFolderTemplate);
            var playlistFilenameTemplate = GetActiveTemplate(PlaylistFilenameTemplateSlot, PlaylistFilenameTemplate);
            const string playlistId = "100000";
            const string playlistTitle = "Road Trip";
            const string playlistOwner = "MusicEnjoyer";
            const string albumArtist = "Example Artist";
            const string albumTitle = "Example Album";
            const string quality = "FLAC 24/96";
            const int playlistTotalTracks = 1900;

            var album = new Album
            {
                Id = "1",
                Title = albumTitle,
                Artist = new Artist { Name = albumArtist },
                Version = "Remastered Edition",
                ReleaseDateOriginal = DateTime.Today,
                ReleaseType = "album",
                Label = new Label { Name = "Example Records" },
                Upc = "0000000000000",
                TracksCount = 10,
                MediaCount = 1
            };

            var track = new Track
            {
                Id = 1,
                Title = "Example Track",
                Version = "Remastered Edition",
                TrackNumber = 1,
                MediaNumber = 1,
                Album = album,
                Performer = new Artist { Name = albumArtist },
                Isrc = "GBUM72600001"
            };

            var baseFolder = Settings.EffectiveDownloadFolder;
            var playlistDestination = PathTemplateRenderer.RenderPlaylistDestination(
                baseFolder,
                playlistFolderTemplate,
                playlistId,
                playlistTitle,
                playlistOwner,
                track,
                album,
                albumArtist,
                albumTitle,
                quality,
                playlistNumber: 1,
                playlistTotalTracks);

            var fileName = PathTemplateRenderer.RenderPlaylistAudioFilename(
                playlistFilenameTemplate,
                track,
                album,
                albumArtist,
                albumTitle,
                quality,
                playlistId,
                playlistTitle,
                playlistOwner,
                playlistNumber: 1,
                playlistTotalTracks,
                extension: ".flac",
                Settings.MaxFileNameLength);

            var builder = new StringBuilder();
            builder.AppendLine(baseFolder);
            var ancestors = new List<bool>();
            foreach (var segment in StringTools.GetRelativeSegments(baseFolder, playlistDestination))
            {
                StringTools.AppendTreeLeaf(builder, ancestors, segment, isLast: true);
                ancestors.Add(false);
            }

            StringTools.AppendTreeLeaf(builder, ancestors, fileName, isLast: false);
            StringTools.AppendTreeLeaf(builder, ancestors, $"{playlistTotalTracks - 1} more", isLast: true);
            PlaylistTemplatePreview = builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            PlaylistTemplatePreview = $"Preview failed: {ex.Message}";
        }
    }

    private void AppendWorkPreview(
        StringBuilder builder,
        IReadOnlyList<bool> ancestors,
        bool useWorkHandling,
        StandardPreviewContext context,
        IReadOnlyList<string> works)
    {
        var mode = useWorkHandling
            ? DiscWorkHandling switch
            {
                "Folders" => "Folders",
                "Inline or Folders" => "Inline or Folders",
                _ => "Inline"
            }
            : "Inline";

        void AppendDisc(
            int discNumber,
            IReadOnlyList<string> discWorks,
            string currentWork,
            bool includeTrack,
            int moreCount,
            bool hasLaterDisc)
        {
            AppendDiscPath(
                builder,
                ancestors,
                useWorkHandling,
                context,
                discNumber,
                discWorks,
                currentWork,
                includeTrack,
                moreCount,
                hasLaterDisc);
        }

        if (mode == "Folders")
        {
            AppendDisc(discNumber: 1, [works[0]], works[0], includeTrack: true, moreCount: 4, hasLaterDisc: true);
            AppendDisc(discNumber: 2, [works[1]], works[1], includeTrack: false, moreCount: 5, hasLaterDisc: false);
            return;
        }

        if (mode == "Inline or Folders")
        {
            AppendDisc(discNumber: 1, [works[0]], works[0], includeTrack: true, moreCount: 4, hasLaterDisc: true);
            AppendDisc(discNumber: 2, works, works[0], includeTrack: false, moreCount: 5, hasLaterDisc: false);
            return;
        }

        AppendDisc(discNumber: 1, works, works[0], includeTrack: true, moreCount: context.TotalTracks - 1, hasLaterDisc: false);
    }

    private void AppendDiscPath(
        StringBuilder builder,
        IReadOnlyList<bool> ancestors,
        bool useWorkHandling,
        StandardPreviewContext context,
        int discNumber,
        IReadOnlyList<string> works,
        string currentWork,
        bool includeTrack,
        int moreCount,
        bool hasLaterDisc)
    {
        var discSegments = PathTemplateRenderer.RenderDiscFolderSegmentsPreview(
            context.DiscFolderTemplate,
            useWorkHandling ? DiscWorkHandling : AppSettings.DefaultDiscWorkHandling,
            useWorkHandling ? DiscWorkSeparator : AppSettings.DefaultDiscWorkSeparator,
            useWorkHandling && DiscWorkSeparatorNoSpaces,
            context.AlbumArtist,
            context.AlbumTitle,
            context.Quality,
            context.ReleaseDate,
            context.TotalTracks,
            trackNumber: 1,
            context.TrackTitle,
            context.Version,
            discNumber,
            context.TotalDiscs,
            works,
            currentWork,
            workComposer: "Example Work Composer");

        var currentAncestors = ancestors.ToList();
        if (!includeTrack && works.Count > 1 && discSegments.Count >= 2)
        {
            StringTools.AppendTreeLeaf(builder, currentAncestors, discSegments[0], isLast: !hasLaterDisc);
            currentAncestors.Add(hasLaterDisc);
            for (var index = 0; index < works.Count; index++)
            {
                StringTools.AppendTreeLeaf(builder, currentAncestors, works[index], isLast: index == works.Count - 1);
            }

            return;
        }

        for (var index = 0; index < discSegments.Count; index++)
        {
            var isLast = index == 0
                ? !hasLaterDisc
                : true;
            StringTools.AppendTreeLeaf(builder, currentAncestors, discSegments[index], isLast);
            currentAncestors.Add(!isLast);
        }

        if (!includeTrack)
        {
            if (moreCount > 0)
            {
                StringTools.AppendTreeLeaf(builder, currentAncestors, $"{moreCount} tracks", isLast: true);
            }

            return;
        }

        var fileName = PathTemplateRenderer.RenderAudioFilenamePreview(
            context.FilenameTemplate,
            context.AlbumArtist,
            context.AlbumTitle,
            context.Quality,
            context.ReleaseDate,
            context.TotalTracks,
            trackNumber: 1,
            context.TrackTitle,
            context.Version,
            discNumber,
            context.TotalDiscs,
            extension: ".flac",
            context.MaxFileNameLength,
            context.ReleaseType,
            context.Label,
            context.Upc,
            context.Isrc,
            context.ExplicitAdvisory);

        StringTools.AppendTreeLeaf(builder, currentAncestors, fileName, isLast: moreCount == 0);
        if (moreCount > 0)
        {
            StringTools.AppendTreeLeaf(builder, currentAncestors, $"{moreCount} more", isLast: true);
        }
    }

    private static bool IsKnownPlaylistOrganization(string? value)
    {
        return value is AppSettings.DefaultPlaylistOrganization or AppSettings.UseStandardTemplatesPlaylistOrganization;
    }

    private static bool TemplateUsesWork(string? template)
    {
        return template?.Contains("{Work}", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool TemplateUsesWorkComposer(string? template)
    {
        return template?.Contains("{WorkComposer}", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool TemplateUsesWorkAndWorkComposer(string? template)
    {
        return TemplateUsesWork(template) && TemplateUsesWorkComposer(template);
    }

    private static string? ValidateDiscFolderTemplate(string template)
    {
        return TemplateUsesWorkComposer(template) && !TemplateUsesWork(template)
            ? "Work Composer can only be used with Work in disc folder templates."
            : null;
    }

    private void NotifyDiscWorkComposerWarningChanged()
    {
        OnPropertyChanged(nameof(HasSelectedDiscWorkComposerTemplateWarning));
        OnPropertyChanged(nameof(HasDraftDiscWorkComposerTemplateWarning));
        OnPropertyChanged(nameof(DiscWorkComposerTemplateWarningText));
    }

    private StandardPreviewContext CreateStandardPreviewContext()
    {
        return new StandardPreviewContext(
            FilenameTemplate: GetActiveTemplate(FilenameTemplateSlot, FilenameTemplate),
            DiscFolderTemplate: GetActiveTemplate(DiscFolderTemplateSlot, DiscFolderTemplate),
            AlbumArtist: "Example Artist",
            AlbumTitle: "Example Album",
            Quality: "FLAC 24/96",
            ReleaseDate: StringTools.FormatDateTimeOffset(DateTime.Today),
            ReleaseType: "Album",
            Label: "Example Records",
            Upc: "0000000000000",
            Isrc: "GBUM72600001",
            ExplicitAdvisory: true,
            TrackTitle: "Example Track",
            Version: "Remastered Edition",
            TotalTracks: 10,
            TotalDiscs: 2,
            MaxFileNameLength: Settings.MaxFileNameLength);
    }

    private static string GetActiveTemplate(TemplatePresetSlotViewModel slot, string savedTemplate)
    {
        return slot.IsEditorOpen ? slot.DraftTemplate : savedTemplate;
    }

    private static bool IsPreviewTemplateProperty(string? propertyName)
    {
        return propertyName is nameof(TemplatePresetSlotViewModel.SelectedTemplate) or
            nameof(TemplatePresetSlotViewModel.DraftTemplate) or
            nameof(TemplatePresetSlotViewModel.IsEditorOpen);
    }

    private static (string Text, int CaretIndex) InsertAt(string template, string token, int caretIndex)
    {
        var index = Math.Clamp(caretIndex, 0, template.Length);
        var prefix = index > 0 && NeedsSpaceBeforeInsertedToken(template[index - 1]) ? " " : string.Empty;
        var suffix = index < template.Length && NeedsSpaceAfterInsertedToken(template[index]) ? " " : string.Empty;
        var insertion = $"{prefix}{token}{suffix}";
        return (template.Insert(index, insertion), index + prefix.Length + token.Length);
    }

    private static bool NeedsSpaceBeforeInsertedToken(char previous)
    {
        return previous is '}' or ')' or ']';
    }

    private static bool NeedsSpaceAfterInsertedToken(char next)
    {
        return next is '{' or '(' or '[';
    }

    private sealed record StandardPreviewContext(
        string FilenameTemplate,
        string DiscFolderTemplate,
        string AlbumArtist,
        string AlbumTitle,
        string Quality,
        string ReleaseDate,
        string ReleaseType,
        string Label,
        string Upc,
        string Isrc,
        bool ExplicitAdvisory,
        string TrackTitle,
        string Version,
        int TotalTracks,
        int TotalDiscs,
        int MaxFileNameLength);

    private bool CanSave() => !HasFilenameTemplateError;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        foreach (var slot in TemplateSlots)
        {
            if (!slot.CommitDraft())
            {
                StatusText = $"{slot.DisplayName}: {slot.ErrorText}";
                return;
            }

            ApplyTemplateSlotToSettings(slot);
        }

        if (!CanSave())
        {
            StatusText = FilenameTemplateErrorText;
            return;
        }

        Settings.PlaylistOrganization = IsKnownPlaylistOrganization(PlaylistOrganization)
            ? PlaylistOrganization
            : AppSettings.DefaultPlaylistOrganization;
        Settings.DiscWorkHandling = DiscWorkHandling;
        Settings.DiscWorkSeparator = string.IsNullOrEmpty(DiscWorkSeparator)
            ? AppSettings.DefaultDiscWorkSeparator
            : DiscWorkSeparator;
        Settings.DiscWorkSeparatorNoSpaces = DiscWorkSeparatorNoSpaces;
        await settingsStore.SaveAsync(Settings);
        SettingsSaved?.Invoke(this, Settings);
        StatusText = "Tags saved";
    }
}
