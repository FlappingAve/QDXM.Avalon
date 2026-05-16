using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Exceptions;
using QDXM.Avalon.Services;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.ViewModels;

public partial class DownloadsViewModel : ViewModelBase
{
    private IDownloadJobRunner? downloadJobRunner;
    private readonly AppLogService logService;
    private readonly DownloadQueueStateStore queueStateStore;
    private readonly AppSettings settings;
    private readonly IDownloadWorkScheduler downloadWorkScheduler;
    private CancellationTokenSource? activeCancellationTokenSource;
    private readonly List<IDisposable> deferredRunnerDisposals = [];
    private bool isProcessingQueue;
    private bool queuePaused;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadUrlCommand))]
    private string urlText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDestinationPreview))]
    private DownloadQueueItemViewModel? selectedItem;

    [ObservableProperty]
    private string globalStatusText = DownloadStatusText.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSpeedText))]
    private string? currentSpeedTextOverride;

    public DownloadsViewModel()
        : this(null, new AppLogService())
    {
    }

    public DownloadsViewModel(
        IDownloadJobRunner? downloadJobRunner,
        AppLogService? logService = null,
        DownloadQueueStateStore? queueStateStore = null,
        AppSettings? settings = null,
        IDownloadWorkScheduler? downloadWorkScheduler = null)
    {
        this.downloadJobRunner = downloadJobRunner;
        this.logService = logService ?? new AppLogService();
        this.queueStateStore = queueStateStore ?? new DownloadQueueStateStore();
        this.settings = settings ?? new AppSettings();
        this.downloadWorkScheduler = downloadWorkScheduler ?? new ThreadPoolDownloadWorkScheduler();
        QueueItems = [];

        RestoreQueueState();
    }

    public ObservableCollection<DownloadQueueItemViewModel> QueueItems { get; private set; }
    public string SelectedDestinationPreview => SelectedItem is null
        ? string.Empty
        : DestinationPreviewRenderer.ForDownloadItem(SelectedItem, settings);
    public Func<DownloadQueueItemViewModel, Task<bool>>? ConfirmRemovalAsync { get; set; }
    public Func<int, Task<bool>>? ConfirmClearCompletedAsync { get; set; }

    public double OverallProgressFraction
    {
        get
        {
            var activeBatch = QueueItems.Where(IsCurrentProgressItem).ToList();
            if (activeBatch.Count > 0)
            {
                return activeBatch.Average(item => item.ProgressBarValue);
            }

            var terminalItems = QueueItems.Where(IsTerminalItem).ToList();
            if (terminalItems.Count == 0)
            {
                return 0;
            }

            return terminalItems.Average(item => item.Status == DownloadStatus.Completed ? 1d : item.ProgressBarValue);
        }
    }

    public string OverallProgressPercentText => $"{OverallProgressFraction:P0}";
    public string CurrentSpeedText => CurrentSpeedTextOverride ?? "--";
    public string EtaText => string.Empty;
    public string ItemCountText => $"{QueueItems.Count} items";
    public string ActiveDownloadCountText => $"{QueueItems.Count(item => item.Status == DownloadStatus.Downloading)} active downloads";
    public string PauseResumeButtonText => queuePaused ? "Resume All" : "Pause All";
    public bool IsQueuePaused => queuePaused;
    public bool IsQueueRunning => !queuePaused;
    public bool IsSelectedItemRetryable => IsRetryable(SelectedItem);
    public bool HasSelectedItemError => SelectedItem?.HasError == true;
    public bool HasSelectedItemWarning => SelectedItem?.HasWarning == true;

    partial void OnSelectedItemChanged(DownloadQueueItemViewModel? oldValue, DownloadQueueItemViewModel? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue))
        {
            oldValue?.UnloadCoverArt();
            newValue?.LoadSelectedCoverArt();
        }

        OnPropertyChanged(nameof(IsSelectedItemRetryable));
        OnPropertyChanged(nameof(HasSelectedItemError));
        OnPropertyChanged(nameof(HasSelectedItemWarning));
    }

    private void NotifyPauseResumeChanged()
    {
        OnPropertyChanged(nameof(PauseResumeButtonText));
        OnPropertyChanged(nameof(IsQueuePaused));
        OnPropertyChanged(nameof(IsQueueRunning));
    }

    private bool CanDownloadUrl() => !string.IsNullOrWhiteSpace(UrlText);

    [RelayCommand(CanExecute = nameof(CanDownloadUrl))]
    private void DownloadUrl()
    {
        var queued = TryEnqueueUrl(UrlText);
        if (queued)
        {
            UrlText = string.Empty;
        }
    }

    public bool TryEnqueueUrl(string? url)
    {
        if (!DownloadUrlParser.TryParseDownloadUrl(url, out var request) || request is null)
        {
            GlobalStatusText = "Invalid Qobuz URL";
            logService.Warning("Downloads", "Invalid Qobuz URL entered.");
            return false;
        }

        if (!DownloadRequestSupport.IsSupportedNow(request.ContentType))
        {
            var message = DownloadRequestSupport.GetUnsupportedMessage(request.ContentType);
            GlobalStatusText = message;
            logService.Warning("Downloads", message);
            return false;
        }

        EnqueueDownloadRequest(request);
        GlobalStatusText = GetQueueWaitingStatusText();
        return true;
    }

    public DownloadQueueItemViewModel EnqueueDownloadRequest(
        DownloadRequest request,
        string? title = null,
        string? artist = null,
        string? quality = null,
        int totalTracks = 0,
        string? coverArtUrl = null,
        string? releaseDate = null,
        string? upc = null)
    {
        var item = new DownloadQueueItemViewModel
        {
            SourceUrl = request.SourceUrl,
            ContentId = request.ContentId,
            Type = request.ContentType,
            Title = string.IsNullOrWhiteSpace(title) ? $"{request.ContentType} {request.ContentId}" : title,
            Artist = string.IsNullOrWhiteSpace(artist) ? DownloadStatusText.UnresolvedMetadataPlaceholder : artist,
            Quality = quality ?? string.Empty,
            TotalTracks = totalTracks > 0 ? totalTracks : request.ContentType == DownloadContentType.Track ? 1 : 0,
            CoverArtUrl = coverArtUrl ?? string.Empty,
            ReleaseDate = releaseDate ?? string.Empty,
            Upc = upc ?? string.Empty,
            Status = GetNewQueueItemStatus()
        };

        QueueItems.Add(item);
        SelectedItem = item;
        NotifyQueueTotalsChanged();
        SaveQueueState();
        StartQueueProcessingIfReady();

        return item;
    }

    public DownloadQueueItemViewModel EnqueuePartialAlbumRequest(PartialAlbumDownloadRequest request)
    {
        var item = new DownloadQueueItemViewModel
        {
            SourceUrl = request.AlbumUrl,
            ContentId = request.AlbumId,
            Type = DownloadContentType.Album,
            Title = request.DisplayTitle ?? $"Album {request.AlbumId}",
            Artist = request.DisplayArtist ?? DownloadStatusText.UnresolvedMetadataPlaceholder,
            TotalTracks = request.TrackIds.Count,
            Status = GetNewQueueItemStatus(),
            CurrentTrackTitle = $"{request.TrackIds.Count} selected tracks",
            SelectedTrackIds = request.TrackIds
        };

        QueueItems.Add(item);
        SelectedItem = item;
        GlobalStatusText = queuePaused ? DownloadStatusText.Paused : "Selected album tracks queued";
        NotifyQueueTotalsChanged();
        SaveQueueState();
        StartQueueProcessingIfReady();

        return item;
    }

    public DownloadQueueItemViewModel EnqueuePartialPlaylistRequest(PartialPlaylistDownloadRequest request)
    {
        var item = new DownloadQueueItemViewModel
        {
            SourceUrl = request.PlaylistUrl,
            ContentId = request.PlaylistId,
            Type = DownloadContentType.Playlist,
            Title = request.DisplayTitle ?? $"Playlist {request.PlaylistId}",
            Artist = request.DisplayOwner ?? DownloadStatusText.UnresolvedMetadataPlaceholder,
            Quality = QualityStringMappings.GetQualityLabelFromFormatId(settings.FormatId),
            TotalTracks = request.TrackSelectionKeys.Count,
            Status = GetNewQueueItemStatus(),
            CurrentTrackTitle = $"{request.TrackSelectionKeys.Count} selected tracks",
            CoverArtUrl = request.CoverArtUrl ?? string.Empty,
            SelectedTrackIds = request.TrackSelectionKeys
        };

        QueueItems.Add(item);
        SelectedItem = item;
        GlobalStatusText = queuePaused ? DownloadStatusText.Paused : "Selected playlist tracks queued";
        NotifyQueueTotalsChanged();
        SaveQueueState();
        StartQueueProcessingIfReady();

        return item;
    }

    private DownloadStatus GetNewQueueItemStatus()
    {
        return queuePaused ? DownloadStatus.Paused : DownloadStatus.Queued;
    }

    private string GetQueueWaitingStatusText()
    {
        return queuePaused ? DownloadStatusText.Paused : DownloadStatusText.Queued;
    }

    public void SetDownloadJobRunner(IDownloadJobRunner? runner)
    {
        if (ReferenceEquals(downloadJobRunner, runner))
        {
            return;
        }

        var previousRunner = downloadJobRunner;
        downloadJobRunner = runner;
        DisposeDownloadJobRunner(previousRunner);
    }

    [RelayCommand]
    private void PauseAll()
    {
        if (queuePaused)
        {
            ResumeAll();
            return;
        }

        PauseQueue();
    }

    private void PauseQueue()
    {
        queuePaused = true;

        foreach (var item in QueueItems.Where(item => item.Status == DownloadStatus.Queued))
        {
            item.Status = DownloadStatus.Paused;
            item.NotifyDisplayChanged();
        }

        GlobalStatusText = QueueItems.Any(item => item.Status is DownloadStatus.Resolving or DownloadStatus.Downloading)
            ? DownloadStatusText.PausingAfterCurrentTrack
            : DownloadStatusText.Paused;
        NotifyQueueTotalsChanged();
        NotifyPauseResumeChanged();
        SaveQueueState();
    }

    private void ResumeAll()
    {
        queuePaused = false;

        foreach (var item in QueueItems.Where(item => item.Status == DownloadStatus.Paused))
        {
            item.Status = DownloadStatus.Queued;
            item.NotifyDisplayChanged();
        }

        GlobalStatusText = DownloadStatusText.Queued;
        NotifyQueueTotalsChanged();
        NotifyPauseResumeChanged();
        SaveQueueState();
        StartQueueProcessingIfReady();
    }

    [RelayCommand]
    private async Task ClearCompleted()
    {
        var completedItems = QueueItems
            .Where(item => item.Status == DownloadStatus.Completed)
            .ToList();
        if (completedItems.Count == 0)
        {
            return;
        }

        if (ConfirmClearCompletedAsync is not null &&
            !await ConfirmClearCompletedAsync(completedItems.Count))
        {
            GlobalStatusText = "Clear completed canceled";
            return;
        }

        foreach (var item in completedItems)
        {
            item.DeleteCoverArtCache();
            QueueItems.Remove(item);
        }

        var selectedItem = SelectedItem;
        if (selectedItem is not null && completedItems.Contains(selectedItem))
        {
            SelectedItem = QueueItems.FirstOrDefault();
        }

        GlobalStatusText = "Completed items cleared";
        NotifyQueueTotalsChanged();
        SaveQueueState();
    }

    [RelayCommand]
    private async Task RemoveSelected()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var removedItem = SelectedItem;
        var index = QueueItems.IndexOf(removedItem);
        if (ConfirmRemovalAsync is not null &&
            !await ConfirmRemovalAsync(removedItem))
        {
            GlobalStatusText = "Remove canceled";
            return;
        }

        if (removedItem.Status is DownloadStatus.Resolving or DownloadStatus.Downloading)
        {
            activeCancellationTokenSource?.Cancel();
        }

        QueueItems.Remove(removedItem);
        removedItem.DeleteCoverArtCache();
        SelectedItem = QueueItems.Count == 0 ? null : QueueItems[Math.Clamp(index, 0, QueueItems.Count - 1)];
        ClearPauseStateIfQueueEmpty();
        NotifyQueueTotalsChanged();
        SaveQueueState();
        StartQueueProcessingIfReady();
    }

    [RelayCommand]
    private void RetrySelected()
    {
        var selectedItem = SelectedItem;
        if (selectedItem is null || !IsRetryable(selectedItem))
        {
            return;
        }

        var retryingPlaylistIssues = selectedItem.Status == DownloadStatus.Issues &&
            selectedItem.Type == DownloadContentType.Playlist &&
            selectedItem.FailedPlaylistPositions.Count > 0;
        var retryingFavoriteIssues = selectedItem.Status == DownloadStatus.Issues &&
            selectedItem.Type == DownloadContentType.Favorites;

        selectedItem.Status = GetNewQueueItemStatus();
        selectedItem.ErrorMessage = string.Empty;
        selectedItem.WarningMessage = string.Empty;
        if (retryingFavoriteIssues)
        {
            selectedItem.CompletedTracks = 0;
            selectedItem.CompletedSizeBytes = 0;
            selectedItem.SizeBytes = null;
        }

        if (!retryingPlaylistIssues)
        {
            selectedItem.FailedPlaylistPositions = [];
        }

        selectedItem.FileProgressFraction = null;
        selectedItem.CurrentTrackBytesReceived = 0;
        selectedItem.CurrentTrackTotalBytes = null;
        selectedItem.NotifyDisplayChanged();
        GlobalStatusText = GetQueueWaitingStatusText();
        OnPropertyChanged(nameof(IsSelectedItemRetryable));
        OnPropertyChanged(nameof(HasSelectedItemError));
        OnPropertyChanged(nameof(HasSelectedItemWarning));
        NotifyQueueTotalsChanged();
        SaveQueueState();
        StartQueueProcessingIfReady();
    }

    [RelayCommand]
    private void OpenSelectedFolder()
    {
        var targetFolder = !string.IsNullOrWhiteSpace(SelectedItem?.DestinationPath)
            ? SelectedItem.DestinationPath
            : settings.EffectiveDownloadFolder;
        var folder = ResolveExistingFolder(targetFolder);
        if (folder is null)
        {
            GlobalStatusText = "Folder not found";
            logService.Warning("Downloads", "Open folder failed because no existing destination or download folder was available.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenSelectedQobuz()
    {
        if (SelectedItem is null ||
            string.IsNullOrWhiteSpace(SelectedItem.SourceUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedItem.SourceUrl,
            UseShellExecute = true
        });
    }

    private void NotifyQueueTotalsChanged()
    {
        OnPropertyChanged(nameof(OverallProgressFraction));
        OnPropertyChanged(nameof(OverallProgressPercentText));
        OnPropertyChanged(nameof(ItemCountText));
        OnPropertyChanged(nameof(ActiveDownloadCountText));
    }

    private void ClearPauseStateIfQueueEmpty()
    {
        if (!queuePaused || QueueItems.Count > 0)
        {
            return;
        }

        queuePaused = false;
        GlobalStatusText = DownloadStatusText.Idle;
        NotifyPauseResumeChanged();
    }

    private static bool IsCurrentProgressItem(DownloadQueueItemViewModel item)
    {
        return item.Status is
            DownloadStatus.Queued or
            DownloadStatus.Resolving or
            DownloadStatus.Downloading or
            DownloadStatus.Paused;
    }

    private static bool IsTerminalItem(DownloadQueueItemViewModel item)
    {
        return item.Status is
            DownloadStatus.Completed or
            DownloadStatus.Issues or
            DownloadStatus.Failed or
            DownloadStatus.Canceled or
            DownloadStatus.Skipped;
    }

    private static bool IsAggregateSizeItem(DownloadQueueItemViewModel item)
    {
        return item.Type is DownloadContentType.Album or DownloadContentType.Playlist or DownloadContentType.Favorites;
    }

    private static bool IsRetryable(DownloadQueueItemViewModel? item)
    {
        return item?.Status == DownloadStatus.Failed ||
            (item?.Status == DownloadStatus.Issues && !HasOnlyQualityFallbackWarning(item));
    }

    private static bool IsCompletedWithoutRetry(DownloadQueueItemViewModel item)
    {
        return item.Status == DownloadStatus.Completed ||
            item.Status == DownloadStatus.Issues && !IsRetryable(item);
    }

    private static bool HasOnlyQualityFallbackWarning(DownloadQueueItemViewModel? item)
    {
        if (item is null ||
            item.HasError ||
            string.IsNullOrWhiteSpace(item.WarningMessage) ||
            item.FailedPlaylistPositions.Count > 0)
        {
            return false;
        }

        return item.WarningMessage.Contains("quality was reduced", StringComparison.OrdinalIgnoreCase) ||
            item.WarningMessage.Contains("fell back to", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveExistingFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var current = folder;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private void StartQueueProcessingIfReady()
    {
        if (downloadJobRunner is null || queuePaused || isProcessingQueue)
        {
            return;
        }

        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (downloadJobRunner is null || isProcessingQueue)
        {
            return;
        }

        isProcessingQueue = true;

        try
        {
            while (!queuePaused)
            {
                var item = QueueItems.FirstOrDefault(item => item.Status == DownloadStatus.Queued);
                if (item is null)
                {
                    GlobalStatusText = DownloadStatusText.Idle;
                    break;
                }

                activeCancellationTokenSource = new CancellationTokenSource();
                item.Status = DownloadStatus.Resolving;
                item.NotifyDisplayChanged();
                GlobalStatusText = DownloadStatusText.ResolvingProgress;
                NotifyQueueTotalsChanged();

                try
                {
                    await foreach (var downloadEvent in RunDownloadWorkAsync(
                        downloadJobRunner,
                        item.ToCoreItem(),
                        activeCancellationTokenSource.Token))
                    {
                        var shouldContinueRunner = false;
                        var shouldStopConsuming = false;
                        try
                        {
                            ApplyDownloadEvent(item, downloadEvent.Event);
                            shouldContinueRunner = !ShouldPauseAfterCompletedTrack(downloadEvent.Event);
                            if (!shouldContinueRunner)
                            {
                                item.Status = DownloadStatus.Paused;
                                item.NotifyDisplayChanged();
                                GlobalStatusText = DownloadStatusText.Paused;
                                SaveQueueState();
                                shouldStopConsuming = true;
                            }
                        }
                        finally
                        {
                            downloadEvent.Acknowledge(shouldContinueRunner);
                        }

                        if (shouldStopConsuming)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (QueueItems.Contains(item))
                    {
                        item.Status = queuePaused ? DownloadStatus.Paused : DownloadStatus.Canceled;
                        item.NotifyDisplayChanged();
                        GlobalStatusText = queuePaused ? DownloadStatusText.Paused : DownloadStatusText.Canceled;
                        SaveQueueState();
                    }
                }
                catch (ApiErrorResponseException ex)
                {
                    var message = SafeErrorText.FormatDownloadApiFailure(ex);
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage = message;
                    item.NotifyDisplayChanged();
                    NotifySelectedItemContextChanged(item);
                    GlobalStatusText = DownloadStatusText.Failed;
                    logService.Error("Downloads", $"{item.Title}: {message}");
                }
                catch (Exception ex)
                {
                    var message = SafeErrorText.FormatUnexpectedFailure("Download");
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage = message;
                    item.NotifyDisplayChanged();
                    NotifySelectedItemContextChanged(item);
                    GlobalStatusText = DownloadStatusText.Failed;
                    logService.Error("Downloads", $"{item.Title}: {SafeErrorText.FormatUnexpectedLogMessage(ex)}");
                }
            }
        }
        finally
        {
            activeCancellationTokenSource?.Dispose();
            activeCancellationTokenSource = null;
            isProcessingQueue = false;
            DisposeDeferredDownloadJobRunners();
            NotifyQueueTotalsChanged();
        }
    }

    private async IAsyncEnumerable<AcknowledgedDownloadEvent> RunDownloadWorkAsync(
        IDownloadJobRunner runner,
        DownloadQueueItem item,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var producerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var events = Channel.CreateUnbounded<AcknowledgedDownloadEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        Task producerTask;
        try
        {
            producerTask = downloadWorkScheduler.RunAsync(async () =>
            {
                try
                {
                    await foreach (var downloadEvent in runner.RunAsync(item, producerCancellation.Token).ConfigureAwait(false))
                    {
                        var acknowledgedEvent = new AcknowledgedDownloadEvent(downloadEvent);
                        await events.Writer.WriteAsync(acknowledgedEvent, producerCancellation.Token).ConfigureAwait(false);
                        if (!await acknowledgedEvent.WaitForAcknowledgementAsync(producerCancellation.Token).ConfigureAwait(false))
                        {
                            break;
                        }
                    }

                    events.Writer.TryComplete();
                }
                catch (Exception ex)
                {
                    events.Writer.TryComplete(ex);
                }
            });
        }
        catch (Exception ex)
        {
            events.Writer.TryComplete(ex);
            producerTask = Task.CompletedTask;
        }

        try
        {
            await foreach (var downloadEvent in events.Reader.ReadAllAsync(cancellationToken))
            {
                yield return downloadEvent;
            }
        }
        finally
        {
            await producerCancellation.CancelAsync();
            try
            {
                await producerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (producerCancellation.IsCancellationRequested)
            {
            }
        }
    }

    private sealed class AcknowledgedDownloadEvent
    {
        private readonly TaskCompletionSource<bool> acknowledgement = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AcknowledgedDownloadEvent(DownloadEvent downloadEvent)
        {
            Event = downloadEvent;
        }

        public DownloadEvent Event { get; }

        public void Acknowledge(bool shouldContinueRunner)
        {
            acknowledgement.TrySetResult(shouldContinueRunner);
        }

        public async Task<bool> WaitForAcknowledgementAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(
                static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
                acknowledgement);
            return await acknowledgement.Task.ConfigureAwait(false);
        }
    }

    private void ApplyDownloadEvent(DownloadQueueItemViewModel item, DownloadEvent downloadEvent)
    {
        switch (downloadEvent)
        {
            case DownloadResolvedEvent resolved:
                var preservePlaylistFailures = item.Type == DownloadContentType.Playlist &&
                    item.FailedPlaylistPositions.Count > 0 &&
                    item.CompletedTracks < resolved.TotalTracks;
                var preserveAggregateSize = IsAggregateSizeItem(item) &&
                    item.CompletedTracks > 0 &&
                    item.CompletedTracks < resolved.TotalTracks &&
                    resolved.TotalSizeBytes is null;
                var completedSizeBytes = preserveAggregateSize ? item.CompletedSizeBytes : 0;
                item.Type = resolved.Type;
                item.Title = resolved.Title;
                item.Artist = resolved.Artist;
                item.Quality = resolved.Quality;
                item.TotalTracks = resolved.TotalTracks;
                item.CoverArtUrl = resolved.CoverArtUrl;
                item.ReleaseDate = resolved.ReleaseDate;
                item.Upc = resolved.Upc;
                item.DestinationPath = resolved.DestinationPath;
                item.DestinationFilePaths = resolved.FilePaths?.Where(path => !string.IsNullOrWhiteSpace(path)).ToList() ?? [];
                item.DestinationPreviewRemainingCount = Math.Max(0, resolved.DestinationPreviewRemainingCount);
                item.SizeBytes = resolved.TotalSizeBytes;
                item.CompletedSizeBytes = completedSizeBytes;
                item.CurrentTrackBytesReceived = 0;
                item.CurrentTrackTotalBytes = null;
                item.CurrentTrackTitle = string.Empty;
                item.Status = DownloadStatus.Downloading;
                item.ErrorMessage = string.Empty;
                item.WarningMessage = preservePlaylistFailures
                    ? $"{item.FailedPlaylistPositions.Count} playlist tracks failed. See Logs."
                    : string.Empty;
                if (!preservePlaylistFailures)
                {
                    item.FailedPlaylistPositions = [];
                }

                GlobalStatusText = DownloadStatusText.DownloadingProgress;
                break;
            case TrackStartedEvent started:
                item.CurrentTrackTitle = $"{started.TrackNumber:00} - {started.TrackTitle}";
                item.FileProgressFraction = null;
                item.CurrentTrackBytesReceived = 0;
                item.CurrentTrackTotalBytes = null;
                item.Status = DownloadStatus.Downloading;
                GlobalStatusText = DownloadStatusText.DownloadingProgress;
                break;
            case FileProgressEvent progress:
                item.FileProgressFraction = progress.TotalBytes is > 0
                    ? (double)progress.BytesReceived / progress.TotalBytes.Value
                    : null;
                if (IsAggregateSizeItem(item))
                {
                    item.CurrentTrackBytesReceived = progress.BytesReceived;
                    item.CurrentTrackTotalBytes = progress.TotalBytes;
                }
                else
                {
                    item.SizeBytes = progress.TotalBytes ?? progress.BytesReceived;
                }

                CurrentSpeedTextOverride = $"{progress.MegabytesPerSecond:0.0} MB/s";
                break;
            case TrackCompletedEvent completed:
                item.CompletedTracks = completed.CompletedTracks;
                if (item.Type is not (DownloadContentType.Playlist or DownloadContentType.Favorites) &&
                    !string.IsNullOrWhiteSpace(completed.FilePath) &&
                    !item.DestinationFilePaths.Contains(completed.FilePath, StringComparer.OrdinalIgnoreCase))
                {
                    item.DestinationFilePaths = [.. item.DestinationFilePaths, completed.FilePath];
                }

                if (IsAggregateSizeItem(item) && item.SizeBytes is null)
                {
                    item.CompletedSizeBytes += completed.FileSizeBytes ?? item.CurrentTrackBytesReceived;
                    item.CurrentTrackBytesReceived = 0;
                    item.CurrentTrackTotalBytes = null;
                }

                break;
            case DownloadCompletedEvent completed:
                item.CompletedTracks = item.TotalTracks <= 0 ? item.CompletedTracks : item.TotalTracks;
                item.FileProgressFraction = 1d;
                item.Status = completed.HasWarnings
                    ? DownloadStatus.Issues
                    : DownloadStatus.Completed;
                item.CurrentTrackTitle = string.Empty;
                if (completed.HasWarnings && string.IsNullOrWhiteSpace(item.WarningMessage))
                {
                    item.WarningMessage = DownloadStatusText.CompletedWithWarningsDetail;
                }

                GlobalStatusText = completed.HasWarnings ? DownloadStatusText.CompletedWithWarnings : DownloadStatusText.Completed;
                if (completed.HasWarnings)
                {
                    logService.Warning("Downloads", $"{item.Title} completed with warnings.");
                }
                break;
            case DownloadFailedEvent failed:
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage = failed.Message;
                GlobalStatusText = DownloadStatusText.Failed;
                logService.Error("Downloads", $"{item.Title}: {failed.Message}");
                break;
            case DownloadWarningEvent warning:
                item.WarningMessage = warning.Message;
                GlobalStatusText = DownloadStatusText.CompletedWithWarnings;
                logService.Warning("Downloads", $"{item.Title}: {warning.Message}");
                break;
            case DownloadInfoEvent info:
                logService.Info("Downloads", $"{item.Title}: {info.Message}");
                break;
            case PlaylistTrackFailedEvent playlistTrackFailed:
                if (!item.FailedPlaylistPositions.Contains(playlistTrackFailed.PlaylistPosition))
                {
                    item.FailedPlaylistPositions = [.. item.FailedPlaylistPositions, playlistTrackFailed.PlaylistPosition];
                }

                item.WarningMessage = $"{item.FailedPlaylistPositions.Count} playlist tracks failed. See Logs.";
                GlobalStatusText = DownloadStatusText.CompletedWithWarnings;
                logService.Warning("Downloads", $"{item.Title}: {playlistTrackFailed.Message}");
                break;
        }

        if (IsCompletedWithoutRetry(item))
        {
            item.ReleaseFinishedJobTransientState();
        }

        item.NotifyDisplayChanged();
        NotifySelectedItemContextChanged(item);
        NotifyQueueTotalsChanged();
        SaveQueueStateForDownloadEvent(downloadEvent);
    }

    private void SaveQueueStateForDownloadEvent(DownloadEvent downloadEvent)
    {
        switch (downloadEvent)
        {
            case FileProgressEvent:
            case TrackStartedEvent:
                break;
            default:
                SaveQueueState();
                break;
        }
    }

    private void NotifySelectedItemContextChanged(DownloadQueueItemViewModel item)
    {
        if (ReferenceEquals(item, SelectedItem))
        {
            item.LoadSelectedCoverArt();
            OnPropertyChanged(nameof(IsSelectedItemRetryable));
            OnPropertyChanged(nameof(HasSelectedItemError));
            OnPropertyChanged(nameof(HasSelectedItemWarning));
            OnPropertyChanged(nameof(SelectedDestinationPreview));
        }
    }

    private bool ShouldPauseAfterCompletedTrack(DownloadEvent downloadEvent)
    {
        return queuePaused &&
            downloadEvent is TrackCompletedEvent completed &&
            completed.CompletedTracks < completed.TotalTracks;
    }

    public async Task PrepareForShutdownAsync()
    {
        activeCancellationTokenSource?.Cancel();

        foreach (var item in QueueItems.Where(item => item.Status is DownloadStatus.Resolving or DownloadStatus.Downloading))
        {
            item.Status = DownloadStatus.Paused;
            item.NotifyDisplayChanged();
        }

        await SaveQueueStateAsync().ConfigureAwait(false);
        DisposeDownloadJobRunner(downloadJobRunner);
        downloadJobRunner = null;
        if (!isProcessingQueue)
        {
            DisposeDeferredDownloadJobRunners();
        }
    }

    private void DisposeDownloadJobRunner(IDownloadJobRunner? runner)
    {
        if (runner is not IDisposable disposable)
        {
            return;
        }

        if (isProcessingQueue)
        {
            deferredRunnerDisposals.Add(disposable);
            return;
        }

        disposable.Dispose();
    }

    private void DisposeDeferredDownloadJobRunners()
    {
        if (deferredRunnerDisposals.Count == 0)
        {
            return;
        }

        foreach (var disposable in deferredRunnerDisposals)
        {
            disposable.Dispose();
        }

        deferredRunnerDisposals.Clear();
    }

    private void RestoreQueueState()
    {
        var restoredItems = queueStateStore.LoadAsync().GetAwaiter().GetResult();
        DownloadQueueCoverArtCache.Shared.PruneExcept(restoredItems.Select(item => item.Id));
        foreach (var stateItem in restoredItems)
        {
            var item = DownloadQueueItemStateMapper.ToViewModel(stateItem);
            if (IsCompletedWithoutRetry(item))
            {
                item.ReleaseFinishedJobTransientState();
            }

            QueueItems.Add(item);
        }

        SelectedItem = QueueItems.FirstOrDefault();
        queuePaused = QueueItems.Any(item => item.Status == DownloadStatus.Paused);
        GlobalStatusText = QueueItems.Count == 0
            ? DownloadStatusText.Idle
            : queuePaused
                ? DownloadStatusText.Paused
                : DownloadStatusText.QueueRestored;
        NotifyQueueTotalsChanged();
        NotifyPauseResumeChanged();
    }

    private void SaveQueueState()
    {
        _ = SaveQueueStateAsync();
    }

    private Task SaveQueueStateAsync()
    {
        return queueStateStore.SaveAsync(QueueItems.Select(DownloadQueueItemStateMapper.ToStateItem));
    }
}
