using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class TagsViewModelTests
{
    [Fact]
    public void StandardPreviewUsesOpenTemplateDrafts()
    {
        var viewModel = CreateViewModel();

        viewModel.NewTemplatePresetCommand.Execute(viewModel.FolderTemplateSlot);
        viewModel.FolderTemplateSlot.DraftTemplate = @"DraftRoot\{AlbumTitle}";

        Assert.Contains("DraftRoot", viewModel.CombinedTemplatePreview);
        Assert.Equal(AppSettings.DefaultFolderTemplate, viewModel.FolderTemplate);

        viewModel.NewTemplatePresetCommand.Execute(viewModel.FilenameTemplateSlot);
        viewModel.FilenameTemplateSlot.DraftTemplate = "Draft - {TrackTitle}";

        Assert.Contains("Draft - Example Track.flac", viewModel.CombinedTemplatePreview);
        Assert.Equal(AppSettings.DefaultFilenameTemplate, viewModel.FilenameTemplate);
    }

    [Fact]
    public void PlaylistPreviewUsesOpenTemplateDrafts()
    {
        var viewModel = CreateViewModel();

        viewModel.NewTemplatePresetCommand.Execute(viewModel.PlaylistFolderTemplateSlot);
        viewModel.PlaylistFolderTemplateSlot.DraftTemplate = @"DraftPlaylists\{PlaylistTitle}";

        Assert.Contains("DraftPlaylists", viewModel.PlaylistTemplatePreview);
        Assert.Equal(AppSettings.DefaultPlaylistFolderTemplate, viewModel.PlaylistFolderTemplate);

        viewModel.NewTemplatePresetCommand.Execute(viewModel.PlaylistFilenameTemplateSlot);
        viewModel.PlaylistFilenameTemplateSlot.DraftTemplate = "{PlaylistNumberPadded} draft {TrackTitle}";

        Assert.Contains("0001 draft Example Track.flac", viewModel.PlaylistTemplatePreview);
        Assert.Equal(AppSettings.DefaultPlaylistFilenameTemplate, viewModel.PlaylistFilenameTemplate);
    }

    [Fact]
    public void TemplatePreviewsUseConnectedTreePrefixes()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(
            """
            D:\Sort
            └─ Example Artist
               └─ (2026) Example Album (Remastered Edition) [FLAC (24bit-96kHz)]
                  └─ Disc 01 - Example Work No. 1 & Example Work No. 2
                     ├─ 01 - Example Track (Remastered Edition).flac
                     └─ 9 more
            """.ReplaceLineEndings("\n"),
            viewModel.CombinedTemplatePreview.ReplaceLineEndings("\n"));

        Assert.Equal(
            """
            D:\Sort
            └─ Playlists
               └─ Road Trip
                  ├─ 0001 - Example Artist - Example Track (Remastered Edition).flac
                  └─ 1899 more
            """.ReplaceLineEndings("\n"),
            viewModel.PlaylistTemplatePreview.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MultiDiscWorkPreviewUsesConnectedTreePrefixes()
    {
        var viewModel = CreateViewModel();

        viewModel.DiscWorkHandling = "Folders";

        Assert.Equal(
            """
            D:\Sort
            └─ Example Artist
               └─ (2026) Example Album (Remastered Edition) [FLAC (24bit-96kHz)]
                  ├─ Disc 01
                  │  └─ Example Work No. 1
                  │     ├─ 01 - Example Track (Remastered Edition).flac
                  │     └─ 4 more
                  └─ Disc 02
                     └─ Example Work No. 2
                        └─ 5 tracks
            """.ReplaceLineEndings("\n"),
            viewModel.CombinedTemplatePreview.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void DiscWorkComposerWarningTracksSelectedAndDraftTemplates()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasSelectedDiscWorkComposerTemplateWarning);
        Assert.False(viewModel.HasDraftDiscWorkComposerTemplateWarning);

        viewModel.DiscFolderTemplate = "Disc {DiscNumberPadded} - {Work} ({WorkComposer})";

        Assert.True(viewModel.HasSelectedDiscWorkComposerTemplateWarning);
        Assert.False(viewModel.HasDraftDiscWorkComposerTemplateWarning);

        viewModel.NewTemplatePresetCommand.Execute(viewModel.DiscFolderTemplateSlot);

        Assert.False(viewModel.HasSelectedDiscWorkComposerTemplateWarning);
        Assert.False(viewModel.HasDraftDiscWorkComposerTemplateWarning);

        viewModel.DiscFolderTemplateSlot.DraftTemplate = "{Work} ({WorkComposer})";

        Assert.False(viewModel.HasSelectedDiscWorkComposerTemplateWarning);
        Assert.True(viewModel.HasDraftDiscWorkComposerTemplateWarning);
    }

    [Fact]
    public void DiscFolderTemplateRejectsComposerWithoutWork()
    {
        var viewModel = CreateViewModel();

        viewModel.NewTemplatePresetCommand.Execute(viewModel.DiscFolderTemplateSlot);
        viewModel.DiscFolderTemplateSlot.DraftName = "Composer Only";
        viewModel.DiscFolderTemplateSlot.DraftTemplate = "Disc {DiscNumberPadded} - {WorkComposer}";

        Assert.True(viewModel.DiscFolderTemplateSlot.HasTemplateValidationError);
        Assert.Contains("Work Composer can only be used with Work", viewModel.DiscFolderTemplateSlot.TemplateValidationErrorText);

        viewModel.DiscFolderTemplateSlot.DraftTemplate = "Disc {DiscNumberPadded} - {Work} ({WorkComposer})";

        Assert.False(viewModel.DiscFolderTemplateSlot.HasTemplateValidationError);
        Assert.Equal(string.Empty, viewModel.DiscFolderTemplateSlot.TemplateValidationErrorText);

        viewModel.DiscFolderTemplateSlot.DraftTemplate = "Disc {DiscNumberPadded} - {WorkComposer}";

        var committed = viewModel.DiscFolderTemplateSlot.CommitDraft();

        Assert.False(committed);
        Assert.True(viewModel.DiscFolderTemplateSlot.HasError);
        Assert.Contains("Work Composer can only be used with Work", viewModel.DiscFolderTemplateSlot.ErrorText);
        Assert.True(viewModel.DiscFolderTemplateSlot.IsEditorOpen);
    }

    private static TagsViewModel CreateViewModel()
    {
        return new TagsViewModel(new MemorySettingsStore());
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public AppSettings Current { get; } = new() { DownloadFolder = @"D:\Sort" };

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Current);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveTemplatePresetSlotAsync(
            string slot,
            string selectedPresetId,
            IReadOnlyList<TemplatePreset> userPresets,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
