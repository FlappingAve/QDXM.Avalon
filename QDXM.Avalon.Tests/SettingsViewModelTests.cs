using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task SaveRejectsMaxFileNameLengthAboveRange()
    {
        var store = new MemorySettingsStore(new AppSettings { MaxFileNameLength = 80 });
        var viewModel = new SettingsViewModel(store)
        {
            MaxFileNameLengthValue = 101
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(80, store.Current.MaxFileNameLength);
        Assert.Equal(0, store.SaveCount);
        Assert.True(viewModel.HasMaxFileNameLengthError);
    }

    [Fact]
    public async Task SaveStoresDownloadFolderAndValidMaxFileNameLength()
    {
        var store = new MemorySettingsStore(new AppSettings
        {
            DownloadFolder = @"D:\Before",
            MaxFileNameLength = 80
        });
        var viewModel = new SettingsViewModel(store)
        {
            DownloadFolder = @"D:\After",
            MaxFileNameLengthValue = 90
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(@"D:\After", store.Current.DownloadFolder);
        Assert.Equal(90, store.Current.MaxFileNameLength);
        Assert.Equal(1, store.SaveCount);
        Assert.False(viewModel.HasMaxFileNameLengthError);
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public MemorySettingsStore(AppSettings settings)
        {
            Current = settings;
        }

        public AppSettings Current { get; }

        public int SaveCount { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Current);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCount++;
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
