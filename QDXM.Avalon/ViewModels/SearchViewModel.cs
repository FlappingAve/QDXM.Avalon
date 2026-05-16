using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Exceptions;
using QDXM.Avalon.Services;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Search;
using QDXM.Avalon.Core.Settings;

namespace QDXM.Avalon.ViewModels;

public delegate void SearchDownloadQueued(DownloadRequest request, SearchResultViewModel result);

public partial class SearchViewModel : ViewModelBase
{
    private const string AlbumsType = "Albums";
    private const string TracksType = "Tracks";
    private const string PlaylistsType = "Playlists";
    private const string ArtistType = "Artist";
    private const string ArtistAlbumsType = "Artist Albums";
    private const string LabelType = "Label";
    private const string LabelAlbumsType = "Label Albums";
    private const string NewestishSort = "Newest-ish";
    private const string RelevanceSort = "Relevance";
    private const string UnarrangedArrange = "Unarranged";
    private const string ReleaseDateArrange = "Release Date";
    private const string QualityArrange = "Quality";
    private const string NameArrange = "Name";
    private const string TotalTracksArrange = "Track Count";
    private const string TotalAlbumsArrange = "Album Count";
    private static readonly TimeSpan ReleasedSearchMemoryCollectionDelay = TimeSpan.FromSeconds(2);

    private IQobuzClient? qobuzClient;
    private readonly AppLogService logService;
    private readonly SearchDownloadQueued? enqueueDownload;
    private readonly Action<PartialAlbumDownloadRequest>? enqueuePartialAlbum;
    private readonly Action<PartialPlaylistDownloadRequest>? enqueuePartialPlaylist;
    private readonly AppSettings settings;
    private readonly SearchResultFactory resultFactory;
    private readonly ISearchMemoryCleanupScheduler memoryCleanupScheduler;
    private readonly Action collectReleasedSearchMemory;
    private readonly List<SearchResultViewModel> loadedResults = [];
    private readonly RemoteImageCache searchImageCache = new(
        retainDecodedImages: false,
        cacheCompressedImagesOnDisk: true);
    private CancellationTokenSource? activeSearchCancellationSource;
    private int nextOffset;
    private int releasedMemoryCollectionVersion;
    private long searchOperationVersion;

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholderText))]
    [NotifyPropertyChangedFor(nameof(IsArtistAlbumSortVisible))]
    private string selectedType = AlbumsType;

    [ObservableProperty]
    private string selectedSort = NewestishSort;

    [ObservableProperty]
    private SearchArrangeOptionView? selectedArrangeBy = CreateArrangeOption(SearchArrangeOption.Unarranged);

    [ObservableProperty]
    private int selectedLimit = 25;

    [ObservableProperty]
    private IReadOnlyList<SearchArrangeOptionView> arrangeByOptions = [];

    [ObservableProperty]
    private IReadOnlyList<int> limitOptions = [];

    [ObservableProperty]
    private string statusText = "Search ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedResult))]
    [NotifyPropertyChangedFor(nameof(DestinationPreview))]
    private SearchResultViewModel? selectedResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    private bool isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    private bool hasSearched;

    public IReadOnlyList<string> TypeOptions { get; } =
    [
        AlbumsType,
        TracksType,
        PlaylistsType,
        ArtistType,
        ArtistAlbumsType,
        LabelType,
        LabelAlbumsType
    ];
    public IReadOnlyList<string> SortOptions { get; } = [NewestishSort, RelevanceSort];
    public ObservableCollection<SearchResultViewModel> Results { get; } = [];

    public string ResultCountText => $"{Results.Count} results";
    public bool HasSelectedResult => SelectedResult is not null;
    public bool HasResults => Results.Count > 0;
    public bool ShowNoResults => HasSearched && !IsSearching && Results.Count == 0;
    public bool IsArtistAlbumSortVisible => SelectedType == ArtistAlbumsType;
    public string SearchPlaceholderText => SelectedType switch
    {
        ArtistType => "Search an artist on Qobuz",
        LabelType => "Search a label on Qobuz",
        ArtistAlbumsType => "Enter artist id, e.g. 12345 or id:12345",
        LabelAlbumsType => "Enter label id, e.g. 12345 or id:12345",
        TracksType => "Search tracks on Qobuz or id:12345",
        PlaylistsType => "Search playlists on Qobuz",
        _ => "Search Qobuz or id:12345"
    };
    public string DestinationPreview => SelectedResult is null
        ? string.Empty
        : DestinationPreviewRenderer.ForSearchResult(SelectedResult, settings);

    public void CollapseExpandedResultsExcept(SearchResultViewModel resultToKeep)
    {
        var collapsedSelectedResult = false;

        foreach (var result in Results)
        {
            if (ReferenceEquals(result, resultToKeep) || !result.IsExpanded)
            {
                continue;
            }

            result.IsExpanded = false;
            result.NotifyTrackSelectionChanged();
            collapsedSelectedResult |= ReferenceEquals(result, SelectedResult);
        }

        if (collapsedSelectedResult)
        {
            OnPropertyChanged(nameof(DestinationPreview));
        }
    }

    public SearchViewModel()
        : this(null, null, null, null, new AppLogService(), new AppSettings())
    {
    }

    public SearchViewModel(
        IQobuzClient? qobuzClient,
        SearchDownloadQueued? enqueueDownload,
        Action<PartialAlbumDownloadRequest>? enqueuePartialAlbum,
        Action<PartialPlaylistDownloadRequest>? enqueuePartialPlaylist = null,
        AppLogService? logService = null,
        AppSettings? settings = null,
        ISearchMemoryCleanupScheduler? memoryCleanupScheduler = null,
        Action? collectReleasedSearchMemory = null)
    {
        this.qobuzClient = qobuzClient;
        this.enqueueDownload = enqueueDownload;
        this.enqueuePartialAlbum = enqueuePartialAlbum;
        this.enqueuePartialPlaylist = enqueuePartialPlaylist;
        this.logService = logService ?? new AppLogService();
        this.settings = settings ?? new AppSettings();
        this.memoryCleanupScheduler = memoryCleanupScheduler ?? new BackgroundSearchMemoryCleanupScheduler();
        this.collectReleasedSearchMemory = collectReleasedSearchMemory ?? CollectReleasedSearchMemory;
        resultFactory = new SearchResultFactory(
            OnResultActionCompleted,
            LoadAlbumTracksAsync,
            LoadPlaylistTracksAsync,
            LoadPlaylistTrackPageAsync,
            DownloadPrimaryAsync,
            DownloadSelectedAsync,
            SearchEntityAlbumsAsync,
            HandleTrackSelectionChanged,
            searchImageCache);

        UpdateLimitOptions();
        UpdateArrangeByOptions();
    }

    public void SetQobuzClient(IQobuzClient? client)
    {
        if (!ReferenceEquals(qobuzClient, client) && qobuzClient is IDisposable disposable)
        {
            disposable.Dispose();
        }

        qobuzClient = client;
        StatusText = "Search ready";
    }

    public void RefreshSettingsPreview() => OnPropertyChanged(nameof(DestinationPreview));

    partial void OnSelectedArrangeByChanged(SearchArrangeOptionView? value)
    {
        RefreshDisplayedResults();
    }

    partial void OnSelectedResultChanged(SearchResultViewModel? oldValue, SearchResultViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        oldValue?.ReleaseThumbnailIfNotVisible();
        newValue?.EnsureThumbnailLoaded();
    }

    partial void OnSelectedTypeChanged(string value)
    {
        CancelActiveSearch();
        OnPropertyChanged(nameof(IsArtistAlbumSortVisible));
        var resultType = ToSearchResultType();
        var clampedLimit = SearchPageSizeOptions.ClampLimit(resultType, SelectedLimit);
        if (SelectedLimit != clampedLimit)
        {
            SelectedLimit = clampedLimit;
        }

        UpdateLimitOptions();
        ClearResultsForTypeChange();
        UpdateArrangeByOptions();
    }

    private void ClearResultsForTypeChange()
    {
        if (!ClearCurrentResultsAndScheduleMemoryCollection())
        {
            return;
        }

        HasSearched = false;
        nextOffset = 0;
        StatusText = "Search ready";
        NotifyResultCountChanged();
    }

    [RelayCommand]
    private async Task Search()
    {
        nextOffset = 0;
        await SearchAsync(append: false);
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        await SearchAsync(append: true);
    }

    private async Task SearchAsync(bool append)
    {
        if (qobuzClient is null)
        {
            ClearCurrentResultsAndScheduleMemoryCollection();
            HasSearched = false;
            StatusText = "Qobuz search is not configured. Log in first.";
            logService.Warning("Search", "Search attempted before Qobuz client was configured.");
            NotifyResultCountChanged();
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            HasSearched = false;
            StatusText = "Enter a search term";
            return;
        }

        var (operationVersion, cancellationSource) = BeginSearchOperation();
        var cancellationToken = cancellationSource.Token;

        IsSearching = true;
        StatusText = append ? "Loading more results..." : "Searching Qobuz...";

        try
        {
            var options = new SearchQueryOptions(
                Query.Trim(),
                ToSearchResultType(),
                ToArtistAlbumSortOption(),
                ToEffectiveLimit(),
                nextOffset);

            if (!append)
            {
                ClearCurrentResultsAndScheduleMemoryCollection();
                HasSearched = false;
            }

            if (options.Type is SearchResultType.Albums)
            {
                var albums = SearchQueryClassifier.TryGetDirectAlbumId(options, out var albumId)
                    ? [await qobuzClient.GetAlbumTracksAsync(albumId, cancellationToken)]
                    : await qobuzClient.SearchAlbumsAsync(options, cancellationToken);

                ThrowIfSearchStale(operationVersion, cancellationToken);
                AppendLoadedPage(albums.Select(resultFactory.CreateAlbum));
            }

            else if (options.Type is SearchResultType.Tracks)
            {
                var tracks = SearchQueryClassifier.TryGetDirectTrackId(options, out var trackId)
                    ? [await qobuzClient.GetTrackAsync(trackId, cancellationToken)]
                    : await qobuzClient.SearchTracksAsync(options, cancellationToken);
                ThrowIfSearchStale(operationVersion, cancellationToken);
                AppendLoadedPage(tracks.Select(resultFactory.CreateTrack));
            }
            else if (options.Type is SearchResultType.Playlists)
            {
                var playlists = await qobuzClient.SearchPlaylistsAsync(options, cancellationToken);
                ThrowIfSearchStale(operationVersion, cancellationToken);
                AppendLoadedPage(playlists.Select(resultFactory.CreatePlaylist));
            }
            else if (options.Type is SearchResultType.Artist)
            {
                var artists = await qobuzClient.SearchArtistsAsync(options, cancellationToken);
                ThrowIfSearchStale(operationVersion, cancellationToken);
                AppendLoadedPage(artists.Select(resultFactory.CreateArtist));
            }
            else if (options.Type is SearchResultType.ArtistAlbums)
            {
                if (!SearchQueryClassifier.TryGetNumericId(options, out _))
                {
                    StatusText = "Enter an artist id";
                    NotifyResultCountChanged();
                    return;
                }

                if (ToArrangeOption() is SearchArrangeOption.Unarranged)
                {
                    await foreach (var album in qobuzClient.SearchArtistAlbumsIncrementalAsync(options, cancellationToken).WithCancellation(cancellationToken))
                    {
                        ThrowIfSearchStale(operationVersion, cancellationToken);
                        AddLoadedResult(resultFactory.CreateAlbum(album));
                        StatusText = $"{Results.Count} results";
                        NotifyResultCountChanged();
                    }
                }
                else
                {
                    var pageResults = new List<SearchResultViewModel>();
                    await foreach (var album in qobuzClient.SearchArtistAlbumsIncrementalAsync(options, cancellationToken).WithCancellation(cancellationToken))
                    {
                        ThrowIfSearchStale(operationVersion, cancellationToken);
                        pageResults.Add(resultFactory.CreateAlbum(album));
                    }

                    ThrowIfSearchStale(operationVersion, cancellationToken);
                    AppendLoadedPage(pageResults);
                }
            }
            else if (options.Type is SearchResultType.Label)
            {
                var labels = await qobuzClient.SearchLabelsAsync(options, cancellationToken);
                ThrowIfSearchStale(operationVersion, cancellationToken);
                AppendLoadedPage(labels.Select(resultFactory.CreateLabel));
            }
            else if (options.Type is SearchResultType.LabelAlbums)
            {
                if (!SearchQueryClassifier.TryGetNumericId(options, out _))
                {
                    StatusText = "Enter a label id";
                    NotifyResultCountChanged();
                    return;
                }

                var albums = await qobuzClient.SearchLabelAlbumsAsync(options, cancellationToken);
                ThrowIfSearchStale(operationVersion, cancellationToken);
                AppendLoadedPage(albums.Select(resultFactory.CreateAlbum));
            }

            ThrowIfSearchStale(operationVersion, cancellationToken);
            nextOffset += options.NormalizedLimit;
            SelectedResult ??= Results.FirstOrDefault();
            HasSearched = true;
            StatusText = $"{Results.Count} results";
            NotifyResultCountChanged();
        }
        catch (ApiErrorResponseException ex)
        {
            StatusText = SafeErrorText.FormatApiFailure("Search", ex);
            logService.Error("Search", StatusText);
        }
        catch (OperationCanceledException) when (!IsSearchCurrent(operationVersion, cancellationToken))
        {
        }
        catch (Exception ex)
        {
            StatusText = SafeErrorText.FormatUnexpectedFailure("Search");
            logService.Error("Search", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
        finally
        {
            if (operationVersion == searchOperationVersion)
            {
                IsSearching = false;
            }

            if (ReferenceEquals(activeSearchCancellationSource, cancellationSource))
            {
                activeSearchCancellationSource = null;
            }

            cancellationSource.Dispose();
        }
    }

    private (long OperationVersion, CancellationTokenSource CancellationSource) BeginSearchOperation()
    {
        activeSearchCancellationSource?.Cancel();

        var cancellationSource = new CancellationTokenSource();
        activeSearchCancellationSource = cancellationSource;
        var operationVersion = ++searchOperationVersion;

        return (operationVersion, cancellationSource);
    }

    private void CancelActiveSearch()
    {
        activeSearchCancellationSource?.Cancel();
        searchOperationVersion++;
        IsSearching = false;
    }

    private bool IsSearchCurrent(long operationVersion, CancellationToken cancellationToken)
    {
        return operationVersion == searchOperationVersion && !cancellationToken.IsCancellationRequested;
    }

    private void ThrowIfSearchStale(long operationVersion, CancellationToken cancellationToken)
    {
        if (!IsSearchCurrent(operationVersion, cancellationToken))
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private void AddLoadedResult(SearchResultViewModel result)
    {
        loadedResults.Add(result);
        Results.Add(result);
        SelectedResult ??= result;
    }

    private void AppendLoadedPage(IEnumerable<SearchResultViewModel> pageResults)
    {
        var page = pageResults.ToList();
        loadedResults.AddRange(page);

        foreach (var result in SearchResultViewArranger.Arrange(page, ToArrangeOption()))
        {
            Results.Add(result);
        }

        SelectedResult ??= Results.FirstOrDefault();
    }

    private void RefreshDisplayedResults()
    {
        if (loadedResults.Count == 0)
        {
            return;
        }

        var selected = SelectedResult;
        var arranged = SearchResultViewArranger.Arrange(loadedResults, ToArrangeOption());

        Results.Clear();
        foreach (var result in arranged)
        {
            Results.Add(result);
        }

        if (selected is not null && Results.Contains(selected))
        {
            SelectedResult = selected;
        }
        else
        {
            SelectedResult = Results.FirstOrDefault();
        }

        NotifyResultCountChanged();
    }

    private void NotifyResultCountChanged()
    {
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private bool ClearCurrentResultsAndScheduleMemoryCollection()
    {
        var hadResults = ClearCurrentResults();
        if (hadResults)
        {
            ScheduleReleasedSearchMemoryCollection();
        }

        return hadResults;
    }

    private bool ClearCurrentResults()
    {
        var hadResults = Results.Count > 0 || loadedResults.Count > 0 || SelectedResult is not null;
        ReleaseCurrentResultMemory();
        Results.Clear();
        loadedResults.Clear();
        SelectedResult = null;
        NotifyResultCountChanged();
        return hadResults;
    }

    private void ReleaseCurrentResultMemory()
    {
        var released = new HashSet<SearchResultViewModel>();
        foreach (var result in loadedResults)
        {
            if (released.Add(result))
            {
                result.ReleaseHeavyState();
            }
        }

        foreach (var result in Results)
        {
            if (released.Add(result))
            {
                result.ReleaseHeavyState();
            }
        }

        searchImageCache.Clear();
    }

    private void ScheduleReleasedSearchMemoryCollection()
    {
        var version = Interlocked.Increment(ref releasedMemoryCollectionVersion);
        memoryCleanupScheduler.Schedule(
            ReleasedSearchMemoryCollectionDelay,
            () => version == Volatile.Read(ref releasedMemoryCollectionVersion),
            collectReleasedSearchMemory);
    }

    private static void CollectReleasedSearchMemory()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
    }

    private async Task LoadAlbumTracksAsync(SearchResultViewModel result)
    {
        if (qobuzClient is null)
        {
            return;
        }

        try
        {
            var album = await qobuzClient.GetAlbumTracksAsync(result.Id);
            result.Tracks.Clear();

            foreach (var track in album.Tracks)
            {
                result.Tracks.Add(resultFactory.CreateTrackSelection(track, result, isSelected: true));
            }

            result.TotalDiscs = album.TotalDiscs;
            result.NotifyAlbumTrackRowsChanged();
            OnResultActionCompleted(result, $"{result.Tracks.Count} tracks loaded");
        }
        catch (Exception ex)
        {
            OnResultActionCompleted(result, SafeErrorText.FormatUnexpectedFailure("Load tracks"));
            logService.Error("Search", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
    }

    private Task LoadPlaylistTracksAsync(SearchResultViewModel result)
    {
        return LoadPlaylistTrackPageAsync(result, pageIndex: 0);
    }

    private async Task LoadPlaylistTrackPageAsync(SearchResultViewModel result, int pageIndex)
    {
        if (qobuzClient is null || string.IsNullOrWhiteSpace(result.Id))
        {
            return;
        }

        if (result.TryShowCachedTrackPage(pageIndex))
        {
            OnResultActionCompleted(result, result.TrackRangeText);
            return;
        }

        try
        {
            var visiblePageSize = result.TrackPageSize;
            var requestedTrackOffset = pageIndex * visiblePageSize;
            var chunkOffset = requestedTrackOffset / QobuzApiLimits.PlaylistTrackPageSize * QobuzApiLimits.PlaylistTrackPageSize;
            var firstChunkPageIndex = chunkOffset / visiblePageSize;
            var page = await qobuzClient.GetPlaylistTracksAsync(
                result.Id,
                QobuzApiLimits.PlaylistTrackPageSize,
                chunkOffset);
            result.UpdateTrackTotal(page.TotalTracks);
            var tracks = page.Tracks
                .Select(track => resultFactory.CreatePlaylistTrackSelection(track, result, isSelected: false))
                .ToList();
            var visiblePages = tracks
                .Chunk(visiblePageSize)
                .Select(chunk => (IReadOnlyList<AlbumTrackSelectionViewModel>)chunk.ToList())
                .ToList();

            result.SetTrackPages(firstChunkPageIndex, visiblePages, pageIndex);
            OnResultActionCompleted(result, result.TrackRangeText);
        }
        catch (Exception ex)
        {
            OnResultActionCompleted(result, SafeErrorText.FormatUnexpectedFailure("Load playlist tracks"));
            logService.Error("Search", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
    }

    private async Task SearchEntityAlbumsAsync(SearchResultViewModel result)
    {
        SelectedType = result.IsArtist ? ArtistAlbumsType : LabelAlbumsType;
        Query = result.Id;
        SelectedResult = result;
        nextOffset = 0;
        await SearchAsync(append: false);
    }

    private Task DownloadPrimaryAsync(SearchResultViewModel result)
    {
        if (string.IsNullOrWhiteSpace(result.Id))
        {
            OnResultActionCompleted(result, "Could not create a Qobuz download URL");
            return Task.CompletedTask;
        }

        var contentType = result.IsAlbum
            ? DownloadContentType.Album
            : result.IsPlaylist
                ? DownloadContentType.Playlist
                : DownloadContentType.Track;
        var request = new DownloadRequest(
            GetSearchSourceUrl(result, contentType),
            contentType,
            result.Id);

        enqueueDownload?.Invoke(request, result);
        OnResultActionCompleted(result, result.IsAlbum ? "Album queued" : result.IsPlaylist ? "Playlist queued" : "Track queued");
        return Task.CompletedTask;
    }

    private Task DownloadSelectedAsync(SearchResultViewModel result)
    {
        var trackIds = result.SelectedTrackSelectionKeys;

        if (trackIds.Count == 0)
        {
            OnResultActionCompleted(result, "Select at least one track");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(result.Id))
        {
            OnResultActionCompleted(result, "Could not create a Qobuz download URL");
            return Task.CompletedTask;
        }

        if (result.IsPlaylist)
        {
            enqueuePartialPlaylist?.Invoke(new PartialPlaylistDownloadRequest(
                result.Id,
                GetSearchSourceUrl(result, DownloadContentType.Playlist),
                trackIds,
                result.Title,
                result.Artist,
                result.ThumbnailUrl));

            OnResultActionCompleted(result, $"{trackIds.Count} selected playlist tracks queued");
            return Task.CompletedTask;
        }

        enqueuePartialAlbum?.Invoke(new PartialAlbumDownloadRequest(
            result.Id,
            GetSearchSourceUrl(result, DownloadContentType.Album),
            trackIds,
            result.Title,
            result.Artist));

        OnResultActionCompleted(result, $"{trackIds.Count} selected tracks queued");
        return Task.CompletedTask;
    }

    private SearchResultType ToSearchResultType()
    {
        return SelectedType switch
        {
            AlbumsType => SearchResultType.Albums,
            TracksType => SearchResultType.Tracks,
            PlaylistsType => SearchResultType.Playlists,
            ArtistType => SearchResultType.Artist,
            ArtistAlbumsType => SearchResultType.ArtistAlbums,
            LabelType => SearchResultType.Label,
            LabelAlbumsType => SearchResultType.LabelAlbums,
            _ => SearchResultType.Albums
        };
    }

    private SearchArtistAlbumSortOption ToArtistAlbumSortOption()
    {
        return SelectedSort == RelevanceSort
            ? SearchArtistAlbumSortOption.Relevance
            : SearchArtistAlbumSortOption.Newestish;
    }

    private SearchArrangeOption ToArrangeOption()
    {
        return SelectedArrangeBy?.Value ?? SearchArrangeOption.Unarranged;
    }

    private int ToEffectiveLimit()
    {
        return SearchPageSizeOptions.ClampLimit(ToSearchResultType(), SelectedLimit);
    }

    private void UpdateLimitOptions()
    {
        LimitOptions = SearchPageSizeOptions.ForType(ToSearchResultType());
    }

    private void UpdateArrangeByOptions()
    {
        var currentArrangeBy = ToArrangeOption();
        ArrangeByOptions = SearchArrangeOptions.ForType(ToSearchResultType())
            .Select(CreateArrangeOption)
            .ToList();

        SelectedArrangeBy = ArrangeByOptions.FirstOrDefault(option => option.Value == currentArrangeBy)
            ?? ArrangeByOptions.FirstOrDefault(option => option.Value == SearchArrangeOption.Unarranged)
            ?? CreateArrangeOption(SearchArrangeOption.Unarranged);
    }

    private static SearchArrangeOptionView CreateArrangeOption(SearchArrangeOption option)
    {
        var label = option switch
        {
            SearchArrangeOption.ReleaseDate => ReleaseDateArrange,
            SearchArrangeOption.Quality => QualityArrange,
            SearchArrangeOption.Name => NameArrange,
            SearchArrangeOption.TotalTracks => TotalTracksArrange,
            SearchArrangeOption.TotalAlbums => TotalAlbumsArrange,
            _ => UnarrangedArrange
        };

        return new SearchArrangeOptionView(option, label);
    }

    private static string GetSearchSourceUrl(SearchResultViewModel result, DownloadContentType contentType)
    {
        if (!string.IsNullOrWhiteSpace(result.WebPlayerUrl))
        {
            return result.WebPlayerUrl;
        }

        return QobuzUrlBuilder.CreateOpenUrl(contentType, result.Id);
    }

    private void HandleTrackSelectionChanged(SearchResultViewModel owner)
    {
        if (ReferenceEquals(SelectedResult, owner))
        {
            OnPropertyChanged(nameof(DestinationPreview));
        }
    }

    private void OnResultActionCompleted(SearchResultViewModel result, string message)
    {
        SelectedResult = result;
        StatusText = message;
        OnPropertyChanged(nameof(DestinationPreview));
    }
}
