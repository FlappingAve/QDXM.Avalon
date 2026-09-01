using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.ViewModels;

public sealed partial class PreviewPlayerViewModel : ViewModelBase, IDisposable
{
    private static readonly string[] PreviewFormatIds = ["5", "6", "7", "27"];
    private readonly DispatcherTimer positionTimer;
    private readonly IUserCredentialStore credentialStore;
    private readonly AppLogService logService;
    private WaveOutEvent? outputDevice;
    private MediaFoundationReader? activeReader;
    private CachedPreview? cachedPreview;
    private string activeContextKey = string.Empty;
    private int playbackVersion;
    private bool disposed;

    public PreviewPlayerViewModel()
        : this(new WindowsCredentialStore(AppDataPaths.PreviewCredentialTargetName), new AppLogService())
    {
    }

    public PreviewPlayerViewModel(
        IUserCredentialStore credentialStore,
        AppLogService logService)
    {
        this.credentialStore = credentialStore;
        this.logService = logService;
        positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        positionTimer.Tick += (_, _) => RefreshCurrentTime();
    }

    [ObservableProperty]
    private string trackName = "No preview";

    [ObservableProperty]
    private string albumName = string.Empty;

    [ObservableProperty]
    private string activeTrackId = string.Empty;

    [ObservableProperty]
    private string currentLengthText = "--:--";

    [ObservableProperty]
    private string trackLengthText = "--:--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseToolTip))]
    [NotifyPropertyChangedFor(nameof(IsNotPlaying))]
    private bool isPlaying;

    [ObservableProperty]
    private double volume = 50;

    public string PlayPauseToolTip => IsPlaying ? "Pause preview" : "Resume preview";
    public bool IsNotPlaying => !IsPlaying;

    public async Task PlayTrackAsync(PreviewTrackRequest track)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (string.IsNullOrWhiteSpace(track.TrackId))
        {
            ShowUnavailable(track);
            return;
        }

        var version = Interlocked.Increment(ref playbackVersion);
        IsPlaying = false;
        positionTimer.Stop();
        ActiveTrackId = string.Empty;
        activeContextKey = track.ContextKey;
        TrackName = string.IsNullOrWhiteSpace(track.Title) ? $"Track {track.TrackId}" : track.Title;
        AlbumName = track.AlbumTitle;
        CurrentLengthText = "--:--";
        TrackLengthText = "--:--";

        try
        {
            if (TryUseCachedTrack(track))
            {
                ActiveTrackId = track.TrackId;
                StartPlayback();
                return;
            }

            DisposePlayback();
            var credential = await credentialStore.ReadAsync().ConfigureAwait(true);
            if (credential is null ||
                string.IsNullOrWhiteSpace(credential.UserId) ||
                string.IsNullOrWhiteSpace(credential.UserAuthToken))
            {
                ShowUnavailable(track);
                return;
            }

            var preview = await Task.Run(() => FetchPreviewStream(track, credential)).ConfigureAwait(true);
            if (version != Volatile.Read(ref playbackVersion))
            {
                preview.Reader.Dispose();
                return;
            }

            activeReader = preview.Reader;
            cachedPreview = new CachedPreview(track, preview.Duration);
            ActiveTrackId = track.TrackId;
            outputDevice = new WaveOutEvent
            {
                Volume = (float)Math.Clamp(Volume / 100, 0, 1)
            };
            outputDevice.Init(activeReader);
            outputDevice.PlaybackStopped += OutputDevice_OnPlaybackStopped;
            TrackLengthText = StringTools.FormatDuration(preview.Duration);
            RefreshCurrentTime();
            StartPlayback();
        }
        catch (Exception ex)
        {
            if (version != Volatile.Read(ref playbackVersion))
            {
                return;
            }

            ShowUnavailable(track);
            logService.Error("Preview", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
    }

    public void ClearIfContext(string contextKey)
    {
        if (string.IsNullOrWhiteSpace(contextKey) ||
            !string.Equals(activeContextKey, contextKey, StringComparison.Ordinal))
        {
            return;
        }

        Clear();
    }

    public void Clear()
    {
        Interlocked.Increment(ref playbackVersion);
        positionTimer.Stop();
        DisposePlayback();
        TrackName = "No preview";
        AlbumName = string.Empty;
        ActiveTrackId = string.Empty;
        activeContextKey = string.Empty;
        CurrentLengthText = "--:--";
        TrackLengthText = "--:--";
        IsPlaying = false;
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (outputDevice is null || activeReader is null)
        {
            return;
        }

        if (outputDevice.PlaybackState == PlaybackState.Playing)
        {
            outputDevice.Pause();
            IsPlaying = false;
            positionTimer.Stop();
            RefreshCurrentTime();
            return;
        }

        ResetIfAtEnd();
        StartPlayback();
    }

    [RelayCommand]
    private void Stop()
    {
        Clear();
    }

    partial void OnVolumeChanged(double value)
    {
        if (outputDevice is not null)
        {
            outputDevice.Volume = (float)Math.Clamp(value / 100, 0, 1);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Clear();
        positionTimer.Stop();
    }

    private bool TryUseCachedTrack(PreviewTrackRequest track)
    {
        if (cachedPreview is null ||
            activeReader is null ||
            outputDevice is null ||
            !string.Equals(cachedPreview.Track.TrackId, track.TrackId, StringComparison.Ordinal))
        {
            return false;
        }

        ResetIfAtEnd();
        TrackLengthText = StringTools.FormatDuration(cachedPreview.Duration);
        RefreshCurrentTime();
        return true;
    }

    private static PreviewStream FetchPreviewStream(PreviewTrackRequest track, UserCredential credential)
    {
        using var service = new QobuzApiServiceFactory(
            credential.AppId,
            credential.AppSecret,
            credential.UserAuthToken).Create();

        Exception? lastFailure = null;
        var failures = new List<string>();
        var emptyUrlCount = 0;
        foreach (var formatId in PreviewFormatIds)
        {
            try
            {
                var fileUrl = service.GetTrackFileUrl(track.TrackId, formatId);
                if (string.IsNullOrWhiteSpace(fileUrl.Url))
                {
                    emptyUrlCount++;
                    var reason = FormatPreviewFileUrlFailure(formatId, fileUrl.Message, fileUrl.Code, fileUrl.Status);
                    failures.Add(reason);
                    lastFailure = new InvalidOperationException(reason);
                    continue;
                }

                var reader = new MediaFoundationReader(fileUrl.Url);
                var duration = reader.TotalTime > TimeSpan.Zero
                    ? reader.TotalTime
                    : TimeSpan.FromSeconds(fileUrl.Duration);

                return new PreviewStream(reader, duration);
            }
            catch (Exception ex)
            {
                failures.Add($"format {formatId}: {ex.Message}");
                lastFailure = ex;
            }
        }

        var detail = failures.Count == 0
            ? "No format attempts were made."
            : string.Join("; ", failures);
        if (emptyUrlCount == PreviewFormatIds.Length)
        {
            throw new InvalidOperationException($"\"{track.Title}\" (track {track.TrackId}): No playable preview. It may be unavailable in the preview account's region.", lastFailure);
        }

        throw new InvalidOperationException($"Qobuz did not return a playable preview URL. {detail}", lastFailure);
    }

    private static string FormatPreviewFileUrlFailure(
        string formatId,
        string? message,
        string? code,
        string? status)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(message))
        {
            details.Add(message.Trim());
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            details.Add($"code {code.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            details.Add($"status {status.Trim()}");
        }

        return details.Count == 0
            ? $"format {formatId}: no URL"
            : $"format {formatId}: {string.Join(", ", details)}";
    }

    private void StartPlayback()
    {
        if (outputDevice is null)
        {
            return;
        }

        outputDevice.Play();
        IsPlaying = true;
        positionTimer.Start();
    }

    private void DisposePlayback()
    {
        var output = outputDevice;
        outputDevice = null;
        if (output is not null)
        {
            output.PlaybackStopped -= OutputDevice_OnPlaybackStopped;
            output.Stop();
            output.Dispose();
        }

        activeReader?.Dispose();
        activeReader = null;
        cachedPreview = null;
        IsPlaying = false;
    }

    private void OutputDevice_OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, outputDevice))
            {
                return;
            }

            IsPlaying = false;
            positionTimer.Stop();
            RefreshCurrentTime();

            if (e.Exception is not null)
            {
                logService.Error("Preview", SafeErrorText.FormatUnexpectedLogMessage(e.Exception));
                if (cachedPreview is not null)
                {
                    ShowUnavailable(cachedPreview.Track);
                }
            }
        });
    }

    private void RefreshCurrentTime()
    {
        if (activeReader is null)
        {
            CurrentLengthText = "--:--";
            return;
        }

        CurrentLengthText = StringTools.FormatDuration(activeReader.CurrentTime);
    }

    private void ResetIfAtEnd()
    {
        if (activeReader is null ||
            cachedPreview is null ||
            activeReader.CurrentTime < cachedPreview.Duration - TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        activeReader.CurrentTime = TimeSpan.Zero;
        RefreshCurrentTime();
    }

    private void ShowUnavailable(PreviewTrackRequest track)
    {
        DisposePlayback();
        TrackName = "Preview Unavailable";
        AlbumName = string.IsNullOrWhiteSpace(track.Title) ? track.AlbumTitle : track.Title;
        ActiveTrackId = string.Empty;
        CurrentLengthText = "--:--";
        TrackLengthText = "--:--";
        IsPlaying = false;
    }

    private sealed record PreviewStream(
        MediaFoundationReader Reader,
        TimeSpan Duration);

    private sealed record CachedPreview(
        PreviewTrackRequest Track,
        TimeSpan Duration);
}

public sealed record PreviewTrackRequest(
    string TrackId,
    string Title,
    string AlbumTitle,
    string ContextKey);
