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

        var expectedStandardPreview = string.Join(
            '\n',
            TestPaths.DownloadRoot,
            "\u2514\u2500 Example Artist",
            "   \u2514\u2500 (2026) Example Album (Remastered Edition) [FLAC (24bit-96kHz)]",
            "      \u2514\u2500 Disc 01 - Example Work No. 1 & Example Work No. 2",
            "         \u251c\u2500 01 - Example Track (Remastered Edition).flac",
            "         \u2514\u2500 9 more");

        Assert.Equal(expectedStandardPreview, viewModel.CombinedTemplatePreview.ReplaceLineEndings("\n"));

        var expectedPlaylistPreview = string.Join(
            '\n',
            TestPaths.DownloadRoot,
            "\u2514\u2500 Playlists",
            "   \u2514\u2500 Road Trip",
            "      \u251c\u2500 0001 - Example Artist - Example Track (Remastered Edition).flac",
            "      \u2514\u2500 1899 more");

        Assert.Equal(expectedPlaylistPreview, viewModel.PlaylistTemplatePreview.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MultiDiscWorkPreviewUsesConnectedTreePrefixes()
    {
        var viewModel = CreateViewModel();

        viewModel.DiscWorkHandling = "Folders";

        var expected = string.Join(
            '\n',
            TestPaths.DownloadRoot,
            "\u2514\u2500 Example Artist",
            "   \u2514\u2500 (2026) Example Album (Remastered Edition) [FLAC (24bit-96kHz)]",
            "      \u251c\u2500 Disc 01",
            "      \u2502  \u2514\u2500 Example Work No. 1",
            "      \u2502     \u251c\u2500 01 - Example Track (Remastered Edition).flac",
            "      \u2502     \u2514\u2500 4 more",
            "      \u2514\u2500 Disc 02",
            "         \u2514\u2500 Example Work No. 2",
            "            \u2514\u2500 5 tracks");

        Assert.Equal(expected, viewModel.CombinedTemplatePreview.ReplaceLineEndings("\n"));
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
        public AppSettings Current { get; } = new() { DownloadFolder = TestPaths.DownloadRoot };

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
