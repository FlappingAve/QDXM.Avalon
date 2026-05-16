using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Services;
using QDXM.Avalon.ViewModels;
using Avalonia.Media;
using System.Reflection;

namespace QDXM.Avalon.Tests;

public sealed class PlaylistQueueStateTests
{
    [Fact]
    public void DownloadQueueItemViewModel_PlaylistProgressTextShowsTrackCountWhenWarningExists()
    {
        var item = new DownloadQueueItemViewModel
        {
            Type = DownloadContentType.Playlist,
            TotalTracks = 90,
            CompletedTracks = 66,
            WarningMessage = "1 playlist tracks failed. See Logs."
        };

        Assert.Equal("66/90 tracks", item.ProgressText);
        Assert.Equal("1 playlist tracks failed. See Logs.", item.DetailMessage);
        Assert.Equal(Brushes.Yellow, item.StatusBrush);
        Assert.Equal(Brushes.Yellow, item.ProgressBrush);
    }

    [Fact]
    public void DownloadQueueItemViewModel_PausedUsesOrange()
    {
        var item = new DownloadQueueItemViewModel
        {
            Status = DownloadStatus.Paused
        };

        Assert.Equal(Brushes.Orange, item.StatusBrush);
        Assert.Equal(Brushes.Orange, item.ProgressBrush);
    }

    [Fact]
    public void LogEntryViewModel_WarningUsesYellow()
    {
        var item = new LogEntryViewModel(
            new AppLogEntry(DateTimeOffset.UnixEpoch, "Warning", "Downloads", "Completed with warnings."));

        Assert.Equal(Brushes.Yellow, item.LevelBrush);
    }

    [Fact]
    public void DownloadsViewModel_CompletedWithWarningsUsesIssuesStatusForTracks()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = new DownloadQueueItemViewModel
        {
            Type = DownloadContentType.Track,
            TotalTracks = 1,
            Status = DownloadStatus.Downloading
        };

        ApplyDownloadEvent(viewModel, item, new DownloadWarningEvent(
            item.Id,
            "Example Track quality was reduced to FLAC 24/88.2 after the requested FLAC stream failed."));
        ApplyDownloadEvent(viewModel, item, new DownloadCompletedEvent(item.Id, HasWarnings: true));

        Assert.Equal(DownloadStatus.Issues, item.Status);
        Assert.Equal(Brushes.Yellow, item.StatusBrush);
        Assert.Contains("quality was reduced", item.WarningMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void DownloadsViewModel_CompletedJobReleasesTransientDownloadState()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/track/123",
            DownloadContentType.Track,
            "123"));
        item.Status = DownloadStatus.Downloading;
        item.CurrentTrackTitle = "01 - Example Track";
        item.CurrentTrackBytesReceived = 123;
        item.CurrentTrackTotalBytes = 456;
        item.FileProgressFraction = 0.25d;

        ApplyDownloadEvent(viewModel, item, new DownloadCompletedEvent(item.Id, HasWarnings: false));

        Assert.Equal(DownloadStatus.Completed, item.Status);
        Assert.Equal(string.Empty, item.CurrentTrackTitle);
        Assert.Equal(0, item.CurrentTrackBytesReceived);
        Assert.Null(item.CurrentTrackTotalBytes);
        Assert.Equal(1d, item.FileProgressFraction);
        Assert.Equal(1d, item.ProgressBarValue);
    }

    [Fact]
    public void DownloadsViewModel_NonRetryableIssuesJobReleasesTransientDownloadState()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/track/123",
            DownloadContentType.Track,
            "123"));
        item.Status = DownloadStatus.Downloading;
        item.CurrentTrackTitle = "01 - Example Track";
        item.CurrentTrackBytesReceived = 123;
        item.CurrentTrackTotalBytes = 456;
        item.FileProgressFraction = 0.25d;

        ApplyDownloadEvent(viewModel, item, new DownloadWarningEvent(
            item.Id,
            "Example Track quality was reduced to FLAC 16/44.1 after the requested FLAC stream failed."));
        ApplyDownloadEvent(viewModel, item, new DownloadCompletedEvent(item.Id, HasWarnings: true));

        Assert.Equal(DownloadStatus.Issues, item.Status);
        Assert.False(viewModel.IsSelectedItemRetryable);
        Assert.Contains("quality was reduced", item.WarningMessage, StringComparison.Ordinal);
        Assert.Equal(string.Empty, item.CurrentTrackTitle);
        Assert.Equal(0, item.CurrentTrackBytesReceived);
        Assert.Null(item.CurrentTrackTotalBytes);
        Assert.Equal(1d, item.FileProgressFraction);
    }

    [Fact]
    public void DownloadsViewModel_RetryableIssuesJobKeepsTransientDownloadState()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/playlist/11932795",
            DownloadContentType.Playlist,
            "11932795"));
        item.Status = DownloadStatus.Downloading;
        item.CurrentTrackBytesReceived = 123;
        item.CurrentTrackTotalBytes = 456;
        item.FileProgressFraction = 0.25d;

        ApplyDownloadEvent(viewModel, item, new DownloadWarningEvent(
            item.Id,
            "1 playlist tracks failed. See Logs."));
        ApplyDownloadEvent(viewModel, item, new DownloadCompletedEvent(item.Id, HasWarnings: true));

        Assert.Equal(DownloadStatus.Issues, item.Status);
        Assert.True(viewModel.IsSelectedItemRetryable);
        Assert.Equal(123, item.CurrentTrackBytesReceived);
        Assert.Equal(456, item.CurrentTrackTotalBytes);
    }

    [Fact]
    public async Task DownloadsViewModel_PlaylistSizeAccumulatesAcrossTracks()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var runner = new ScriptedDownloadJobRunner(
            itemId =>
                [
                    new DownloadResolvedEvent(
                        itemId,
                        DownloadContentType.Playlist,
                        "Road Trip",
                        "MusicEnjoyer",
                        "FLAC 24/96",
                        TotalTracks: 2,
                        CoverArtUrl: string.Empty,
                        ReleaseDate: string.Empty,
                        Upc: string.Empty,
                        DestinationPath: @"D:\Sort\Playlists\Road Trip",
                        FilePaths: []),
                    new TrackStartedEvent(itemId, TrackNumber: 1, TotalTracks: 2, TrackTitle: "First"),
                    new FileProgressEvent(itemId, BytesReceived: 5, TotalBytes: 10, MegabytesPerSecond: 1),
                    new TrackCompletedEvent(itemId, CompletedTracks: 1, TotalTracks: 2, FilePath: @"D:\Sort\first.flac", FileSizeBytes: 10),
                    new TrackStartedEvent(itemId, TrackNumber: 2, TotalTracks: 2, TrackTitle: "Second"),
                    new FileProgressEvent(itemId, BytesReceived: 20, TotalBytes: 25, MegabytesPerSecond: 1),
                    new TrackCompletedEvent(itemId, CompletedTracks: 2, TotalTracks: 2, FilePath: @"D:\Sort\second.flac", FileSizeBytes: 25),
                    new DownloadCompletedEvent(itemId, HasWarnings: false)
                ]);
        var viewModel = new DownloadsViewModel(
            runner,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());

        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/playlist/11932795",
            DownloadContentType.Playlist,
            "11932795"));

        await WaitUntil(() => item.Status == DownloadStatus.Completed);

        Assert.Null(item.SizeBytes);
        Assert.Equal(35, item.CompletedSizeBytes);
        Assert.Equal(0, item.CurrentTrackBytesReceived);
        Assert.Null(item.CurrentTrackTotalBytes);
    }

    [Fact]
    public void DownloadsViewModel_PlaylistResumePreservesCompletedSizeAfterResolve()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = new DownloadQueueItemViewModel
        {
            Type = DownloadContentType.Playlist,
            CompletedTracks = 1,
            TotalTracks = 2,
            CompletedSizeBytes = 10,
            Status = DownloadStatus.Queued
        };

        ApplyDownloadEvent(viewModel, item, new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Playlist,
            "Road Trip",
            "MusicEnjoyer",
            "FLAC 24/96",
            TotalTracks: 2,
            CoverArtUrl: string.Empty,
            ReleaseDate: string.Empty,
            Upc: string.Empty,
            DestinationPath: @"D:\Sort\Playlists\Road Trip",
            FilePaths: []));
        ApplyDownloadEvent(viewModel, item, new TrackCompletedEvent(
            item.Id,
            CompletedTracks: 2,
            TotalTracks: 2,
            FilePath: @"D:\Sort\second.flac",
            FileSizeBytes: 25));

        Assert.Null(item.SizeBytes);
        Assert.Equal(35, item.CompletedSizeBytes);
        Assert.Equal(0, item.CurrentTrackBytesReceived);
        Assert.Null(item.CurrentTrackTotalBytes);
    }

    [Fact]
    public void DownloadsViewModel_AlbumResumePreservesCompletedSizeAfterResolve()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = new DownloadQueueItemViewModel
        {
            Type = DownloadContentType.Album,
            CompletedTracks = 1,
            TotalTracks = 2,
            CompletedSizeBytes = 10,
            Status = DownloadStatus.Queued
        };

        ApplyDownloadEvent(viewModel, item, new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Album,
            "Example Album",
            "Example Artist",
            "FLAC 24/96",
            TotalTracks: 2,
            CoverArtUrl: string.Empty,
            ReleaseDate: string.Empty,
            Upc: string.Empty,
            DestinationPath: @"D:\Sort\Example Artist\Example Album",
            FilePaths: []));
        ApplyDownloadEvent(viewModel, item, new TrackCompletedEvent(
            item.Id,
            CompletedTracks: 2,
            TotalTracks: 2,
            FilePath: @"D:\Sort\second.flac",
            FileSizeBytes: 25));

        Assert.Null(item.SizeBytes);
        Assert.Equal(35, item.CompletedSizeBytes);
    }

    [Fact]
    public async Task DownloadQueueStateStore_PersistsFailedPlaylistPositions()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var store = new DownloadQueueStateStore(statePath);
        var item = new DownloadQueueStateItem
        {
            SourceUrl = "https://open.qobuz.com/playlist/11932795",
            ContentId = "11932795",
            Type = DownloadContentType.Playlist,
            Title = "Road Trip",
            Artist = "MusicEnjoyer",
            Status = DownloadStatus.Issues,
            TotalTracks = 4,
            CompletedTracks = 4,
            WarningMessage = "2 playlist tracks failed. See Logs.",
            FailedPlaylistPositions = [2, 4]
        };

        await store.SaveAsync([item]);

        var restored = Assert.Single(await store.LoadAsync());
        Assert.Equal(DownloadStatus.Issues, restored.Status);
        Assert.Equal([2, 4], restored.FailedPlaylistPositions);
        Assert.Equal("2 playlist tracks failed. See Logs.", restored.WarningMessage);
    }

    [Fact]
    public void DownloadQueueItemStateMapper_RestoresActiveItemsAsPaused()
    {
        var item = DownloadQueueItemStateMapper.ToViewModel(new DownloadQueueStateItem
        {
            Status = DownloadStatus.Downloading,
            Title = "Interrupted Album",
            Type = DownloadContentType.Album
        });

        Assert.Equal(DownloadStatus.Paused, item.Status);
        Assert.Equal("Interrupted Album", item.Title);
    }

    [Fact]
    public async Task DownloadsViewModel_RestoresActiveQueueStateAsPaused()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var store = new DownloadQueueStateStore(statePath);
        await store.SaveAsync(
            [
                new DownloadQueueStateItem
                {
                    SourceUrl = "https://open.qobuz.com/album/abc123",
                    ContentId = "abc123",
                    Type = DownloadContentType.Album,
                    Title = "Interrupted Album",
                    Artist = "Example Artist",
                    Status = DownloadStatus.Downloading
                }
            ]);

        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: store,
            settings: new AppSettings());

        var item = Assert.Single(viewModel.QueueItems);
        Assert.Equal(DownloadStatus.Paused, item.Status);
        Assert.Equal(DownloadStatusText.Paused, viewModel.GlobalStatusText);
        Assert.Equal("Interrupted Album", item.Title);
    }

    [Fact]
    public async Task DownloadsViewModel_RestoresCompletedQueueStateWithoutTransientDownloadState()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var store = new DownloadQueueStateStore(statePath);
        await store.SaveAsync(
            [
                new DownloadQueueStateItem
                {
                    SourceUrl = "https://open.qobuz.com/track/123",
                    ContentId = "123",
                    Type = DownloadContentType.Track,
                    Title = "Finished Track",
                    Artist = "Example Artist",
                    Status = DownloadStatus.Completed,
                    CurrentTrackTitle = "01 - Finished Track",
                    CurrentTrackBytesReceived = 123,
                    CurrentTrackTotalBytes = 456,
                    FileProgressFraction = 0.25d
                }
            ]);

        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: store,
            settings: new AppSettings());

        var item = Assert.Single(viewModel.QueueItems);
        Assert.Equal(DownloadStatus.Completed, item.Status);
        Assert.Equal(string.Empty, item.CurrentTrackTitle);
        Assert.Equal(0, item.CurrentTrackBytesReceived);
        Assert.Null(item.CurrentTrackTotalBytes);
        Assert.Equal(1d, item.FileProgressFraction);
    }

    [Fact]
    public void DownloadsViewModel_PauseAllBeforeEnqueueKeepsNewItemsPaused()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var runner = new RecordingDownloadJobRunner();
        var scheduler = new CapturingDownloadWorkScheduler();
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: runner,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings(),
            downloadWorkScheduler: scheduler);

        viewModel.PauseAllCommand.Execute(null);
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/track/123",
            DownloadContentType.Track,
            "123"));

        Assert.True(viewModel.IsQueuePaused);
        Assert.Equal("Resume All", viewModel.PauseResumeButtonText);
        Assert.Equal(DownloadStatusText.Paused, viewModel.GlobalStatusText);
        Assert.Equal(DownloadStatus.Paused, item.Status);
        Assert.Equal(DownloadStatusText.UnresolvedMetadataPlaceholder, item.Artist);
        Assert.Empty(scheduler.ScheduledWork);
    }

    [Fact]
    public void DownloadsViewModel_RetryWhilePausedKeepsItemPaused()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/track/123",
            DownloadContentType.Track,
            "123"));
        item.Status = DownloadStatus.Failed;
        item.ErrorMessage = "Failed.";
        item.NotifyDisplayChanged();

        viewModel.PauseAllCommand.Execute(null);
        viewModel.RetrySelectedCommand.Execute(null);

        Assert.True(viewModel.IsQueuePaused);
        Assert.Equal(DownloadStatus.Paused, item.Status);
        Assert.Equal(DownloadStatusText.Paused, viewModel.GlobalStatusText);
    }

    [Fact]
    public void DownloadsViewModel_RetryFromPlaylistIssuesPreservesFailedPlaylistPositions()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/playlist/11932795",
            DownloadContentType.Playlist,
            "11932795"));
        item.Status = DownloadStatus.Issues;
        item.CompletedTracks = 4;
        item.TotalTracks = 4;
        item.WarningMessage = "2 playlist tracks failed. See Logs.";
        item.FailedPlaylistPositions = [2, 4];
        item.NotifyDisplayChanged();

        viewModel.RetrySelectedCommand.Execute(null);

        Assert.Equal(DownloadStatus.Queued, item.Status);
        Assert.Equal([2, 4], item.FailedPlaylistPositions);
        Assert.Equal(string.Empty, item.WarningMessage);
    }

    [Fact]
    public void DownloadsViewModel_RetryFromNonPlaylistFailureClearsFailedPlaylistPositions()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/album/abc123",
            DownloadContentType.Album,
            "abc123"));
        item.Status = DownloadStatus.Failed;
        item.ErrorMessage = "Failed.";
        item.FailedPlaylistPositions = [2, 4];
        item.NotifyDisplayChanged();

        viewModel.RetrySelectedCommand.Execute(null);

        Assert.Equal(DownloadStatus.Queued, item.Status);
        Assert.Empty(item.FailedPlaylistPositions);
        Assert.Equal(string.Empty, item.ErrorMessage);
    }

    [Fact]
    public void DownloadsViewModel_DoesNotRetryQualityFallbackOnlyIssues()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/album/abc123",
            DownloadContentType.Album,
            "abc123"));
        item.Status = DownloadStatus.Issues;
        item.CompletedTracks = 10;
        item.TotalTracks = 10;
        item.WarningMessage = "Example Track quality was reduced to FLAC 16/44.1 after the requested FLAC stream failed.";
        item.NotifyDisplayChanged();

        Assert.False(viewModel.IsSelectedItemRetryable);

        viewModel.RetrySelectedCommand.Execute(null);

        Assert.Equal(DownloadStatus.Issues, item.Status);
        Assert.Equal("Example Track quality was reduced to FLAC 16/44.1 after the requested FLAC stream failed.", item.WarningMessage);
    }

    [Fact]
    public async Task DownloadsViewModel_ClearCompletedMovesSelectionOffRemovedItem()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: null,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());
        var completedItem = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/track/123",
            DownloadContentType.Track,
            "123"));
        completedItem.Status = DownloadStatus.Completed;
        var queuedItem = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://open.qobuz.com/track/456",
            DownloadContentType.Track,
            "456"));
        viewModel.SelectedItem = completedItem;

        await viewModel.ClearCompletedCommand.ExecuteAsync(null);

        Assert.DoesNotContain(completedItem, viewModel.QueueItems);
        Assert.Same(queuedItem, viewModel.SelectedItem);
    }

    [Fact]
    public void DownloadsViewModel_SetDownloadJobRunner_DisposesPreviousRunner()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var firstRunner = new DisposableDownloadJobRunner();
        var secondRunner = new DisposableDownloadJobRunner();
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: firstRunner,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());

        viewModel.SetDownloadJobRunner(secondRunner);

        Assert.True(firstRunner.Disposed);
        Assert.False(secondRunner.Disposed);
    }

    [Fact]
    public async Task DownloadsViewModel_PrepareForShutdownAsync_DisposesCurrentRunner()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var runner = new DisposableDownloadJobRunner();
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: runner,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings());

        await viewModel.PrepareForShutdownAsync();

        Assert.True(runner.Disposed);
    }

    [Fact]
    public async Task DownloadsViewModel_StartsRunnerThroughDownloadWorkScheduler()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var runner = new RecordingDownloadJobRunner();
        var scheduler = new CapturingDownloadWorkScheduler();
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: runner,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings(),
            downloadWorkScheduler: scheduler);

        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://play.qobuz.com/track/123",
            DownloadContentType.Track,
            "123"));

        Assert.Single(scheduler.ScheduledWork);
        Assert.False(runner.RunStarted);
        Assert.Equal(DownloadStatus.Resolving, item.Status);

        await scheduler.RunScheduledWorkAsync(0);
        await WaitUntil(() => item.Status == DownloadStatus.Completed);

        Assert.True(runner.RunStarted);
        Assert.Equal("Resolved Track", item.Title);
    }

    [Fact]
    public async Task DownloadsViewModel_PauseAfterCurrentTrackStopsRunnerBeforeNextTrackStarts()
    {
        using var workspace = CreateQueueStateWorkspace(out var statePath);
        var runner = new PauseAfterCurrentTrackRunner();
        var scheduler = new CapturingDownloadWorkScheduler();
        var viewModel = new DownloadsViewModel(
            downloadJobRunner: runner,
            queueStateStore: new DownloadQueueStateStore(statePath),
            settings: new AppSettings(),
            downloadWorkScheduler: scheduler);

        var item = viewModel.EnqueueDownloadRequest(new DownloadRequest(
            "https://play.qobuz.com/album/123",
            DownloadContentType.Album,
            "123"));
        var workTask = Task.Run(() => scheduler.RunScheduledWorkAsync(0));

        await WaitUntil(() => item.CurrentTrackTitle.Contains("First", StringComparison.Ordinal));

        viewModel.PauseAllCommand.Execute(null);
        runner.AllowFirstTrackToComplete();

        await WaitUntil(() => item.Status == DownloadStatus.Paused);
        await workTask;

        Assert.False(runner.AdvancedToSecondTrack);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationTokenSource.Token);
        }
    }

    private static TestWorkspace CreateQueueStateWorkspace(out string statePath)
    {
        var workspace = TestPaths.CreateWorkspace();
        statePath = workspace.FilePath("queue.json");
        return workspace;
    }

    private static void ApplyDownloadEvent(
        DownloadsViewModel viewModel,
        DownloadQueueItemViewModel item,
        DownloadEvent downloadEvent)
    {
        var method = typeof(DownloadsViewModel).GetMethod(
            "ApplyDownloadEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [item, downloadEvent]);
    }

    private sealed class ScriptedDownloadJobRunner : IDownloadJobRunner
    {
        private readonly Func<string, IReadOnlyList<DownloadEvent>> createEvents;

        public ScriptedDownloadJobRunner(Func<string, IReadOnlyList<DownloadEvent>> createEvents)
        {
            this.createEvents = createEvents;
        }

        public async IAsyncEnumerable<DownloadEvent> RunAsync(
            DownloadQueueItem item,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var downloadEvent in createEvents(item.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return downloadEvent;
            }
        }
    }

    private sealed class DisposableDownloadJobRunner : IDownloadJobRunner, IDisposable
    {
        public bool Disposed { get; private set; }

        public async IAsyncEnumerable<DownloadEvent> RunAsync(
            DownloadQueueItem item,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield break;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class RecordingDownloadJobRunner : IDownloadJobRunner
    {
        public bool RunStarted { get; private set; }

        public async IAsyncEnumerable<DownloadEvent> RunAsync(
            DownloadQueueItem item,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            RunStarted = true;
            await Task.Yield();
            yield return new DownloadResolvedEvent(
                item.Id,
                DownloadContentType.Track,
                "Resolved Track",
                "Resolved Artist",
                "FLAC 16/44.1",
                TotalTracks: 1,
                CoverArtUrl: string.Empty,
                ReleaseDate: string.Empty,
                Upc: string.Empty,
                DestinationPath: TestPaths.TestingRoot);
            yield return new DownloadCompletedEvent(item.Id, HasWarnings: false);
        }
    }

    private sealed class PauseAfterCurrentTrackRunner : IDownloadJobRunner
    {
        private readonly TaskCompletionSource firstTrackMayComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool AdvancedToSecondTrack { get; private set; }

        public void AllowFirstTrackToComplete()
        {
            firstTrackMayComplete.TrySetResult();
        }

        public async IAsyncEnumerable<DownloadEvent> RunAsync(
            DownloadQueueItem item,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new DownloadResolvedEvent(
                item.Id,
                DownloadContentType.Album,
                "Resolved Album",
                "Resolved Artist",
                "FLAC 16/44.1",
                TotalTracks: 2,
                CoverArtUrl: string.Empty,
                ReleaseDate: string.Empty,
                Upc: string.Empty,
                DestinationPath: TestPaths.TestingRoot);
            yield return new TrackStartedEvent(item.Id, TrackNumber: 1, TotalTracks: 2, TrackTitle: "First");

            await firstTrackMayComplete.Task.WaitAsync(cancellationToken);

            yield return new TrackCompletedEvent(item.Id, CompletedTracks: 1, TotalTracks: 2, FilePath: "first.flac");

            AdvancedToSecondTrack = true;
            yield return new TrackStartedEvent(item.Id, TrackNumber: 2, TotalTracks: 2, TrackTitle: "Second");
        }
    }

    private sealed class CapturingDownloadWorkScheduler : IDownloadWorkScheduler
    {
        public List<Func<Task>> ScheduledWork { get; } = [];

        public Task RunAsync(Func<Task> work)
        {
            ScheduledWork.Add(work);
            return Task.CompletedTask;
        }

        public Task RunScheduledWorkAsync(int index)
        {
            return ScheduledWork[index]();
        }
    }
}
