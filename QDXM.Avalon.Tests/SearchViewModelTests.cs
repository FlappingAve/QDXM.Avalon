using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Search;
using QDXM.Avalon.Services;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class SearchViewModelTests
{
    [Fact]
    public void ChangingSearchTypeKeepsQuery()
    {
        var viewModel = new SearchViewModel
        {
            Query = "beatles"
        };

        viewModel.SelectedType = "Tracks";

        Assert.Equal("beatles", viewModel.Query);
    }

    [Fact]
    public async Task SelectingArtistKeepsArtistIdWhenSwitchingToArtistAlbums()
    {
        var client = new SearchViewModelTestClient
        {
            ArtistResults =
            [
                new SearchArtistResult(
                    "12345",
                    "Example Artist",
                    "example-artist",
                    string.Empty,
                    string.Empty,
                    2)
            ]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Artist",
            Query = "example"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.Results.Single().DownloadAlbumCommand.ExecuteAsync(null);

        Assert.Equal("Artist Albums", viewModel.SelectedType);
        Assert.Equal("12345", viewModel.Query);
    }

    [Fact]
    public async Task SelectingLabelKeepsLabelIdWhenSwitchingToLabelAlbums()
    {
        var client = new SearchViewModelTestClient
        {
            LabelResults =
            [
                new SearchLabelResult(
                    "98765",
                    "Example Label",
                    "example-label",
                    string.Empty,
                    3)
            ]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Label",
            Query = "example"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.Results.Single().DownloadAlbumCommand.ExecuteAsync(null);

        Assert.Equal("Label Albums", viewModel.SelectedType);
        Assert.Equal("98765", viewModel.Query);
    }

    [Fact]
    public async Task TrackContextGoToAlbumSearchesTheSourceAlbum()
    {
        var client = new SearchViewModelTestClient
        {
            TrackResults = _ => [CreateTrack("track-1", "album-1")],
            AlbumTrackResult = albumId => CreateAlbum(albumId, "Source Album")
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Tracks",
            Query = "jump"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        var track = Assert.Single(viewModel.Results);
        await track.GoToSourceAlbumCommand.ExecuteAsync(null);

        Assert.Equal("Albums", viewModel.SelectedType);
        Assert.Equal("id:album-1", viewModel.Query);
        Assert.Equal(["album-1"], client.AlbumTrackRequests);
        Assert.Equal("Source Album", Assert.Single(viewModel.Results).Title);
    }

    [Fact]
    public async Task TrackContextDownloadAlbumQueuesTheSourceAlbum()
    {
        var queuedRequests = new List<(DownloadRequest Request, SearchResultViewModel Result)>();
        var client = new SearchViewModelTestClient
        {
            TrackResults = _ => [CreateTrack("track-1", "album-1")],
            AlbumTrackResult = albumId => CreateAlbum(albumId, "Source Album")
        };
        var viewModel = new SearchViewModel(
            client,
            (request, result) => queuedRequests.Add((request, result)),
            null)
        {
            SelectedType = "Tracks",
            Query = "jump"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        var track = Assert.Single(viewModel.Results);
        await track.DownloadSourceAlbumCommand.ExecuteAsync(null);

        var queued = Assert.Single(queuedRequests);
        Assert.True(track.CanUseTrackAlbumActions);
        Assert.Equal(["album-1"], client.AlbumTrackRequests);
        Assert.Equal(DownloadContentType.Album, queued.Request.ContentType);
        Assert.Equal("album-1", queued.Request.ContentId);
        Assert.Equal("Source Album", queued.Result.Title);
    }

    [Fact]
    public async Task TrackContextGoToArtistUsesArtistAlbumsWhenArtistIdIsAvailable()
    {
        var client = new SearchViewModelTestClient
        {
            TrackResults = _ => [CreateTrack("track-1", "album-1", artistId: "12345")]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Tracks",
            Query = "jump"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        var track = Assert.Single(viewModel.Results);
        await track.GoToSourceArtistCommand.ExecuteAsync(null);

        Assert.Equal("Artist Albums", viewModel.SelectedType);
        Assert.Equal("12345", viewModel.Query);
        Assert.Equal("12345", client.LastArtistAlbumOptions?.Query);
    }

    [Fact]
    public async Task TrackContextGoToArtistFallsBackToArtistSearchWhenArtistIdIsMissing()
    {
        var client = new SearchViewModelTestClient
        {
            TrackResults = _ => [CreateTrack("track-1", "album-1", artistId: string.Empty)]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Tracks",
            Query = "jump"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        var track = Assert.Single(viewModel.Results);
        await track.GoToSourceArtistCommand.ExecuteAsync(null);

        Assert.Equal("Artist", viewModel.SelectedType);
        Assert.Equal("Artist", viewModel.Query);
        Assert.Equal("Artist", client.LastArtistOptions?.Query);
    }

    [Fact]
    public void SelectingArtistAlbumsCapsLimitOptionsAtArtistReleaseLimit()
    {
        var viewModel = new SearchViewModel
        {
            SelectedLimit = QobuzApiLimits.SearchPageSize
        };

        viewModel.SelectedType = "Artist Albums";

        Assert.Equal(QobuzApiLimits.ArtistReleasePageSize, viewModel.SelectedLimit);
        Assert.Equal([15, 25, 50, QobuzApiLimits.ArtistReleasePageSize], viewModel.LimitOptions);
        Assert.DoesNotContain(250, viewModel.LimitOptions);
        Assert.DoesNotContain(500, viewModel.LimitOptions);
        Assert.DoesNotContain(QobuzApiLimits.SearchPageSize, viewModel.LimitOptions);
    }

    [Fact]
    public void SearchTypeUpdatesArrangeOptions()
    {
        var viewModel = new SearchViewModel();
        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.TotalTracks);

        viewModel.SelectedType = "Artist";

        Assert.Equal(SearchArrangeOption.Unarranged, viewModel.SelectedArrangeBy?.Value);
        Assert.Equal(["Unarranged", "Name", "Album Count"], viewModel.ArrangeByOptions.Select(option => option.Label));

        viewModel.SelectedType = "Playlists";

        Assert.Equal(["Unarranged", "Last Updated", "Name", "Track Count"], viewModel.ArrangeByOptions.Select(option => option.Label));
    }

    [Fact]
    public void CollapseExpandedResultsExceptKeepsOnlyTargetExpanded()
    {
        var viewModel = new SearchViewModel();
        var first = new SearchResultViewModel((_, _) => { }) { IsExpanded = true };
        var second = new SearchResultViewModel((_, _) => { }) { IsExpanded = true };

        viewModel.Results.Add(first);
        viewModel.Results.Add(second);

        viewModel.CollapseExpandedResultsExcept(second);

        Assert.False(first.IsExpanded);
        Assert.True(second.IsExpanded);
    }

    [Fact]
    public async Task ArtistAlbumsSearchPassesServerSortOption()
    {
        var client = new SearchViewModelTestClient();
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Artist Albums",
            Query = "12345",
            SelectedSort = "Relevance"
        };
        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.Quality);

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.NotNull(client.LastArtistAlbumOptions);
        Assert.Equal(SearchArtistAlbumSortOption.Relevance, client.LastArtistAlbumOptions.ArtistAlbumSort);
        Assert.Equal(1, client.ArtistAlbumSearchCount);
    }

    [Fact]
    public async Task ChangingSearchTypeCancelsIncrementalArtistAlbumResults()
    {
        var allowLateResult = new TaskCompletionSource();
        var firstResultLoaded = new TaskCompletionSource();
        var client = new SearchViewModelTestClient
        {
            ArtistAlbumResults = CreateSlowArtistAlbumResults
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Artist Albums",
            Query = "12345"
        };

        var searchTask = viewModel.SearchCommand.ExecuteAsync(null);
        await firstResultLoaded.Task;

        Assert.Equal(["First"], viewModel.Results.Select(result => result.Title));

        viewModel.SelectedType = "Albums";
        allowLateResult.SetResult();
        await searchTask;

        Assert.Empty(viewModel.Results);
        Assert.Equal("Albums", viewModel.SelectedType);
        Assert.False(viewModel.IsSearching);

        async IAsyncEnumerable<SearchAlbumResult> CreateSlowArtistAlbumResults(
            SearchQueryOptions _,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            yield return CreateAlbum("First");
            firstResultLoaded.SetResult();
            await allowLateResult.Task.WaitAsync(cancellationToken);
            yield return CreateAlbum("Late");
        }
    }

    [Fact]
    public async Task PlaylistTrackPagesFetchLargeApiChunksAndCacheVisiblePages()
    {
        var client = new SearchViewModelTestClient
        {
            PlaylistResults = [CreatePlaylist(totalTracks: 600)],
            PlaylistTrackPages = (_, limit, offset) =>
            {
                var count = Math.Min(limit, 600 - offset);
                var tracks = Enumerable.Range(offset + 1, count)
                    .Select(CreatePlaylistTrack)
                    .ToList();
                return new SearchPlaylistTrackPage(600, tracks);
            }
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Playlists",
            Query = "playlist"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        var result = viewModel.Results.Single();

        await result.ToggleExpandedCommand.ExecuteAsync(null);

        Assert.Equal([(QobuzApiLimits.PlaylistTrackPageSize, 0)], client.PlaylistTrackRequests);
        Assert.Equal(QobuzApiLimits.PlaylistTrackPreviewPageSize, result.Tracks.Count);
        Assert.Equal("Track 1", result.Tracks.First().Title);
        Assert.Equal("Page 1 of 6", result.TrackPageText);

        await result.NextTrackPageCommand.ExecuteAsync(null);

        Assert.Equal([(QobuzApiLimits.PlaylistTrackPageSize, 0)], client.PlaylistTrackRequests);
        Assert.Equal("Track 101", result.Tracks.First().Title);
        Assert.Equal("Page 2 of 6", result.TrackPageText);

        for (var i = 0; i < 4; i++)
        {
            await result.NextTrackPageCommand.ExecuteAsync(null);
        }

        Assert.Equal(
            [(QobuzApiLimits.PlaylistTrackPageSize, 0), (QobuzApiLimits.PlaylistTrackPageSize, 500)],
            client.PlaylistTrackRequests);
        Assert.Equal("Track 501", result.Tracks.First().Title);
        Assert.Equal("Page 6 of 6", result.TrackPageText);
    }

    [Fact]
    public async Task ChangingArrangeByReordersLoadedResultsWithoutSearchingAgain()
    {
        var client = new SearchViewModelTestClient
        {
            AlbumResults = _ =>
            [
                CreateAlbum("C"),
                CreateAlbum("A"),
                CreateAlbum("B")
            ]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Albums",
            Query = "test"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(["C", "A", "B"], viewModel.Results.Select(result => result.Title));
        Assert.Equal(1, client.AlbumSearchCount);

        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.Name);

        Assert.Equal(["A", "B", "C"], viewModel.Results.Select(result => result.Title));
        Assert.Equal(1, client.AlbumSearchCount);

        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.Unarranged);

        Assert.Equal(["C", "A", "B"], viewModel.Results.Select(result => result.Title));
        Assert.Equal(1, client.AlbumSearchCount);
    }

    [Fact]
    public async Task LoadMoreAppendsArrangedPageWithoutResortingPreviouslyLoadedResults()
    {
        var client = new SearchViewModelTestClient
        {
            AlbumResults = options => options.Offset == 0
                ? [CreateAlbum("C"), CreateAlbum("A")]
                : [CreateAlbum("D"), CreateAlbum("B")]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Albums",
            Query = "test"
        };
        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.Name);

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["A", "C", "B", "D"], viewModel.Results.Select(result => result.Title));
        Assert.Equal(2, client.AlbumSearchCount);

        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.Unarranged);
        Assert.Equal(["C", "A", "D", "B"], viewModel.Results.Select(result => result.Title));

        viewModel.SelectedArrangeBy = GetArrangeOption(viewModel, SearchArrangeOption.Name);
        Assert.Equal(["A", "B", "C", "D"], viewModel.Results.Select(result => result.Title));
        Assert.Equal(2, client.AlbumSearchCount);
    }

    [Fact]
    public async Task GenreSearchUsesStorefrontSortAndPageBasedLoadMore()
    {
        var client = new SearchViewModelTestClient
        {
            GenreResults = options => options.Offset == 0
                ? [CreateAlbum("First")]
                : [CreateAlbum("Second")]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Genres",
            Query = "https://www.qobuz.com/fr-fr/genre/k-pop/download-streaming-albums?ssf%5BsortBy%5D=main_catalog_date_desc"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], viewModel.Results.Select(result => result.Title));
        Assert.Equal("Newest", viewModel.SelectedSort);
        Assert.Equal(SearchGenreSortOption.Newest, client.LastGenreOptions?.GenreSort);
        Assert.Equal(2, client.GenreSearchCount);
        Assert.Equal(1, client.LastGenreOptions?.Offset);
    }

    [Fact]
    public async Task GenreSearchDoesNotOverrideManualSortSelectionOnSearch()
    {
        var client = new SearchViewModelTestClient
        {
            GenreResults = _ => [CreateAlbum("First")]
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            SelectedType = "Genres",
            Query = "https://www.qobuz.com/fr-fr/genre/k-pop/download-streaming-albums?ssf%5BsortBy%5D=main_catalog_date_desc",
            SelectedSort = "Best Sellers"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(SearchGenreSortOption.BestSellers, client.LastGenreOptions?.GenreSort);
    }

    [Fact]
    public void SelectingGenresKeepsPageSizeBindingOnValidIntegerOptions()
    {
        var viewModel = new SearchViewModel(null, null, null)
        {
            SelectedType = "Genres"
        };

        Assert.False(viewModel.IsResultsPerPageVisible);
        Assert.Equal(25, viewModel.SelectedLimit);
        Assert.Contains(25, viewModel.LimitOptions);

        viewModel.SelectedType = "Albums";

        Assert.True(viewModel.IsResultsPerPageVisible);
        Assert.Equal(25, viewModel.SelectedLimit);
        Assert.Contains(25, viewModel.LimitOptions);
    }

    [Fact]
    public async Task PreviewPlaybackStateMarksOnlyMatchingTrackAsActive()
    {
        var client = new SearchViewModelTestClient
        {
            AlbumResults = _ => [CreateAlbum("Album")],
            AlbumTrackResult = albumId => CreateAlbumWithTrack(albumId, "track-1")
        };
        var viewModel = new SearchViewModel(client, null, null)
        {
            Query = "album"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        var result = Assert.Single(viewModel.Results);
        await result.ToggleExpandedCommand.ExecuteAsync(null);
        var firstTrack = Assert.Single(result.Tracks);

        viewModel.UpdatePreviewPlaybackState(firstTrack.TrackId, previewIsPlaying: true);

        Assert.True(firstTrack.IsPreviewActive);
        Assert.True(firstTrack.IsPreviewPlaying);

        viewModel.UpdatePreviewPlaybackState("other-track", previewIsPlaying: true);

        Assert.False(firstTrack.IsPreviewActive);
        Assert.False(firstTrack.IsPreviewPlaying);
    }


    [Fact]
    public async Task ClearingSearchResultsSchedulesMemoryCleanupWithoutRunningInline()
    {
        var client = new SearchViewModelTestClient
        {
            AlbumResults = _ => [CreateAlbum("A")]
        };
        var scheduler = new CapturingSearchMemoryCleanupScheduler();
        var cleanupCount = 0;
        var viewModel = new SearchViewModel(
            client,
            null,
            null,
            memoryCleanupScheduler: scheduler,
            collectReleasedSearchMemory: () => cleanupCount++)
        {
            SelectedType = "Albums",
            Query = "test"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Single(scheduler.ScheduledCleanups);
        Assert.Equal(0, cleanupCount);
    }

    [Fact]
    public async Task ScheduledSearchMemoryCleanupsCoalesceToNewestRequest()
    {
        var client = new SearchViewModelTestClient
        {
            AlbumResults = _ => [CreateAlbum("A")]
        };
        var scheduler = new CapturingSearchMemoryCleanupScheduler();
        var cleanupCount = 0;
        var viewModel = new SearchViewModel(
            client,
            null,
            null,
            memoryCleanupScheduler: scheduler,
            collectReleasedSearchMemory: () => cleanupCount++)
        {
            SelectedType = "Albums",
            Query = "test"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(2, scheduler.ScheduledCleanups.Count);

        scheduler.RunScheduledCleanup(0);
        Assert.Equal(0, cleanupCount);

        scheduler.RunScheduledCleanup(1);
        Assert.Equal(1, cleanupCount);
    }

    private static SearchArrangeOptionView GetArrangeOption(
        SearchViewModel viewModel,
        SearchArrangeOption option)
    {
        return viewModel.ArrangeByOptions.Single(arrangeOption => arrangeOption.Value == option);
    }

    private sealed class SearchViewModelTestClient : IQobuzClient
    {
        public IReadOnlyList<SearchArtistResult> ArtistResults { get; init; } = [];
        public IReadOnlyList<SearchLabelResult> LabelResults { get; init; } = [];
        public IReadOnlyList<SearchPlaylistResult> PlaylistResults { get; init; } = [];
        public Func<SearchQueryOptions, IReadOnlyList<SearchAlbumResult>> AlbumResults { get; init; } = _ => [];
        public Func<SearchQueryOptions, IReadOnlyList<SearchTrackResult>> TrackResults { get; init; } = _ => [];
        public Func<SearchQueryOptions, IReadOnlyList<SearchAlbumResult>> GenreResults { get; init; } = _ => [];
        public Func<string, SearchAlbumResult>? AlbumTrackResult { get; init; }
        public Func<SearchQueryOptions, CancellationToken, IAsyncEnumerable<SearchAlbumResult>>? ArtistAlbumResults { get; init; }
        public Func<string, int, int, SearchPlaylistTrackPage> PlaylistTrackPages { get; init; } = (_, _, _) => new SearchPlaylistTrackPage(0, []);
        public List<(int Limit, int Offset)> PlaylistTrackRequests { get; } = [];
        public List<string> AlbumTrackRequests { get; } = [];
        public SearchQueryOptions? LastArtistOptions { get; private set; }
        public SearchQueryOptions? LastArtistAlbumOptions { get; private set; }
        public SearchQueryOptions? LastGenreOptions { get; private set; }
        public int AlbumSearchCount { get; private set; }
        public int ArtistAlbumSearchCount { get; private set; }
        public int GenreSearchCount { get; private set; }

        public Task<IReadOnlyList<SearchAlbumResult>> SearchAlbumsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default)
        {
            AlbumSearchCount++;
            return Task.FromResult(AlbumResults(options));
        }

        public Task<IReadOnlyList<SearchTrackResult>> SearchTracksAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TrackResults(options));

        public Task<IReadOnlyList<SearchPlaylistResult>> SearchPlaylistsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaylistResults);

        public Task<SearchPlaylistTrackPage> GetPlaylistTracksAsync(
            string playlistId,
            int limit,
            int offset,
            CancellationToken cancellationToken = default)
        {
            PlaylistTrackRequests.Add((limit, offset));
            return Task.FromResult(PlaylistTrackPages(playlistId, limit, offset));
        }

        public Task<SearchTrackResult> GetTrackAsync(
            string trackId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchArtistResult>> SearchArtistsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default)
        {
            LastArtistOptions = options;
            return Task.FromResult(ArtistResults);
        }

        public Task<IReadOnlyList<SearchAlbumResult>> SearchArtistAlbumsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchAlbumResult>>([]);

        public async IAsyncEnumerable<SearchAlbumResult> SearchArtistAlbumsIncrementalAsync(
            SearchQueryOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastArtistAlbumOptions = options;
            ArtistAlbumSearchCount++;
            if (ArtistAlbumResults is not null)
            {
                await foreach (var result in ArtistAlbumResults(options, cancellationToken).WithCancellation(cancellationToken))
                {
                    yield return result;
                }

                yield break;
            }

            await Task.CompletedTask;
            yield break;
        }

        public Task<IReadOnlyList<SearchLabelResult>> SearchLabelsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LabelResults);

        public Task<IReadOnlyList<SearchAlbumResult>> SearchLabelAlbumsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchAlbumResult>>([]);

        public Task<IReadOnlyList<SearchAlbumResult>> SearchGenreAlbumsAsync(
            SearchQueryOptions options,
            CancellationToken cancellationToken = default)
        {
            LastGenreOptions = options;
            GenreSearchCount++;
            return Task.FromResult(GenreResults(options));
        }

        public Task<SearchAlbumResult> GetAlbumTracksAsync(
            string albumId,
            CancellationToken cancellationToken = default)
        {
            AlbumTrackRequests.Add(albumId);
            if (AlbumTrackResult is null)
            {
                throw new NotSupportedException();
            }

            return Task.FromResult(AlbumTrackResult(albumId));
        }
    }

    private sealed class CapturingSearchMemoryCleanupScheduler : ISearchMemoryCleanupScheduler
    {
        public List<ScheduledCleanup> ScheduledCleanups { get; } = [];

        public void Schedule(TimeSpan delay, Func<bool> isCurrent, Action cleanup)
        {
            ScheduledCleanups.Add(new ScheduledCleanup(delay, isCurrent, cleanup));
        }

        public void RunScheduledCleanup(int index)
        {
            var scheduled = ScheduledCleanups[index];
            if (scheduled.IsCurrent())
            {
                scheduled.Cleanup();
            }
        }

        public sealed record ScheduledCleanup(TimeSpan Delay, Func<bool> IsCurrent, Action Cleanup);
    }

    private static SearchAlbumResult CreateAlbum(string title)
    {
        return CreateAlbum(title, title);
    }

    private static SearchAlbumResult CreateAlbum(string albumId, string title)
    {
        return new SearchAlbumResult(
            AlbumId: albumId,
            Title: title,
            Version: string.Empty,
            Artist: "Artist",
            Quality: "FLAC 16/44.1",
            ReleaseDate: "2024-01-01",
            Upc: null,
            ThumbnailUrl: string.Empty,
            WebPlayerUrl: string.Empty,
            StoreUrl: string.Empty,
            TotalTracks: 1,
            TotalDiscs: 1,
            Explicit: false,
            Tracks: []);
    }

    private static SearchTrackResult CreateTrack(
        string trackId,
        string albumId,
        string artistId = "12345")
    {
        return new SearchTrackResult(
            TrackId: trackId,
            AlbumId: albumId,
            ArtistId: artistId,
            Title: "Track",
            Version: string.Empty,
            Artist: "Artist",
            AlbumTitle: "Source Album",
            Quality: "FLAC 16/44.1",
            Duration: TimeSpan.FromSeconds(30),
            ReleaseDate: "2024-01-01",
            ThumbnailUrl: string.Empty,
            WebPlayerUrl: string.Empty,
            StoreUrl: string.Empty,
            Explicit: false);
    }

    private static SearchAlbumResult CreateAlbumWithTrack(string title, string trackId)
    {
        return new SearchAlbumResult(
            AlbumId: title,
            Title: title,
            Version: string.Empty,
            Artist: "Artist",
            Quality: "FLAC 16/44.1",
            ReleaseDate: "2024-01-01",
            Upc: null,
            ThumbnailUrl: string.Empty,
            WebPlayerUrl: string.Empty,
            StoreUrl: string.Empty,
            TotalTracks: 1,
            TotalDiscs: 1,
            Explicit: false,
            Tracks:
            [
                new SearchAlbumTrack(
                    TrackId: trackId,
                    TrackNumber: 1,
                    DiscNumber: 1,
                    Title: "Track",
                    Version: string.Empty,
                    Work: string.Empty,
                    Composer: string.Empty,
                    Duration: TimeSpan.FromSeconds(30),
                    Quality: "FLAC 16/44.1",
                    Explicit: false)
            ]);
    }

    private static SearchPlaylistResult CreatePlaylist(int totalTracks)
    {
        return new SearchPlaylistResult(
            PlaylistId: "playlist",
            Title: "Playlist",
            Owner: "Owner",
            UpdatedDate: "2024-01-01",
            CreatedDate: "2024-01-01",
            Duration: TimeSpan.Zero,
            ThumbnailUrl: string.Empty,
            WebPlayerUrl: string.Empty,
            TotalTracks: totalTracks);
    }

    private static SearchPlaylistTrackResult CreatePlaylistTrack(int position)
    {
        return new SearchPlaylistTrackResult(
            TrackId: position.ToString(),
            SelectionKey: position.ToString(),
            PlaylistPosition: position,
            PlaylistPositionDisplay: position.ToString(),
            AlbumTrackNumber: position,
            AlbumDiscNumber: 1,
            AlbumPositionDisplay: position.ToString(),
            Title: $"Track {position}",
            Version: string.Empty,
            Artist: "Artist",
            AlbumTitle: "Album",
            Duration: TimeSpan.Zero);
    }
}
