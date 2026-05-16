using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.FileIO;
using QobuzApiSharp.Models.Content;
using QobuzApiSharp.Service;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Search;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Core.Downloads;

public sealed class QobuzDownloadJobRunner : IDownloadJobRunner, IDisposable
{
    internal const int PlaylistPreviewSamplePathCount = 2;
    internal const string PlaylistFolderCoverFileName = "PlaylistCover.jpg";
    internal const int FavoritesPreviewSamplePathCount = 2;
    internal const int DownloadStreamMaxAttempts = 2;
    internal static readonly TimeSpan DownloadReadInactivityTimeout = TimeSpan.FromSeconds(30);
    private readonly QobuzApiServiceFactory serviceFactory;
    private readonly AppSettings settings;
    private readonly HttpClient httpClient = new();

    internal sealed record ResolvedDownloadStream(
        string Url,
        AudioQualityDescriptor Quality);

    internal delegate IAsyncEnumerable<DownloadEvent> PlaylistTrackDownload(
        Track track,
        string coverDestination,
        string? coverArtPath,
        int completedTrackNumber,
        int totalTracks,
        Func<ResolvedDownloadStream, string>? resolvedFilePathFactory,
        int displayTrackNumber,
        int displayTotalTracks,
        CancellationToken cancellationToken);

    internal delegate IAsyncEnumerable<DownloadEvent> FavoriteTrackDownload(
        Track track,
        string destination,
        string? coverArtPath,
        int completedTrackNumber,
        int totalTracks,
        int? trackNumberPaddingWidth,
        int displayTrackNumber,
        int displayTotalTracks,
        CancellationToken cancellationToken);

    internal sealed record PlaylistRunnerServices(
        Func<string, (Playlist Playlist, IReadOnlyList<Track> Tracks)> FetchPlaylistTracks,
        Func<Image?, string?, string, CancellationToken, bool, string, Task<CoverArtDownloadResult>> DownloadCoverArtAsync,
        Action<string?> DeleteTemporaryCoverArt,
        Action DeleteTemporaryCoverArtCache,
        PlaylistTrackDownload DownloadTrackAsync);

    internal sealed record FavoritesRunnerServices(
        Func<IReadOnlyList<string>> FetchFavoriteAlbumIds,
        Func<IReadOnlyList<int>> FetchFavoriteTrackIds,
        Func<string, Album> GetAlbum,
        Func<string, Track> GetTrack,
        Func<Image?, string?, string, CancellationToken, bool, string, Task<CoverArtDownloadResult>> DownloadCoverArtAsync,
        Func<Album, string, CancellationToken, Task<bool>> DownloadBookletsAsync,
        Action<string?> DeleteTemporaryCoverArt,
        Action DeleteTemporaryCoverArtCache,
        FavoriteTrackDownload DownloadTrackAsync);

    private sealed record FavoriteAlbumPlan(
        Album Album,
        IReadOnlyList<Track> Tracks,
        string Destination,
        string CoverArtUrl,
        string Quality,
        IReadOnlyList<(int TrackNumber, int DiscNumber)> TrackNumberScopes);

    private sealed record FavoriteTrackPlan(
        Track Track,
        string Destination,
        string CoverArtUrl,
        string Quality);

    public QobuzDownloadJobRunner(QobuzApiServiceFactory serviceFactory, AppSettings settings)
    {
        this.serviceFactory = serviceFactory;
        this.settings = settings;
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/110.0");
    }

    public async IAsyncEnumerable<DownloadEvent> RunAsync(
        DownloadQueueItem item,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!TryResolveDownloadRequest(item, out var request) || request is null)
        {
            yield return new DownloadFailedEvent(item.Id, "Invalid Qobuz URL.");
            yield break;
        }

        var jobSettings = settings.CreateSnapshot();
        var coverArtDownloadService = new CoverArtDownloadService(httpClient, jobSettings);
        using var service = serviceFactory.Create();

        if (request.ContentType == DownloadContentType.Track)
        {
            await foreach (var downloadEvent in RunTrackAsync(
                item,
                service.GetTrack(request.ContentId, true),
                jobSettings,
                coverArtDownloadService,
                cancellationToken))
            {
                yield return downloadEvent;
            }

            yield break;
        }

        if (request.ContentType == DownloadContentType.Playlist)
        {
            await foreach (var downloadEvent in RunPlaylistAsync(
                item,
                service,
                request.ContentId,
                jobSettings,
                coverArtDownloadService,
                cancellationToken))
            {
                yield return downloadEvent;
            }

            yield break;
        }

        if (request.ContentType == DownloadContentType.Favorites)
        {
            await foreach (var downloadEvent in RunFavoritesAsync(
                item,
                service,
                request.ContentId,
                jobSettings,
                coverArtDownloadService,
                cancellationToken))
            {
                yield return downloadEvent;
            }

            yield break;
        }

        if (!DownloadRequestSupport.IsSupportedNow(request.ContentType))
        {
            yield return new DownloadFailedEvent(item.Id, DownloadRequestSupport.GetUnsupportedMessage(request.ContentType));
            yield break;
        }

        var album = QobuzPagination.FetchAlbumWithAllTracks(
            (limit, offset) => service.GetAlbum(request.ContentId, true, null, limit, offset));
        var allTracks = album.Tracks?.Items ?? [];
        var selectedTrackIds = item.SelectedTrackIds.Count == 0
            ? null
            : new HashSet<string>(item.SelectedTrackIds, StringComparer.Ordinal);

        var tracksToDownload = selectedTrackIds is null
            ? allTracks
            : allTracks.Where(track => track.Id is not null && selectedTrackIds.Contains(track.Id.Value.ToString())).ToList();

        if (tracksToDownload.Count == 0)
        {
            yield return new DownloadFailedEvent(item.Id, "No album tracks were available to download.");
            yield break;
        }

        var albumSearch = QobuzApiSearchMapper.ToAlbumResult(album);
        var effectiveQuality = QualityStringMappings.GetEffectiveQuality(jobSettings.FormatId, album);
        var destination = CreateAlbumDestination(album, albumSearch.Artist, albumSearch.Title, effectiveQuality.DisplayQuality, jobSettings);
        Directory.CreateDirectory(destination);
        var orderedTracks = tracksToDownload
            .OrderBy(track => track.MediaNumber ?? 1)
            .ThenBy(track => track.TrackNumber ?? int.MaxValue)
            .ToList();
        foreach (var track in orderedTracks)
        {
            track.Album = album;
        }

        var trackNumberScopes = GetTrackNumberScopes(orderedTracks);
        var plannedFilePaths = orderedTracks
            .Select(track => GetTrackFilePath(
                destination,
                track,
                effectiveQuality.DisplayQuality,
                effectiveQuality.DisplayQuality,
                tracksToDownload.Count,
                track.TrackNumber ?? 0,
                PathTemplateRenderer.GetTrackNumberPaddingWidth(trackNumberScopes, track.MediaNumber ?? 0, jobSettings.DiscFolderTemplate),
                QualityStringMappings.GetAudioExtension(jobSettings.FormatId),
                jobSettings))
            .ToList();

        yield return new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Album,
            albumSearch.Title,
            albumSearch.Artist,
            effectiveQuality.DisplayQuality,
            tracksToDownload.Count,
            albumSearch.ThumbnailUrl,
            albumSearch.ReleaseDate,
            albumSearch.Upc ?? string.Empty,
            destination,
            plannedFilePaths);

        var coverArt = await coverArtDownloadService.DownloadAsync(
            album.Image,
            albumSearch.ThumbnailUrl,
            destination,
            cancellationToken,
            saveFolderCover: ShouldSaveStandardFolderCover(jobSettings));
        if (!string.IsNullOrWhiteSpace(coverArt.WarningMessage))
        {
            yield return new DownloadWarningEvent(item.Id, coverArt.WarningMessage);
        }

        var coverArtPath = coverArt.Path;
        var completed = Math.Clamp(item.CompletedTracks, 0, orderedTracks.Count);
        var hasWarnings = false;
        try
        {
            foreach (var track in orderedTracks.Skip(completed))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var trackFailed = false;
                var nextCompletedCount = completed + 1;

                await foreach (var downloadEvent in DownloadTrackFileAsync(
                    service,
                    item,
                    track,
                    destination,
                    coverArtPath,
                    nextCompletedCount,
                    tracksToDownload.Count,
                    PathTemplateRenderer.GetTrackNumberPaddingWidth(trackNumberScopes, track.MediaNumber ?? 0, jobSettings.DiscFolderTemplate),
                    jobSettings,
                    cancellationToken))
                {
                    yield return downloadEvent;
                    trackFailed = trackFailed || downloadEvent is DownloadFailedEvent;
                    hasWarnings = hasWarnings || downloadEvent is DownloadWarningEvent;
                }

                if (trackFailed)
                {
                    yield break;
                }

                completed = nextCompletedCount;
            }

            hasWarnings = hasWarnings || (jobSettings.DownloadGoodies && !await DownloadBookletsAsync(album, destination, cancellationToken));
            yield return new DownloadCompletedEvent(item.Id, hasWarnings);
        }
        finally
        {
            coverArtDownloadService.DeleteTemporaryCoverArt(coverArtPath);
        }
    }

    private static bool TryResolveDownloadRequest(DownloadQueueItem item, out DownloadRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(item.ContentId))
        {
            request = new DownloadRequest(item.SourceUrl, item.Type, item.ContentId);
            return true;
        }

        return DownloadUrlParser.TryParseDownloadUrl(item.SourceUrl, out request);
    }

    private async IAsyncEnumerable<DownloadEvent> RunTrackAsync(
        DownloadQueueItem item,
        Track track,
        AppSettings jobSettings,
        CoverArtDownloadService coverArtDownloadService,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (track.Album is null)
        {
            yield return new DownloadFailedEvent(item.Id, "Track metadata did not include album context.");
            yield break;
        }

        var trackSearch = QobuzApiSearchMapper.ToTrackResult(track);
        var effectiveQuality = QualityStringMappings.GetEffectiveQuality(jobSettings.FormatId, track.Album);
        var destination = CreateAlbumDestination(track.Album, trackSearch.Artist, trackSearch.AlbumTitle, effectiveQuality.DisplayQuality, jobSettings);
        Directory.CreateDirectory(destination);
        var coverArt = await coverArtDownloadService.DownloadAsync(
            track.Album.Image,
            trackSearch.ThumbnailUrl,
            destination,
            cancellationToken,
            saveFolderCover: ShouldSaveStandardFolderCover(jobSettings));
        var coverArtPath = coverArt.Path;
        var plannedFilePath = GetTrackFilePath(
            albumDestination: destination,
            track: track,
            trackQuality: effectiveQuality.DisplayQuality,
            folderQuality: effectiveQuality.DisplayQuality,
            totalTracks: 1,
            fallbackTrackNumber: 1,
            trackNumberPaddingWidth: null,
            extension: QualityStringMappings.GetAudioExtension(jobSettings.FormatId),
            jobSettings: jobSettings);

        yield return new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Track,
            trackSearch.Title,
            trackSearch.Artist,
            effectiveQuality.DisplayQuality,
            1,
            trackSearch.ThumbnailUrl,
            trackSearch.ReleaseDate,
            track.Album.Upc ?? string.Empty,
            destination,
            [plannedFilePath]);
        if (!string.IsNullOrWhiteSpace(coverArt.WarningMessage))
        {
            yield return new DownloadWarningEvent(item.Id, coverArt.WarningMessage);
        }

        using var service = serviceFactory.Create();
        var trackFailed = false;
        var hasWarnings = false;
        try
        {
            await foreach (var downloadEvent in DownloadTrackFileAsync(
                service,
                item,
                track,
                destination,
                coverArtPath,
                completedTrackNumber: 1,
                totalTracks: 1,
                trackNumberPaddingWidth: null,
                jobSettings: jobSettings,
                cancellationToken: cancellationToken))
            {
                yield return downloadEvent;
                trackFailed = trackFailed || downloadEvent is DownloadFailedEvent;
                hasWarnings = hasWarnings || downloadEvent is DownloadWarningEvent;
            }

            if (trackFailed)
            {
                yield break;
            }

            yield return new DownloadCompletedEvent(item.Id, hasWarnings);
        }
        finally
        {
            coverArtDownloadService.DeleteTemporaryCoverArt(coverArtPath);
        }
    }

    private async IAsyncEnumerable<DownloadEvent> RunPlaylistAsync(
        DownloadQueueItem item,
        QobuzApiService service,
        string playlistId,
        AppSettings jobSettings,
        CoverArtDownloadService coverArtDownloadService,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var services = new PlaylistRunnerServices(
            id => FetchPlaylistTracks(service, id),
            (image, coverArtUrl, destination, token, saveFolderCover, folderCoverFileName) =>
                coverArtDownloadService.DownloadAsync(image, coverArtUrl, destination, token, saveFolderCover, folderCoverFileName),
            coverArtDownloadService.DeleteTemporaryCoverArt,
            coverArtDownloadService.DeleteTemporaryCoverArtCache,
            (track, coverDestination, coverArtPath, completedTrackNumber, totalTracks, resolvedFilePathFactory, displayTrackNumber, displayTotalTracks, token) =>
                DownloadTrackFileAsync(
                    service,
                    item,
                    track,
                    coverDestination,
                    coverArtPath,
                    completedTrackNumber,
                    totalTracks,
                    trackNumberPaddingWidth: null,
                    jobSettings,
                    token,
                    resolvedFilePathFactory: resolvedFilePathFactory,
                    displayTrackNumber: displayTrackNumber,
                    displayTotalTracks: displayTotalTracks));

        await foreach (var downloadEvent in RunPlaylistAsync(item, playlistId, jobSettings, services, cancellationToken))
        {
            yield return downloadEvent;
        }
    }

    internal static async IAsyncEnumerable<DownloadEvent> RunPlaylistAsync(
        DownloadQueueItem item,
        string playlistId,
        AppSettings jobSettings,
        PlaylistRunnerServices services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (playlist, allPlaylistTracks) = services.FetchPlaylistTracks(playlistId);
        var partialPlaylistSelection = item.SelectedTrackIds.Count > 0;
        var playlistTracks = partialPlaylistSelection
            ? SelectPlaylistTracks(allPlaylistTracks, item.SelectedTrackIds)
            : allPlaylistTracks;
        if (playlistTracks.Count == 0)
        {
            yield return new DownloadFailedEvent(item.Id, "No playlist tracks were available to download.");
            yield break;
        }

        var playlistTitle = string.IsNullOrWhiteSpace(playlist.Name)
            ? $"Playlist {playlistId}"
            : playlist.Name.Trim();
        var playlistOwner = string.IsNullOrWhiteSpace(playlist.Owner?.Name)
            ? "Unknown Owner"
            : playlist.Owner.Name.Trim();
        var playlistTotalTracks = playlistTracks.Count;
        var playlistCoverUrl = FirstNonEmptyPlaylistImage(playlist) ??
            CoverArtUrlSelector.GetBestImageUrl(playlistTracks.FirstOrDefault(track => track.Album is not null)?.Album?.Image);
        var playlistQuality = QualityStringMappings.GetQualityLabelFromFormatId(jobSettings.FormatId);
        if (string.IsNullOrWhiteSpace(playlistQuality))
        {
            playlistQuality = jobSettings.SelectedQuality;
        }

        var keepPlaylistTogether = IsKeepPlaylistTogether(jobSettings);
        var playlistDestination = keepPlaylistTogether
            ? CreatePlaylistDestination(
                playlistTracks[0],
                playlistId,
                playlistTitle,
                playlistOwner,
                playlistTotalTracks,
                jobSettings)
            : jobSettings.EffectiveDownloadFolder;
        Directory.CreateDirectory(playlistDestination);

        var plannedFilePaths = playlistTracks
            .Select((track, index) => (Track: track, Index: index))
            .Where(item => item.Track.Album is not null)
            .Take(PlaylistPreviewSamplePathCount)
            .Select(item => keepPlaylistTogether
                ? GetPlaylistTrackFilePath(
                    playlistDestination,
                    item.Track,
                    playlistId,
                    playlistTitle,
                    playlistOwner,
                    partialPlaylistSelection
                        ? item.Index + 1
                        : GetPlaylistNumber(item.Track, item.Index),
                    playlistTotalTracks,
                    jobSettings)
                : GetStandardPlaylistTrackFilePath(item.Track, jobSettings))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        yield return new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Playlist,
            playlistTitle,
            playlistOwner,
            playlistQuality,
            playlistTotalTracks,
            playlistCoverUrl ?? string.Empty,
            string.Empty,
            string.Empty,
            playlistDestination,
            plannedFilePaths,
            DestinationPreviewRemainingCount: Math.Max(0, playlistTotalTracks - plannedFilePaths.Count));

        string? playlistFolderCoverPath = null;
        if (keepPlaylistTogether)
        {
            var playlistFolderCover = await services.DownloadCoverArtAsync(
                null,
                playlistCoverUrl,
                playlistDestination,
                cancellationToken,
                true,
                PlaylistFolderCoverFileName);
            playlistFolderCoverPath = playlistFolderCover.Path;
            if (!string.IsNullOrWhiteSpace(playlistFolderCover.WarningMessage))
            {
                yield return new DownloadWarningEvent(item.Id, playlistFolderCover.WarningMessage);
            }
        }

        var completed = Math.Clamp(item.CompletedTracks, 0, playlistTracks.Count);
        var retryPlaylistPositions = item.FailedPlaylistPositions
            .Where(position => position > 0)
            .ToHashSet();
        var retryingFailedTracks = retryPlaylistPositions.Count > 0 && completed >= playlistTracks.Count;
        var failedTrackCount = retryingFailedTracks ? 0 : retryPlaylistPositions.Count;
        var hasTrackWarnings = false;

        try
        {
            var startIndex = retryingFailedTracks ? 0 : completed;
            for (var index = startIndex; index < playlistTracks.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var track = playlistTracks[index];
                var playlistNumber = partialPlaylistSelection
                    ? index + 1
                    : GetPlaylistNumber(track, index);
                if (retryingFailedTracks && !retryPlaylistPositions.Contains(playlistNumber))
                {
                    continue;
                }

                var nextCompletedCount = retryingFailedTracks ? playlistTotalTracks : index + 1;

                if (track.Album is null)
                {
                    failedTrackCount++;
                    yield return new PlaylistTrackFailedEvent(
                        item.Id,
                        playlistNumber,
                        $"Playlist track {playlistNumber} failed: track metadata did not include album context.");
                    continue;
                }

                var coverDestination = keepPlaylistTogether
                    ? playlistDestination
                    : CreateAlbumDestinationForTrack(track, jobSettings);
                Func<ResolvedDownloadStream, string>? resolvedFilePathFactory = keepPlaylistTogether
                    ? stream => GetPlaylistTrackFilePath(
                        playlistDestination,
                        track,
                        playlistId,
                        playlistTitle,
                        playlistOwner,
                        playlistNumber,
                        playlistTotalTracks,
                        jobSettings,
                        stream.Quality.DisplayQuality,
                        stream.Quality.Extension)
                    : null;
                var coverArt = await services.DownloadCoverArtAsync(
                    track.Album.Image,
                    CoverArtUrlSelector.GetBestImageUrl(track.Album.Image),
                    coverDestination,
                    cancellationToken,
                    !keepPlaylistTogether && ShouldSaveStandardFolderCover(jobSettings),
                    "Cover.jpg");
                if (!string.IsNullOrWhiteSpace(coverArt.WarningMessage))
                {
                    yield return new DownloadWarningEvent(item.Id, coverArt.WarningMessage);
                }

                var coverArtPath = coverArt.Path;
                var albumTotalTracks = GetAlbumTotalTracks(track);

                var trackFailed = false;
                var failureMessage = string.Empty;
                var trackEvents = services.DownloadTrackAsync(
                    track,
                    coverDestination,
                    coverArtPath,
                    nextCompletedCount,
                    albumTotalTracks,
                    resolvedFilePathFactory,
                    playlistNumber,
                    playlistTotalTracks,
                    cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);

                try
                {
                    while (true)
                    {
                        DownloadEvent downloadEvent;
                        try
                        {
                            if (!await trackEvents.MoveNextAsync())
                            {
                                break;
                            }

                            downloadEvent = trackEvents.Current;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            trackFailed = true;
                            failureMessage = $"{ex.GetType().Name}: {ex.Message}";
                            break;
                        }

                        if (downloadEvent is DownloadWarningEvent)
                        {
                            hasTrackWarnings = true;
                        }

                        if (downloadEvent is DownloadFailedEvent failed)
                        {
                            trackFailed = true;
                            failureMessage = failed.Message;
                            continue;
                        }

                        yield return downloadEvent;
                    }
                }
                finally
                {
                    await trackEvents.DisposeAsync();
                }

                if (trackFailed)
                {
                    failedTrackCount++;
                    yield return new PlaylistTrackFailedEvent(
                        item.Id,
                        playlistNumber,
                        $"Playlist track {playlistNumber} failed: {failureMessage}");
                }
            }
        }
        finally
        {
            services.DeleteTemporaryCoverArt(playlistFolderCoverPath);
            services.DeleteTemporaryCoverArtCache();
        }

        if (failedTrackCount > 0)
        {
            yield return new DownloadWarningEvent(
                item.Id,
                $"{failedTrackCount} playlist tracks failed. See Logs.");
        }

        yield return new DownloadCompletedEvent(item.Id, HasWarnings: failedTrackCount > 0 || hasTrackWarnings);
    }

    private async IAsyncEnumerable<DownloadEvent> RunFavoritesAsync(
        DownloadQueueItem item,
        QobuzApiService service,
        string favoriteType,
        AppSettings jobSettings,
        CoverArtDownloadService coverArtDownloadService,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var services = new FavoritesRunnerServices(
            () => FetchFavoriteAlbumIds(service),
            () => FetchFavoriteTrackIds(service),
            albumId => QobuzPagination.FetchAlbumWithAllTracks(
                (limit, offset) => service.GetAlbum(albumId, true, null, limit, offset)),
            trackId => service.GetTrack(trackId, true),
            (image, coverArtUrl, destination, token, saveFolderCover, folderCoverFileName) =>
                coverArtDownloadService.DownloadAsync(image, coverArtUrl, destination, token, saveFolderCover, folderCoverFileName),
            DownloadBookletsAsync,
            coverArtDownloadService.DeleteTemporaryCoverArt,
            coverArtDownloadService.DeleteTemporaryCoverArtCache,
            (track, destination, coverArtPath, completedTrackNumber, totalTracks, trackNumberPaddingWidth, displayTrackNumber, displayTotalTracks, token) =>
                DownloadTrackFileAsync(
                    service,
                    item,
                    track,
                    destination,
                    coverArtPath,
                    completedTrackNumber,
                    totalTracks,
                    trackNumberPaddingWidth,
                    jobSettings,
                    token,
                    displayTrackNumber: displayTrackNumber,
                    displayTotalTracks: displayTotalTracks));

        await foreach (var downloadEvent in RunFavoritesAsync(item, favoriteType, jobSettings, services, cancellationToken))
        {
            yield return downloadEvent;
        }
    }

    internal static async IAsyncEnumerable<DownloadEvent> RunFavoritesAsync(
        DownloadQueueItem item,
        string favoriteType,
        AppSettings jobSettings,
        FavoritesRunnerServices services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.Equals(favoriteType, "albums", StringComparison.OrdinalIgnoreCase))
        {
            await foreach (var downloadEvent in RunFavoriteAlbumsAsync(item, jobSettings, services, cancellationToken))
            {
                yield return downloadEvent;
            }

            yield break;
        }

        if (string.Equals(favoriteType, "tracks", StringComparison.OrdinalIgnoreCase))
        {
            await foreach (var downloadEvent in RunFavoriteTracksAsync(item, jobSettings, services, cancellationToken))
            {
                yield return downloadEvent;
            }

            yield break;
        }

        yield return new DownloadFailedEvent(item.Id, "Unsupported favorites URL. Use a Qobuz favorite albums or favorite tracks URL.");
    }

    private static async IAsyncEnumerable<DownloadEvent> RunFavoriteAlbumsAsync(
        DownloadQueueItem item,
        AppSettings jobSettings,
        FavoritesRunnerServices services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plans = new List<FavoriteAlbumPlan>();
        var skippedItems = 0;

        foreach (var albumId in services.FetchFavoriteAlbumIds().Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Album? album = null;
            DownloadWarningEvent? resolveWarning = null;
            try
            {
                album = services.GetAlbum(albumId);
            }
            catch (Exception ex)
            {
                skippedItems++;
                resolveWarning = new DownloadWarningEvent(item.Id, $"Favorite album {albumId} failed to resolve: {ex.Message}", ex);
            }

            if (resolveWarning is not null || album is null)
            {
                yield return resolveWarning ?? new DownloadWarningEvent(item.Id, $"Favorite album {albumId} failed to resolve.");
                continue;
            }

            var tracks = album.Tracks?.Items?
                .Where(track => track is not null)
                .OrderBy(track => track.MediaNumber ?? 1)
                .ThenBy(track => track.TrackNumber ?? int.MaxValue)
                .ToList() ?? [];

            if (tracks.Count == 0)
            {
                skippedItems++;
                yield return new DownloadWarningEvent(item.Id, $"Favorite album {album.Id} did not include downloadable track metadata.");
                continue;
            }

            foreach (var track in tracks)
            {
                track.Album = album;
            }

            var albumSearch = QobuzApiSearchMapper.ToAlbumResult(album);
            var quality = QualityStringMappings.GetEffectiveQuality(jobSettings.FormatId, album).DisplayQuality;
            plans.Add(new FavoriteAlbumPlan(
                album,
                tracks,
                CreateAlbumDestination(album, albumSearch.Artist, albumSearch.Title, quality, jobSettings),
                albumSearch.ThumbnailUrl,
                quality,
                GetTrackNumberScopes(tracks)));
        }

        if (plans.Count == 0)
        {
            yield return new DownloadFailedEvent(item.Id, "No favorite albums were available to download.");
            yield break;
        }

        var totalTracks = plans.Sum(plan => plan.Tracks.Count);
        var previewPaths = CreateFavoriteAlbumPreviewPaths(plans, jobSettings);
        yield return new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Favorites,
            "Favorite Albums",
            "Qobuz Library",
            jobSettings.SelectedQuality,
            totalTracks,
            plans.FirstOrDefault()?.CoverArtUrl ?? string.Empty,
            string.Empty,
            string.Empty,
            jobSettings.EffectiveDownloadFolder,
            previewPaths,
            DestinationPreviewRemainingCount: Math.Max(0, totalTracks - previewPaths.Count));

        var completedToSkip = Math.Clamp(item.CompletedTracks, 0, totalTracks);
        var completedTracks = completedToSkip;
        var hasTrackWarnings = skippedItems > 0;
        var failedTracks = 0;

        try
        {
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (completedToSkip >= plan.Tracks.Count)
                {
                    completedToSkip -= plan.Tracks.Count;
                    continue;
                }

                Directory.CreateDirectory(plan.Destination);
                var coverArt = await services.DownloadCoverArtAsync(
                    plan.Album.Image,
                    plan.CoverArtUrl,
                    plan.Destination,
                    cancellationToken,
                    ShouldSaveStandardFolderCover(jobSettings),
                    "Cover.jpg");
                if (!string.IsNullOrWhiteSpace(coverArt.WarningMessage))
                {
                    yield return new DownloadWarningEvent(item.Id, coverArt.WarningMessage);
                }

                var coverArtPath = coverArt.Path;

                try
                {
                    var albumTrackTotal = plan.Tracks.Count;
                    foreach (var track in plan.Tracks.Skip(completedToSkip))
                    {
                        var nextCompletedCount = completedTracks + 1;
                        var trackFailed = false;
                        var failureMessage = string.Empty;

                        await foreach (var downloadEvent in EnumerateFavoriteTrackEvents(
                            services,
                            track,
                            plan.Destination,
                            coverArtPath,
                            nextCompletedCount,
                            albumTrackTotal,
                            PathTemplateRenderer.GetTrackNumberPaddingWidth(
                                plan.TrackNumberScopes,
                                track.MediaNumber ?? 0,
                                jobSettings.DiscFolderTemplate),
                            nextCompletedCount,
                            totalTracks,
                            cancellationToken))
                        {
                            if (downloadEvent is DownloadFailedEvent failed)
                            {
                                trackFailed = true;
                                failureMessage = failed.Message;
                                continue;
                            }

                            hasTrackWarnings = hasTrackWarnings || downloadEvent is DownloadWarningEvent;
                            yield return downloadEvent;
                        }

                        if (trackFailed)
                        {
                            failedTracks++;
                            yield return new DownloadWarningEvent(
                                item.Id,
                                $"Favorite album track {nextCompletedCount} failed: {failureMessage}");
                        }

                        completedTracks = nextCompletedCount;
                    }
                }
                finally
                {
                    services.DeleteTemporaryCoverArt(coverArtPath);
                }

                completedToSkip = 0;
                if (jobSettings.DownloadGoodies && !await services.DownloadBookletsAsync(plan.Album, plan.Destination, cancellationToken))
                {
                    hasTrackWarnings = true;
                    yield return new DownloadWarningEvent(item.Id, $"Favorite album {plan.Album.Title} booklet download failed.");
                }
            }
        }
        finally
        {
            services.DeleteTemporaryCoverArtCache();
        }

        if (skippedItems > 0)
        {
            yield return new DownloadWarningEvent(item.Id, $"{skippedItems} favorite albums failed to resolve. See Logs.");
        }

        if (failedTracks > 0)
        {
            yield return new DownloadWarningEvent(item.Id, $"{failedTracks} favorite album tracks failed. See Logs.");
        }

        yield return new DownloadCompletedEvent(item.Id, skippedItems > 0 || failedTracks > 0 || hasTrackWarnings);
    }

    private static async IAsyncEnumerable<DownloadEvent> RunFavoriteTracksAsync(
        DownloadQueueItem item,
        AppSettings jobSettings,
        FavoritesRunnerServices services,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plans = new List<FavoriteTrackPlan>();
        var skippedItems = 0;

        foreach (var trackId in services.FetchFavoriteTrackIds())
        {
            cancellationToken.ThrowIfCancellationRequested();

            Track? track = null;
            DownloadWarningEvent? resolveWarning = null;
            try
            {
                track = services.GetTrack(trackId.ToString());
            }
            catch (Exception ex)
            {
                skippedItems++;
                resolveWarning = new DownloadWarningEvent(item.Id, $"Favorite track {trackId} failed to resolve: {ex.Message}", ex);
            }

            if (resolveWarning is not null || track is null)
            {
                yield return resolveWarning ?? new DownloadWarningEvent(item.Id, $"Favorite track {trackId} failed to resolve.");
                continue;
            }

            if (track.Album is null)
            {
                skippedItems++;
                yield return new DownloadWarningEvent(item.Id, $"Favorite track {trackId} did not include album context.");
                continue;
            }

            var context = ResolveTrackContext(track, jobSettings);
            plans.Add(new FavoriteTrackPlan(
                track,
                CreateAlbumDestinationForTrack(track, jobSettings),
                CoverArtUrlSelector.GetBestImageUrl(track.Album.Image),
                context.Quality));
        }

        if (plans.Count == 0)
        {
            yield return new DownloadFailedEvent(item.Id, "No favorite tracks were available to download.");
            yield break;
        }

        var totalTracks = plans.Count;
        var previewPaths = CreateFavoriteTrackPreviewPaths(plans, jobSettings);
        yield return new DownloadResolvedEvent(
            item.Id,
            DownloadContentType.Favorites,
            "Favorite Tracks",
            "Qobuz Library",
            jobSettings.SelectedQuality,
            totalTracks,
            plans.FirstOrDefault()?.CoverArtUrl ?? string.Empty,
            string.Empty,
            string.Empty,
            jobSettings.EffectiveDownloadFolder,
            previewPaths,
            DestinationPreviewRemainingCount: Math.Max(0, totalTracks - previewPaths.Count));

        var completedToSkip = Math.Clamp(item.CompletedTracks, 0, totalTracks);
        var hasTrackWarnings = skippedItems > 0;
        var failedTracks = 0;

        try
        {
            for (var index = completedToSkip; index < plans.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var plan = plans[index];
                Directory.CreateDirectory(plan.Destination);
                var coverArt = await services.DownloadCoverArtAsync(
                    plan.Track.Album?.Image,
                    plan.CoverArtUrl,
                    plan.Destination,
                    cancellationToken,
                    ShouldSaveStandardFolderCover(jobSettings),
                    "Cover.jpg");
                if (!string.IsNullOrWhiteSpace(coverArt.WarningMessage))
                {
                    yield return new DownloadWarningEvent(item.Id, coverArt.WarningMessage);
                }

                var coverArtPath = coverArt.Path;

                try
                {
                    var completedTracks = index + 1;
                    var trackFailed = false;
                    var failureMessage = string.Empty;

                    await foreach (var downloadEvent in EnumerateFavoriteTrackEvents(
                        services,
                        plan.Track,
                        plan.Destination,
                        coverArtPath,
                        completedTracks,
                        GetAlbumTotalTracks(plan.Track),
                        trackNumberPaddingWidth: null,
                        completedTracks,
                        totalTracks,
                        cancellationToken))
                    {
                        if (downloadEvent is DownloadFailedEvent failed)
                        {
                            trackFailed = true;
                            failureMessage = failed.Message;
                            continue;
                        }

                        hasTrackWarnings = hasTrackWarnings || downloadEvent is DownloadWarningEvent;
                        yield return downloadEvent;
                    }

                    if (trackFailed)
                    {
                        failedTracks++;
                        yield return new DownloadWarningEvent(
                            item.Id,
                            $"Favorite track {completedTracks} failed: {failureMessage}");
                    }
                }
                finally
                {
                    services.DeleteTemporaryCoverArt(coverArtPath);
                }
            }
        }
        finally
        {
            services.DeleteTemporaryCoverArtCache();
        }

        var totalFailures = skippedItems + failedTracks;
        if (totalFailures > 0)
        {
            yield return new DownloadWarningEvent(item.Id, $"{totalFailures} favorite tracks failed. See Logs.");
        }

        yield return new DownloadCompletedEvent(item.Id, totalFailures > 0 || hasTrackWarnings);
    }

    private static async IAsyncEnumerable<DownloadEvent> EnumerateFavoriteTrackEvents(
        FavoritesRunnerServices services,
        Track track,
        string destination,
        string? coverArtPath,
        int completedTrackNumber,
        int totalTracks,
        int? trackNumberPaddingWidth,
        int displayTrackNumber,
        int displayTotalTracks,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var downloadEvent in services.DownloadTrackAsync(
            track,
            destination,
            coverArtPath,
            completedTrackNumber,
            totalTracks,
            trackNumberPaddingWidth,
            displayTrackNumber,
            displayTotalTracks,
            cancellationToken))
        {
            yield return downloadEvent;
        }
    }

    private async IAsyncEnumerable<DownloadEvent> DownloadTrackFileAsync(
        QobuzApiService service,
        DownloadQueueItem item,
        Track track,
        string destination,
        string? coverArtPath,
        int completedTrackNumber,
        int totalTracks,
        int? trackNumberPaddingWidth,
        AppSettings jobSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        Func<ResolvedDownloadStream, string>? resolvedFilePathFactory = null,
        int? displayTrackNumber = null,
        int? displayTotalTracks = null)
    {
        var trackId = track.Id?.ToString() ?? throw new InvalidOperationException("Track ID is missing.");
        var trackNumber = track.TrackNumber ?? completedTrackNumber;
        var trackTitle = QobuzTitleFormatter.TrackTitle(track.Title, "Untitled");
        var movedIntoPlace = false;
        var activePartialFilePath = string.Empty;
        try
        {
            yield return new TrackStartedEvent(
                item.Id,
                displayTrackNumber ?? trackNumber,
                displayTotalTracks ?? totalTracks,
                trackTitle);

            var requestedFormatId = jobSettings.FormatId;
            var candidateFormatIds = GetDownloadCandidateFormatIds(jobSettings);
            var plannedTrackQuality = ResolveTrackContext(track, jobSettings).Quality;
            var lastFailureMessage = string.Empty;
            foreach (var candidateFormatId in candidateFormatIds)
            {
                FileUrl? fileUrl = null;
                var candidateFailureMessage = string.Empty;
                try
                {
                    fileUrl = service.GetTrackFileUrl(trackId, candidateFormatId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    candidateFailureMessage = $"{QualityStringMappings.GetCandidateQualityLabel(candidateFormatId)} file URL lookup failed for {trackTitle}: {ex.Message}";
                }

                if (fileUrl is null || string.IsNullOrWhiteSpace(fileUrl.Url))
                {
                    lastFailureMessage = GetFileUrlFailureMessage(trackTitle, candidateFormatId, fileUrl, candidateFailureMessage);
                    continue;
                }

                var stream = ResolveDownloadStream(fileUrl, candidateFormatId);
                var filePath = resolvedFilePathFactory?.Invoke(stream) ??
                    GetTrackFilePath(
                        destination,
                        track,
                        stream.Quality.DisplayQuality,
                        plannedTrackQuality,
                        totalTracks,
                        trackNumber,
                        trackNumberPaddingWidth,
                        stream.Quality.Extension,
                        jobSettings);
                if (ShouldSkipExistingFile(jobSettings, filePath))
                {
                    var existingFileSizeBytes = new FileInfo(filePath).Length;
                    yield return new DownloadInfoEvent(
                        item.Id,
                        $"{trackTitle} skipped because {Path.GetFileName(filePath)} already exists.");
                    yield return new TrackCompletedEvent(item.Id, completedTrackNumber, displayTotalTracks ?? totalTracks, filePath, existingFileSizeBytes);
                    yield break;
                }

                var trackDestination = Path.GetDirectoryName(filePath) ?? destination;
                Directory.CreateDirectory(trackDestination);
                activePartialFilePath = GetPartialFilePath(filePath);

                DownloadFailedEvent? localFileFailure = null;
                var downloadSucceeded = false;
                for (var attempt = 1; attempt <= DownloadStreamMaxAttempts; attempt++)
                {
                    if (attempt > 1)
                    {
                        yield return new DownloadInfoEvent(
                            item.Id,
                            $"Retrying {stream.Quality.DisplayQuality} download for {trackTitle} after a transport failure.");
                    }

                    var downloadFailed = false;
                    var progressEvents = DownloadFileAsync(item.Id, stream.Url, activePartialFilePath, null, cancellationToken)
                        .GetAsyncEnumerator(cancellationToken);

                    try
                    {
                        while (true)
                        {
                            FileProgressEvent progressEvent;
                            try
                            {
                                if (!await progressEvents.MoveNextAsync())
                                {
                                    break;
                                }

                                progressEvent = progressEvents.Current;
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                downloadFailed = true;
                                var localFailure = IsLocalFileFailure(ex);
                                lastFailureMessage = GetDownloadFailureMessage(
                                    stream.Quality.DisplayQuality,
                                    trackTitle,
                                    ex,
                                    !localFailure && attempt < DownloadStreamMaxAttempts);
                                if (localFailure)
                                {
                                    localFileFailure = new DownloadFailedEvent(item.Id, lastFailureMessage, ex);
                                }

                                break;
                            }

                            yield return progressEvent;
                        }
                    }
                    finally
                    {
                        await progressEvents.DisposeAsync();
                    }

                    if (localFileFailure is not null)
                    {
                        break;
                    }

                    if (!downloadFailed)
                    {
                        downloadSucceeded = true;
                        break;
                    }

                    DeletePartialDownloadFile(activePartialFilePath);
                }

                if (localFileFailure is not null)
                {
                    yield return localFileFailure;
                    yield break;
                }

                if (!downloadSucceeded)
                {
                    activePartialFilePath = string.Empty;
                    continue;
                }

                DownloadFailedEvent? taggingFailure = null;
                try
                {
                    if (track.Album is not null)
                    {
                        AudioFileTagger.AddMetadata(activePartialFilePath, track, track.Album, jobSettings, coverArtPath);
                    }
                }
                catch (Exception ex)
                {
                    taggingFailure = new DownloadFailedEvent(item.Id, $"Metadata tagging failed for {trackTitle}: {ex.Message}", ex);
                }

                if (taggingFailure is not null)
                {
                    yield return taggingFailure;
                    yield break;
                }

                var completedFilePath = MoveCompletedDownloadIntoPlace(activePartialFilePath, filePath, jobSettings);
                movedIntoPlace = true;
                if (!string.Equals(completedFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new DownloadInfoEvent(
                        item.Id,
                        $"{trackTitle} saved as {Path.GetFileName(completedFilePath)} because the destination file already exists.");
                }

                if (!string.Equals(candidateFormatId, requestedFormatId, StringComparison.Ordinal))
                {
                    yield return new DownloadWarningEvent(
                        item.Id,
                        GetQualityFallbackWarningMessage(trackTitle, stream));
                }

                var fileSizeBytes = File.Exists(completedFilePath)
                    ? new FileInfo(completedFilePath).Length
                    : (long?)null;

                yield return new TrackCompletedEvent(item.Id, completedTrackNumber, displayTotalTracks ?? totalTracks, completedFilePath, fileSizeBytes);
                yield break;
            }

            yield return new DownloadFailedEvent(
                item.Id,
                string.IsNullOrWhiteSpace(lastFailureMessage)
                    ? $"Qobuz did not return a file URL for {trackTitle}. The track may be unavailable in your region/account or not included in your subscription."
                    : $"{lastFailureMessage} No configured fallback quality succeeded.");
        }
        finally
        {
            if (!movedIntoPlace && !string.IsNullOrWhiteSpace(activePartialFilePath))
            {
                DeletePartialDownloadFile(activePartialFilePath);
            }
        }
    }

    private async Task<bool> DownloadBookletsAsync(
        Album album,
        string destination,
        CancellationToken cancellationToken)
    {
        var booklets = album.Goodies?
            .Where(goody => goody.FileFormatId == 21 && !string.IsNullOrWhiteSpace(goody.Url))
            .ToList();

        if (booklets is not { Count: > 0 })
        {
            return true;
        }

        var completedWithoutErrors = true;
        for (var index = 0; index < booklets.Count; index++)
        {
            var fileName = index == 0 ? "Digital Booklet.pdf" : $"Digital Booklet {index + 1}.pdf";
            var filePath = Path.Combine(destination, fileName);
            if (File.Exists(filePath))
            {
                continue;
            }

            try
            {
                var bytes = await httpClient.GetByteArrayAsync(booklets[index].Url!, cancellationToken);
                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
            }
            catch
            {
                completedWithoutErrors = false;
            }
        }

        return completedWithoutErrors;
    }

    private async IAsyncEnumerable<FileProgressEvent> DownloadFileAsync(
        string queueItemId,
        string url,
        string filePath,
        long? knownTotalBytes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? knownTotalBytes;
        await using var readStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        RecycleFileIfExists(filePath);
        RecycleFileIfExists(filePath + ".part");
        await using var writeStream = File.Create(filePath);

        var buffer = new byte[64 * 1024];
        var bytesReceived = 0L;
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;

        while (true)
        {
            var bytesRead = await ReadWithInactivityTimeoutAsync(readStream, buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await writeStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesReceived += bytesRead;

            if (stopwatch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(250) || bytesReceived == totalBytes)
            {
                lastReport = stopwatch.Elapsed;
                var speed = bytesReceived / 1024d / 1024d / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001d);
                yield return new FileProgressEvent(queueItemId, bytesReceived, totalBytes, speed);
            }
        }
    }

    internal static async ValueTask<int> ReadWithInactivityTimeoutAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        return await ReadWithInactivityTimeoutAsync(
            stream,
            buffer,
            DownloadReadInactivityTimeout,
            cancellationToken);
    }

    internal static async ValueTask<int> ReadWithInactivityTimeoutAsync(
        Stream stream,
        byte[] buffer,
        TimeSpan inactivityTimeout,
        CancellationToken cancellationToken)
    {
        using var inactivityTokenSource = new CancellationTokenSource(inactivityTimeout);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            inactivityTokenSource.Token);

        try
        {
            return await stream.ReadAsync(buffer, linkedTokenSource.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            inactivityTokenSource.IsCancellationRequested)
        {
            throw new IOException(
                $"No download data was received for {inactivityTimeout.TotalSeconds:0.###} seconds.");
        }
    }

    private static string CreateAlbumDestination(
        Album album,
        string artist,
        string albumTitle,
        string quality,
        AppSettings jobSettings)
    {
        return PathTemplateRenderer.RenderAlbumDestination(
            jobSettings.EffectiveDownloadFolder,
            jobSettings.FolderTemplate,
            album,
            artist,
            albumTitle,
            string.IsNullOrWhiteSpace(quality) ? jobSettings.SelectedQuality : quality);
    }

    private static string CreateAlbumDestinationForTrack(Track track, AppSettings jobSettings)
    {
        var context = ResolveTrackContext(track, jobSettings);
        if (context.Album is null)
        {
            return jobSettings.EffectiveDownloadFolder;
        }

        return CreateAlbumDestination(
            context.Album,
            context.Artist,
            context.AlbumTitle,
            context.Quality,
            jobSettings);
    }

    private static string CreatePlaylistDestination(
        Track sampleTrack,
        string playlistId,
        string playlistTitle,
        string playlistOwner,
        int playlistTotalTracks,
        AppSettings jobSettings)
    {
        var context = ResolveTrackContext(sampleTrack, jobSettings);

        return PathTemplateRenderer.RenderPlaylistDestination(
            jobSettings.EffectiveDownloadFolder,
            jobSettings.PlaylistFolderTemplate,
            playlistId,
            playlistTitle,
            playlistOwner,
            sampleTrack,
            context.Album,
            context.Artist,
            context.AlbumTitle,
            context.Quality,
            playlistNumber: 1,
            playlistTotalTracks);
    }

    private static string GetTrackDestination(
        string albumDestination,
        Track track,
        string trackQuality,
        int totalTracks,
        AppSettings jobSettings)
    {
        if (track.Album?.MediaCount is not > 1)
        {
            return albumDestination;
        }

        var context = ResolveTrackContext(track, jobSettings);
        if (context.Album is null)
        {
            return albumDestination;
        }

        var discFolders = PathTemplateRenderer.RenderDiscFolderSegments(
            jobSettings.DiscFolderTemplate,
            jobSettings.DiscWorkHandling,
            jobSettings.DiscWorkSeparator,
            jobSettings.DiscWorkSeparatorNoSpaces,
            track,
            context.Album,
            context.Artist,
            context.AlbumTitle,
            string.IsNullOrWhiteSpace(trackQuality) ? jobSettings.SelectedQuality : trackQuality,
            totalTracks);

        return discFolders.Count == 0
            ? albumDestination
            : Path.Combine([albumDestination, .. discFolders]);
    }

    private static string GetTrackFilePath(
        string albumDestination,
        Track track,
        string trackQuality,
        string folderQuality,
        int totalTracks,
        int fallbackTrackNumber,
        int? trackNumberPaddingWidth,
        string extension,
        AppSettings jobSettings)
    {
        var trackNumber = track.TrackNumber ?? fallbackTrackNumber;
        var trackTitle = QobuzTitleFormatter.TrackTitle(track.Title, "Untitled");
        var context = ResolveTrackContext(track, jobSettings);
        var fileName = track.Album is null
            ? StringTools.GetSafeFilename($"{trackNumber:00} - {trackTitle}") + extension
            : PathTemplateRenderer.RenderAudioFilename(
                jobSettings.FilenameTemplate,
                track,
                context.Album!,
                context.Artist,
                context.AlbumTitle,
                string.IsNullOrWhiteSpace(trackQuality) ? jobSettings.SelectedQuality : trackQuality,
                totalTracks,
                extension,
                jobSettings.MaxFileNameLength,
                trackNumberPaddingWidth);
        var trackDestination = GetTrackDestination(albumDestination, track, folderQuality, totalTracks, jobSettings);
        return Path.Combine(trackDestination, fileName);
    }

    private static IReadOnlyList<string> CreateFavoriteAlbumPreviewPaths(
        IReadOnlyList<FavoriteAlbumPlan> plans,
        AppSettings jobSettings)
    {
        var paths = new List<string>(FavoritesPreviewSamplePathCount);
        foreach (var plan in plans)
        {
            foreach (var track in plan.Tracks)
            {
                paths.Add(GetTrackFilePath(
                    plan.Destination,
                    track,
                    plan.Quality,
                    plan.Quality,
                    plan.Tracks.Count,
                    track.TrackNumber ?? 0,
                    PathTemplateRenderer.GetTrackNumberPaddingWidth(
                        plan.TrackNumberScopes,
                        track.MediaNumber ?? 0,
                        jobSettings.DiscFolderTemplate),
                    QualityStringMappings.GetAudioExtension(jobSettings.FormatId),
                    jobSettings));

                if (paths.Count >= FavoritesPreviewSamplePathCount)
                {
                    return paths;
                }
            }
        }

        return paths;
    }

    private static IReadOnlyList<string> CreateFavoriteTrackPreviewPaths(
        IReadOnlyList<FavoriteTrackPlan> plans,
        AppSettings jobSettings)
    {
        return plans
            .Take(FavoritesPreviewSamplePathCount)
            .Select(plan => GetTrackFilePath(
                plan.Destination,
                plan.Track,
                plan.Quality,
                plan.Quality,
                GetAlbumTotalTracks(plan.Track),
                plan.Track.TrackNumber ?? 0,
                trackNumberPaddingWidth: null,
                QualityStringMappings.GetAudioExtension(jobSettings.FormatId),
                jobSettings))
            .ToList();
    }

    internal static string GetStandardPlaylistTrackFilePath(Track track, AppSettings jobSettings)
    {
        var albumDestination = CreateAlbumDestinationForTrack(track, jobSettings);
        var albumTotalTracks = GetAlbumTotalTracks(track);
        var context = ResolveTrackContext(track, jobSettings);

        return GetTrackFilePath(
            albumDestination,
            track,
            context.Quality,
            context.Quality,
            albumTotalTracks,
            track.TrackNumber ?? 0,
            trackNumberPaddingWidth: null,
            extension: QualityStringMappings.GetAudioExtension(jobSettings.FormatId),
            jobSettings: jobSettings);
    }

    internal static string GetPlaylistTrackFilePath(
        string playlistDestination,
        Track track,
        string playlistId,
        string playlistTitle,
        string playlistOwner,
        int playlistNumber,
        int playlistTotalTracks,
        AppSettings jobSettings,
        string? trackQualityOverride = null,
        string? extensionOverride = null)
    {
        var context = ResolveTrackContext(track, jobSettings);
        var album = context.Album ?? throw new InvalidOperationException("Playlist track album context is missing.");
        var fileName = PathTemplateRenderer.RenderPlaylistAudioFilename(
            jobSettings.PlaylistFilenameTemplate,
            track,
            album,
            context.Artist,
            context.AlbumTitle,
            string.IsNullOrWhiteSpace(trackQualityOverride) ? context.Quality : trackQualityOverride,
            playlistId,
            playlistTitle,
            playlistOwner,
            playlistNumber,
            playlistTotalTracks,
            string.IsNullOrWhiteSpace(extensionOverride)
                ? QualityStringMappings.GetAudioExtension(jobSettings.FormatId)
                : extensionOverride,
            jobSettings.MaxFileNameLength);

        return Path.Combine(playlistDestination, fileName);
    }

    private static (Album? Album, string Artist, string AlbumTitle, string Quality) ResolveTrackContext(
        Track track,
        AppSettings jobSettings)
    {
        var album = track.Album;
        var artist = album?.Artist?.Name ?? track.Performer?.Name ?? string.Empty;
        var albumTitle = QobuzTitleFormatter.AlbumTitle(album?.Title, "Untitled");
        var quality = album is null
            ? jobSettings.SelectedQuality
            : QualityStringMappings.GetEffectiveQuality(jobSettings.FormatId, album).DisplayQuality;

        return (album, artist, albumTitle, quality);
    }

    private static IReadOnlyList<(int TrackNumber, int DiscNumber)> GetTrackNumberScopes(IReadOnlyList<Track> tracks)
    {
        return tracks
            .Select(track => (track.TrackNumber ?? 0, track.MediaNumber ?? 0))
            .ToList();
    }

    private static (Playlist Playlist, IReadOnlyList<Track> Tracks) FetchPlaylistTracks(
        QobuzApiService service,
        string playlistId)
    {
        return FetchPlaylistTracks((limit, offset) => service.GetPlaylist(playlistId, true, "tracks", limit, offset));
    }

    private static IReadOnlyList<Track> SelectPlaylistTracks(
        IReadOnlyList<Track> playlistTracks,
        IReadOnlyList<string> selectionKeys)
    {
        if (selectionKeys.Count == 0)
        {
            return playlistTracks;
        }

        var indexedTracks = playlistTracks
            .Select((track, index) => (Track: track, Index: index))
            .ToList();
        var selectedTracks = new List<Track>();

        foreach (var selectionKey in selectionKeys)
        {
            var matchIndex = indexedTracks.FindIndex(item =>
                PlaylistTrackSelectionKey.Matches(item.Track, item.Index, selectionKey));
            if (matchIndex < 0)
            {
                continue;
            }

            selectedTracks.Add(indexedTracks[matchIndex].Track);
            indexedTracks.RemoveAt(matchIndex);
        }

        return selectedTracks;
    }

    internal static (Playlist Playlist, IReadOnlyList<Track> Tracks) FetchPlaylistTracks(
        Func<int, int, Playlist> getPlaylistPage)
    {
        var result = QobuzPagination.FetchAll(
            QobuzApiLimits.PlaylistTrackPageSize,
            getPlaylistPage,
            playlist => playlist.Tracks?.Items,
            playlist => playlist.TracksCount ?? playlist.Tracks?.Total);

        return (result.FirstPage, result.Items);
    }

    private static IReadOnlyList<string> FetchFavoriteAlbumIds(QobuzApiService service)
    {
        return FetchFavoriteIds(
            (limit, offset) => service.GetUserFavoriteIds(string.Empty, limit, offset),
            favorites => favorites.Albums ?? []);
    }

    private static IReadOnlyList<int> FetchFavoriteTrackIds(QobuzApiService service)
    {
        return FetchFavoriteIds(
            (limit, offset) => service.GetUserFavoriteIds(string.Empty, limit, offset),
            favorites => favorites.Tracks ?? []);
    }

    internal static IReadOnlyList<T> FetchFavoriteIds<T>(
        Func<int, int, UserFavoritesIds> getFavoriteIdsPage,
        Func<UserFavoritesIds, IReadOnlyList<T>> selectIds)
    {
        return QobuzPagination.FetchAll(
            QobuzApiLimits.FavoriteIdPageSize,
            getFavoriteIdsPage,
            selectIds).Items;
    }

    internal static int GetPlaylistNumber(Track track, int returnedIndex)
    {
        return track.PlaylistPosition is > 0
            ? track.PlaylistPosition.Value
            : returnedIndex + 1;
    }

    internal static int GetAlbumTotalTracks(Track track)
    {
        return Math.Max(1, track.Album?.TracksCount ?? track.TrackNumber ?? 1);
    }

    internal static bool IsKeepPlaylistTogether(AppSettings jobSettings)
    {
        return !string.Equals(
            jobSettings.PlaylistOrganization,
            AppSettings.UseStandardTemplatesPlaylistOrganization,
            StringComparison.Ordinal);
    }

    internal static bool ShouldSaveStandardFolderCover(AppSettings jobSettings)
    {
        return !string.IsNullOrWhiteSpace(jobSettings.FolderTemplate);
    }

    internal static IReadOnlyList<string> GetDownloadCandidateFormatIds(AppSettings jobSettings)
    {
        if (jobSettings.FormatId == QualityStringMappings.Mp3FormatId)
        {
            return [QualityStringMappings.Mp3FormatId];
        }

        if (jobSettings.FallbackToMp3IfFlacUnavailable)
        {
            return
            [
                QualityStringMappings.FlacHighestFormatId,
                QualityStringMappings.FlacHiResFormatId,
                QualityStringMappings.FlacCdFormatId,
                QualityStringMappings.Mp3FormatId
            ];
        }

        return
        [
            QualityStringMappings.FlacHighestFormatId,
            QualityStringMappings.FlacHiResFormatId,
            QualityStringMappings.FlacCdFormatId
        ];
    }

    internal static ResolvedDownloadStream ResolveDownloadStream(
        FileUrl fileUrl,
        string candidateFormatId)
    {
        return new ResolvedDownloadStream(
            fileUrl.Url,
            QualityStringMappings.GetActualQuality(fileUrl, candidateFormatId));
    }

    internal static string GetQualityFallbackWarningMessage(string trackTitle, ResolvedDownloadStream stream)
    {
        return stream.Quality.FormatId == QualityStringMappings.Mp3FormatId
            ? $"{trackTitle} fell back to {stream.Quality.DisplayQuality} after no FLAC stream succeeded."
            : $"{trackTitle} quality was reduced to {stream.Quality.DisplayQuality} after the requested FLAC stream failed.";
    }

    internal static string GetDownloadFailureMessage(
        string quality,
        string trackTitle,
        Exception exception,
        bool willRetry)
    {
        var retryText = willRetry
            ? " The download will be retried."
            : string.Empty;

        return $"{quality} download failed for {trackTitle}: {exception.Message}{retryText}";
    }

    internal static string? FirstNonEmptyPlaylistImage(Playlist playlist)
    {
        return PlaylistImageUrlSelector.GetBestImageUrl(playlist);
    }

    private static string GetFileUrlFailureMessage(
        string trackTitle,
        string candidateFormatId,
        FileUrl? fileUrl,
        string lastFailureMessage)
    {
        if (!string.IsNullOrWhiteSpace(fileUrl?.Message))
        {
            var code = string.IsNullOrWhiteSpace(fileUrl.Code) ? string.Empty : $" ({fileUrl.Code})";
            return $"{QualityStringMappings.GetCandidateQualityLabel(candidateFormatId)} file URL lookup failed for {trackTitle}: {fileUrl.Message}{code}";
        }

        return string.IsNullOrWhiteSpace(lastFailureMessage)
            ? $"Qobuz did not return a {QualityStringMappings.GetCandidateQualityLabel(candidateFormatId)} file URL for {trackTitle}."
            : lastFailureMessage;
    }

    internal static bool IsLocalFileFailure(Exception exception)
    {
        return exception is PathTooLongException or UnauthorizedAccessException or DirectoryNotFoundException or NotSupportedException or ArgumentException;
    }

    private static string GetPartialFilePath(string finalFilePath)
    {
        var extension = Path.GetExtension(finalFilePath);
        return string.IsNullOrEmpty(extension)
            ? finalFilePath + ".part"
            : Path.Combine(
                Path.GetDirectoryName(finalFilePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(finalFilePath) + ".part" + extension);
    }

    private static bool ShouldSkipExistingFile(AppSettings jobSettings, string finalFilePath)
    {
        return string.Equals(jobSettings.DuplicateFileBehavior, AppSettings.DuplicateFileSkip, StringComparison.Ordinal) &&
            File.Exists(finalFilePath);
    }

    private static string MoveCompletedDownloadIntoPlace(
        string partialFilePath,
        string finalFilePath,
        AppSettings jobSettings)
    {
        if (string.Equals(jobSettings.DuplicateFileBehavior, AppSettings.DuplicateFileKeepBoth, StringComparison.Ordinal))
        {
            var duplicateFilePath = GetAvailableDuplicateFilePath(finalFilePath);
            File.Move(partialFilePath, duplicateFilePath);
            return duplicateFilePath;
        }

        RecycleFileIfExists(finalFilePath);
        File.Move(partialFilePath, finalFilePath);
        return finalFilePath;
    }

    private static string GetAvailableDuplicateFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        for (var copyNumber = 1; copyNumber < int.MaxValue; copyNumber++)
        {
            var candidate = Path.Combine(directory, $"{fileName} ({copyNumber}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find an available duplicate filename for {filePath}.");
    }

    private static void RecycleFileIfExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        FileSystem.DeleteFile(
            filePath,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin);
    }

    private static void DeletePartialDownloadFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // A failed cleanup should not mask the original download failure.
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }


}
