using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Views;

public partial class SearchView : UserControl
{
    private const double ExpandedTracksViewportPadding = 8;
    private const double TrackWheelScrollStep = 48;

    private long expandedLayoutRecalculationVersion;
    private long searchResultsRefreshVersion;

    private static readonly AttachedProperty<SearchResultViewModel?> ThumbnailOwnerProperty =
        AvaloniaProperty.RegisterAttached<Control, SearchResultViewModel?>(
            "ThumbnailOwner",
            typeof(SearchView));

    private static readonly AttachedProperty<IDisposable?> StickyHeaderSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, IDisposable?>(
            "StickyHeaderSubscription",
            typeof(SearchView));

    public SearchView()
    {
        InitializeComponent();
        SearchResultsScrollViewer.SizeChanged += SearchResultsScrollViewer_OnSizeChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ScheduleExpandedLayoutRecalculation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && IsVisible)
        {
            ScheduleExpandedLayoutRecalculation();
        }
    }

    private void SearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            DataContext is not SearchViewModel viewModel ||
            !viewModel.SearchCommand.CanExecute(null))
        {
            return;
        }

        viewModel.SearchCommand.Execute(null);
        FocusSearchResultsScrollViewer();
        e.Handled = true;
    }

    private void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FocusSearchResultsScrollViewer();
    }

    private async void SearchResultHeader_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: SearchResultViewModel result } ||
            WasClickInsideButton(e.Source as global::Avalonia.Visual))
        {
            return;
        }

        var resultContainer = FindSearchResultContainer(sender as global::Avalonia.Visual);
        var shouldSnapExpandedResult = result.CanExpand && !result.IsExpanded;
        SearchResultsScrollViewer.Focus();

        if (DataContext is SearchViewModel viewModel)
        {
            viewModel.SelectedResult = result;
            if (shouldSnapExpandedResult)
            {
                viewModel.CollapseExpandedResultsExcept(result);
            }
        }

        if (result.CanExpand &&
            result.ToggleExpandedCommand is IAsyncRelayCommand toggleExpandedCommand &&
            toggleExpandedCommand.CanExecute(null))
        {
            await toggleExpandedCommand.ExecuteAsync(null);
        }
        else if (result.CanExpand && result.ToggleExpandedCommand.CanExecute(null))
        {
            result.ToggleExpandedCommand.Execute(null);
        }

        if (shouldSnapExpandedResult && resultContainer is not null)
        {
            await SnapExpandedResultToViewportAsync(resultContainer);
        }

        e.Handled = true;
    }

    private void SearchResult_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateThumbnailOwner(control);
        }
    }

    private void SearchResult_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            SetThumbnailOwner(control, null);
        }
    }

    private void SearchResult_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
        {
            UpdateThumbnailOwner(control);
        }
    }

    private void SearchResultsScroll_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var delta = e.Key switch
        {
            Key.PageUp => -scrollViewer.Viewport.Height,
            Key.PageDown => scrollViewer.Viewport.Height,
            Key.Home => -scrollViewer.Offset.Y,
            Key.End => scrollViewer.Extent.Height,
            _ => (double?)null
        };

        if (delta is null)
        {
            return;
        }

        ScrollVertically(scrollViewer, delta.Value);
        ScheduleSearchResultsRefresh(GetKeyboardRevealIndex(e.Key), scrollViewer.Offset.Y);
        e.Handled = true;
    }

    private void SearchResultsScroll_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control &&
            !HasVisualAncestorWithClass(e.Source as global::Avalonia.Visual, "expandedTracksScroll"))
        {
            control.Focus();
        }
    }

    private void FocusSearchResultsScrollViewer()
    {
        Dispatcher.UIThread.Post(
            () => SearchResultsScrollViewer.Focus(),
            DispatcherPriority.Background);
    }

    private void ExpandedTracksScroll_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (!CanScrollVertically(scrollViewer))
        {
            return;
        }

        var delta = e.Key switch
        {
            Key.PageUp => -scrollViewer.Viewport.Height,
            Key.PageDown => scrollViewer.Viewport.Height,
            Key.Home => -scrollViewer.Offset.Y,
            Key.End => scrollViewer.Extent.Height,
            _ => (double?)null
        };

        if (delta is null)
        {
            return;
        }

        ScrollVertically(scrollViewer, delta.Value);
        e.Handled = true;
    }

    private void ExpandedTracksScroll_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            control.Focus();
        }
    }

    private void ExpandedTracksScroll_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (!CanScrollVertically(scrollViewer))
        {
            return;
        }

        ScrollVertically(scrollViewer, -e.Delta.Y * TrackWheelScrollStep);
        e.Handled = true;
    }

    private void ExpandedTracksScroll_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.GetValue(StickyHeaderSubscriptionProperty)?.Dispose();
        UpdateStickyExpandedTrackHeader(scrollViewer);
        var subscription = scrollViewer
            .GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(new AnonymousObserver<Vector>(_ => UpdateStickyExpandedTrackHeader(scrollViewer)));
        scrollViewer.SetValue(StickyHeaderSubscriptionProperty, subscription);
        Dispatcher.UIThread.Post(
            () => UpdateStickyExpandedTrackHeader(scrollViewer),
            DispatcherPriority.Background);
    }

    private void ExpandedTracksScroll_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.GetValue(StickyHeaderSubscriptionProperty)?.Dispose();
        scrollViewer.SetValue(StickyHeaderSubscriptionProperty, null);
    }

    private void SearchResultsScrollViewer_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ScheduleExpandedLayoutRecalculation();
    }

    private static Control? FindStickyExpandedTrackHeader(ScrollViewer scrollViewer)
    {
        return scrollViewer
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Classes.Contains("stickyExpandedTrackHeader"));
    }

    private static void UpdateStickyExpandedTrackHeader(ScrollViewer scrollViewer)
    {
        var header = FindStickyExpandedTrackHeader(scrollViewer);
        if (header is null)
        {
            return;
        }

        if (header.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            header.RenderTransform = transform;
        }

        transform.Y = Math.Ceiling(scrollViewer.Offset.Y);
    }

    private static bool WasClickInsideButton(global::Avalonia.Visual? visual)
    {
        return visual is not null &&
            visual.GetSelfAndVisualAncestors().OfType<Button>().Any();
    }

    private static Control? FindSearchResultContainer(global::Avalonia.Visual? visual)
    {
        return visual?
            .GetSelfAndVisualAncestors()
            .OfType<Control>()
            .FirstOrDefault(control => control.Classes.Contains("searchResult"));
    }

    private static bool HasVisualAncestorWithClass(global::Avalonia.Visual? visual, string className)
    {
        return visual is not null &&
            visual.GetSelfAndVisualAncestors()
                .OfType<Control>()
                .Any(control => control.Classes.Contains(className));
    }

    private async Task SnapExpandedResultToViewportAsync(Control resultContainer)
    {
        await Dispatcher.UIThread.InvokeAsync(
            () => SnapExpandedResultToViewport(resultContainer),
            DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(
            () => SnapExpandedResultToViewport(resultContainer),
            DispatcherPriority.Render);
    }

    private void SnapExpandedResultToViewport(Control resultContainer)
    {
        var resultTop = resultContainer.TranslatePoint(new Point(), SearchResultsScrollViewer)?.Y;
        if (resultTop is null)
        {
            return;
        }

        var targetOffsetY = SearchResultsScrollViewer.Offset.Y + resultTop.Value;
        var maxOffsetY = Math.Max(0, SearchResultsScrollViewer.Extent.Height - SearchResultsScrollViewer.Viewport.Height);
        SearchResultsScrollViewer.Offset = new Vector(
            SearchResultsScrollViewer.Offset.X,
            Math.Clamp(targetOffsetY, 0, maxOffsetY));

        CapExpandedTrackScroller(resultContainer);
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer)
    {
        return scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 1;
    }

    private static void ScrollVertically(ScrollViewer scrollViewer, double deltaY)
    {
        var maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var nextOffsetY = Math.Clamp(scrollViewer.Offset.Y + deltaY, 0, maxOffsetY);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextOffsetY);
    }

    private int? GetKeyboardRevealIndex(Key key)
    {
        if (DataContext is not SearchViewModel viewModel || viewModel.Results.Count == 0)
        {
            return null;
        }

        return key switch
        {
            Key.Home => 0,
            Key.End => viewModel.Results.Count - 1,
            _ => null
        };
    }

    private void ScheduleSearchResultsRefresh(int? resultIndexToReveal = null, double? finalOffsetY = null)
    {
        var version = ++searchResultsRefreshVersion;

        RefreshSearchResultsLayout(resultIndexToReveal, finalOffsetY);

        _ = Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (version == searchResultsRefreshVersion)
                {
                    RefreshSearchResultsLayout(resultIndexToReveal, finalOffsetY);
                }
            },
            DispatcherPriority.Loaded);

        _ = Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (version == searchResultsRefreshVersion)
                {
                    RefreshSearchResultsLayout(resultIndexToReveal, finalOffsetY);
                }
            },
            DispatcherPriority.Render);
    }

    private void RefreshSearchResultsLayout(int? resultIndexToReveal, double? finalOffsetY)
    {
        InvalidateSearchResultsLayout();

        if (resultIndexToReveal is int index)
        {
            RevealSearchResult(index, finalOffsetY);
        }
    }

    private void RevealSearchResult(int index, double? finalOffsetY)
    {
        if (index < 0)
        {
            return;
        }

        // Home/End can leave ItemsRepeater's realized range stale; realizing the edge item mirrors a user scroll.
        SearchResultsRepeater.GetOrCreateElement(index)?.BringIntoView();

        if (finalOffsetY is double offsetY)
        {
            var maxOffsetY = Math.Max(0, SearchResultsScrollViewer.Extent.Height - SearchResultsScrollViewer.Viewport.Height);
            SearchResultsScrollViewer.Offset = new Vector(
                SearchResultsScrollViewer.Offset.X,
                Math.Clamp(offsetY, 0, maxOffsetY));
        }
    }

    private void InvalidateSearchResultsLayout()
    {
        SearchResultsRepeater.InvalidateMeasure();
        SearchResultsRepeater.InvalidateArrange();
        SearchResultsScrollViewer.InvalidateMeasure();
        SearchResultsScrollViewer.InvalidateArrange();
    }

    private void ScheduleExpandedLayoutRecalculation()
    {
        var version = ++expandedLayoutRecalculationVersion;

        _ = Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (version != expandedLayoutRecalculationVersion ||
                    FindSelectedExpandedResultContainer() is not { } resultContainer)
                {
                    return;
                }

                SnapExpandedResultToViewport(resultContainer);
            },
            DispatcherPriority.Render);
    }

    private Control? FindSelectedExpandedResultContainer()
    {
        if (DataContext is not SearchViewModel { SelectedResult: { IsExpanded: true } selectedResult })
        {
            return null;
        }

        return SearchResultsScrollViewer
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control =>
                control.Classes.Contains("searchResult") &&
                ReferenceEquals(control.DataContext, selectedResult));
    }

    private void CapExpandedTrackScroller(Control resultContainer)
    {
        var trackScroller = resultContainer
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault(scrollViewer =>
                scrollViewer.IsVisible &&
                scrollViewer.Classes.Contains("expandedTracksScroll"));

        var scrollerTop = trackScroller?.TranslatePoint(new Point(), SearchResultsScrollViewer)?.Y;
        if (trackScroller is null || scrollerTop is null)
        {
            return;
        }

        var availableHeight = SearchResultsScrollViewer.Viewport.Height - scrollerTop.Value - ExpandedTracksViewportPadding;
        trackScroller.MaxHeight = Math.Max(0, availableHeight);
        ScrollVertically(trackScroller, 0);
    }

    private void UpdateThumbnailOwner(Control control)
    {
        var result = control.DataContext as SearchResultViewModel;
        SetThumbnailOwner(control, result);
    }

    private void SetThumbnailOwner(Control control, SearchResultViewModel? result)
    {
        var previous = control.GetValue(ThumbnailOwnerProperty);
        if (ReferenceEquals(previous, result))
        {
            return;
        }

        if (previous is not null)
        {
            previous.DetachThumbnailVisual(ShouldKeepThumbnailLoaded(previous));
        }

        control.SetValue(ThumbnailOwnerProperty, result);
        result?.AttachThumbnailVisual();
    }

    private bool ShouldKeepThumbnailLoaded(SearchResultViewModel result)
    {
        return DataContext is SearchViewModel viewModel &&
            ReferenceEquals(viewModel.SelectedResult, result);
    }
}
