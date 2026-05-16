using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace QDXM.Avalon.Behaviors;

public enum MiddlePanAxes
{
    Horizontal,
    Vertical,
    Both
}

public static class MiddlePan
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsEnabled",
            typeof(MiddlePan));

    public static readonly AttachedProperty<MiddlePanAxes> AxesProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, MiddlePanAxes>(
            "Axes",
            typeof(MiddlePan),
            MiddlePanAxes.Horizontal);

    public static readonly AttachedProperty<string?> IgnoredAncestorClassProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, string?>(
            "IgnoredAncestorClass",
            typeof(MiddlePan));

    private static readonly AttachedProperty<Controller?> ControllerProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, Controller?>(
            "Controller",
            typeof(MiddlePan));

    static MiddlePan()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(ScrollViewer element)
    {
        return element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(ScrollViewer element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static MiddlePanAxes GetAxes(ScrollViewer element)
    {
        return element.GetValue(AxesProperty);
    }

    public static void SetAxes(ScrollViewer element, MiddlePanAxes value)
    {
        element.SetValue(AxesProperty, value);
    }

    public static string? GetIgnoredAncestorClass(ScrollViewer element)
    {
        return element.GetValue(IgnoredAncestorClassProperty);
    }

    public static void SetIgnoredAncestorClass(ScrollViewer element, string? value)
    {
        element.SetValue(IgnoredAncestorClassProperty, value);
    }

    private static void OnIsEnabledChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            if (scrollViewer.GetValue(ControllerProperty) is null)
            {
                scrollViewer.SetValue(ControllerProperty, new Controller(scrollViewer));
            }

            return;
        }

        if (scrollViewer.GetValue(ControllerProperty) is { } controller)
        {
            controller.Dispose();
            scrollViewer.ClearValue(ControllerProperty);
        }
    }

    private sealed class Controller : IDisposable
    {
        private const double DeadZone = 8;
        private const double HoldThreshold = 3;
        private const double MaxPixelsPerSecond = 1700;
        private const double SpeedFactor = 2.6;

        private readonly ScrollViewer scrollViewer;
        private Control? indicator;
        private AdornerLayer? adornerLayer;
        private TopLevel? topLevel;
        private Point anchor;
        private Point current;
        private TimeSpan? lastFrameElapsed;
        private int animationGeneration;
        private bool frameRequested;
        private bool middlePressed;
        private bool movedWhilePressed;
        private bool toggleActive;

        public Controller(ScrollViewer scrollViewer)
        {
            this.scrollViewer = scrollViewer;

            scrollViewer.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
            scrollViewer.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, true);
            scrollViewer.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);
            scrollViewer.PointerCaptureLost += OnPointerCaptureLost;
            scrollViewer.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        public void Dispose()
        {
            Stop();

            scrollViewer.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            scrollViewer.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            scrollViewer.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            scrollViewer.PointerCaptureLost -= OnPointerCaptureLost;
            scrollViewer.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Handled || HasIgnoredAncestor(e.Source as global::Avalonia.Visual))
            {
                return;
            }

            if (toggleActive)
            {
                Stop();
                e.Handled = true;
                return;
            }

            var point = e.GetCurrentPoint(scrollViewer);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed || !CanScroll())
            {
                return;
            }

            middlePressed = true;
            movedWhilePressed = false;
            Begin(point.Position);
            e.Pointer.Capture(scrollViewer);
            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!middlePressed && !toggleActive)
            {
                return;
            }

            current = e.GetCurrentPoint(scrollViewer).Position;
            if (middlePressed && DistanceFromAnchor() > HoldThreshold)
            {
                movedWhilePressed = true;
            }

            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!middlePressed)
            {
                return;
            }

            var point = e.GetCurrentPoint(scrollViewer);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonReleased)
            {
                return;
            }

            middlePressed = false;
            e.Pointer.Capture(null);

            if (movedWhilePressed)
            {
                Stop();
            }
            else
            {
                toggleActive = true;
                AttachTopLevelHandlers();
            }

            e.Handled = true;
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (middlePressed && !toggleActive)
            {
                Stop();
            }
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            Stop();
        }

        private void OnTopLevelDeactivated(object? sender, EventArgs e)
        {
            Stop();
        }

        private void OnTopLevelPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!toggleActive)
            {
                return;
            }

            current = e.GetCurrentPoint(scrollViewer).Position;
        }

        private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!toggleActive)
            {
                return;
            }

            Stop();
            e.Handled = true;
        }

        private void Begin(Point position)
        {
            anchor = position;
            current = position;
            EnsureIndicator();
            if (indicator is not null)
            {
                indicator.Margin = new Thickness(anchor.X - 10, anchor.Y - 10, 0, 0);
            }

            StartAnimation();
        }

        private void Stop()
        {
            middlePressed = false;
            movedWhilePressed = false;
            toggleActive = false;
            animationGeneration++;
            frameRequested = false;
            lastFrameElapsed = null;
            DetachTopLevelHandlers();
            RemoveIndicator();
        }

        private void StartAnimation()
        {
            if (frameRequested)
            {
                return;
            }

            var generation = ++animationGeneration;
            lastFrameElapsed = null;
            RequestNextFrame(generation);
        }

        private void RequestNextFrame(int generation)
        {
            var level = TopLevel.GetTopLevel(scrollViewer);
            if (level is null)
            {
                frameRequested = false;
                return;
            }

            frameRequested = true;
            level.RequestAnimationFrame(elapsed => OnAnimationFrame(elapsed, generation));
        }

        private void OnAnimationFrame(TimeSpan elapsed, int generation)
        {
            if (generation != animationGeneration)
            {
                return;
            }

            frameRequested = false;
            if (!middlePressed && !toggleActive)
            {
                Stop();
                return;
            }

            var deltaSeconds = lastFrameElapsed is { } lastElapsed
                ? Math.Clamp((elapsed - lastElapsed).TotalSeconds, 0, 0.05)
                : 1d / 60d;
            lastFrameElapsed = elapsed;

            var offset = scrollViewer.Offset;
            var nextX = offset.X;
            var nextY = offset.Y;
            var axes = GetAxes(scrollViewer);

            if (axes is MiddlePanAxes.Horizontal or MiddlePanAxes.Both)
            {
                nextX = GetNextOffset(offset.X, current.X - anchor.X, deltaSeconds, scrollViewer.Extent.Width, scrollViewer.Viewport.Width);
            }

            if (axes is MiddlePanAxes.Vertical or MiddlePanAxes.Both)
            {
                nextY = GetNextOffset(offset.Y, current.Y - anchor.Y, deltaSeconds, scrollViewer.Extent.Height, scrollViewer.Viewport.Height);
            }

            scrollViewer.Offset = new Vector(nextX, nextY);
            RequestNextFrame(generation);
        }

        private static double GetNextOffset(double offset, double delta, double deltaSeconds, double extent, double viewport)
        {
            if (Math.Abs(delta) <= DeadZone)
            {
                return offset;
            }

            var direction = Math.Sign(delta);
            var rawDistance = Math.Abs(delta) - DeadZone;
            var pixelsPerSecond = Math.Min(MaxPixelsPerSecond, Math.Pow(rawDistance, 1.25) * SpeedFactor);
            var pixels = pixelsPerSecond * deltaSeconds;
            var maxOffset = Math.Max(0, extent - viewport);

            return Math.Clamp(offset + (direction * pixels), 0, maxOffset);
        }

        private double DistanceFromAnchor()
        {
            var axes = GetAxes(scrollViewer);
            var dx = axes is MiddlePanAxes.Horizontal or MiddlePanAxes.Both
                ? current.X - anchor.X
                : 0;
            var dy = axes is MiddlePanAxes.Vertical or MiddlePanAxes.Both
                ? current.Y - anchor.Y
                : 0;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private bool CanScroll()
        {
            var axes = GetAxes(scrollViewer);
            return (axes is MiddlePanAxes.Horizontal or MiddlePanAxes.Both &&
                    scrollViewer.Extent.Width > scrollViewer.Viewport.Width + 1) ||
                   (axes is MiddlePanAxes.Vertical or MiddlePanAxes.Both &&
                    scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 1);
        }

        private bool HasIgnoredAncestor(global::Avalonia.Visual? visual)
        {
            var ignoredClass = GetIgnoredAncestorClass(scrollViewer);
            return !string.IsNullOrWhiteSpace(ignoredClass) &&
                   visual is not null &&
                   visual.GetSelfAndVisualAncestors()
                       .OfType<Control>()
                       .Any(control => control.Classes.Contains(ignoredClass));
        }

        private void AttachTopLevelHandlers()
        {
            if (topLevel is not null)
            {
                return;
            }

            topLevel = TopLevel.GetTopLevel(scrollViewer);
            if (topLevel is not null)
            {
                topLevel.AddHandler(InputElement.PointerMovedEvent, OnTopLevelPointerMoved, RoutingStrategies.Tunnel, true);
                topLevel.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, true);
                if (topLevel is Window window)
                {
                    window.Deactivated += OnTopLevelDeactivated;
                }
            }
        }

        private void DetachTopLevelHandlers()
        {
            if (topLevel is not null)
            {
                topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnTopLevelPointerMoved);
                topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed);
                if (topLevel is Window window)
                {
                    window.Deactivated -= OnTopLevelDeactivated;
                }
                topLevel = null;
            }
        }

        private void RemoveIndicator()
        {
            if (indicator is not null && adornerLayer is not null)
            {
                adornerLayer.Children.Remove(indicator);
                indicator = null;
                adornerLayer = null;
            }
        }

        private void EnsureIndicator()
        {
            if (indicator is not null)
            {
                return;
            }

            indicator = new Avalonia.Controls.Shapes.Path
            {
                Data = StreamGeometry.Parse(GetIndicatorGeometry()),
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                StrokeThickness = 1,
                Stretch = Stretch.None,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                IsHitTestVisible = false
            };

            adornerLayer = AdornerLayer.GetAdornerLayer(scrollViewer);
            if (adornerLayer is not null)
            {
                adornerLayer.Children.Add(indicator);
                AdornerLayer.SetAdornedElement(indicator, scrollViewer);
            }
        }

        private string GetIndicatorGeometry()
        {
            return GetAxes(scrollViewer) switch
            {
                MiddlePanAxes.Vertical => "M10,7 A3,3 0 1,0 10,13 A3,3 0 1,0 10,7 Z M10,0 L4,6 L16,6 Z M10,20 L4,14 L16,14 Z",
                MiddlePanAxes.Both => "M10,7 A3,3 0 1,0 10,13 A3,3 0 1,0 10,7 Z M10,0 L4,6 L16,6 Z M10,20 L4,14 L16,14 Z M0,10 L6,4 L6,16 Z M20,10 L14,4 L14,16 Z",
                _ => "M10,7 A3,3 0 1,0 10,13 A3,3 0 1,0 10,7 Z M0,10 L6,4 L6,16 Z M20,10 L14,4 L14,16 Z"
            };
        }
    }
}
