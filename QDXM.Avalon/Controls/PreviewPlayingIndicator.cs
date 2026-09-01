using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace QDXM.Avalon.Controls;

public sealed class PreviewPlayingIndicator : Control
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<PreviewPlayingIndicator, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<PreviewPlayingIndicator, IBrush?>(nameof(BarBrush));

    private readonly DispatcherTimer animationTimer;
    private double phase;
    private bool isAttached;

    public PreviewPlayingIndicator()
    {
        Width = 18;
        Height = 18;
        animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };
        animationTimer.Tick += (_, _) =>
        {
            phase += 0.55;
            InvalidateVisual();
        };
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public IBrush? BarBrush
    {
        get => GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        isAttached = true;
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        isAttached = false;
        animationTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            UpdateTimer();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var brush = BarBrush ?? Brushes.White;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var barWidth = Math.Max(2, width / 5);
        var gap = Math.Max(2, (width - (barWidth * 3)) / 2);
        var left = (width - (barWidth * 3) - (gap * 2)) / 2;
        var baseHeights = IsPlaying
            ? new[]
            {
                0.35 + 0.45 * Wave(phase),
                0.35 + 0.45 * Wave(phase + 1.7),
                0.35 + 0.45 * Wave(phase + 3.1)
            }
            : [0.72, 0.42, 0.58];

        for (var index = 0; index < baseHeights.Length; index++)
        {
            var barHeight = Math.Max(4, height * baseHeights[index]);
            var x = left + index * (barWidth + gap);
            var y = (height - barHeight) / 2;
            context.DrawRectangle(
                brush,
                null,
                new Rect(x, y, barWidth, barHeight),
                radiusX: barWidth / 2,
                radiusY: barWidth / 2);
        }
    }

    private void UpdateTimer()
    {
        if (isAttached && IsPlaying)
        {
            animationTimer.Start();
            return;
        }

        animationTimer.Stop();
    }

    private static double Wave(double value)
    {
        return (Math.Sin(value) + 1) / 2;
    }
}
