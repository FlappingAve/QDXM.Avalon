using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace QDXM.Avalon.Controls;

public partial class DestinationPreviewBox : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<DestinationPreviewBox, string>(
            nameof(Text),
            string.Empty);

    public static readonly StyledProperty<double> PreviewFontSizeProperty =
        AvaloniaProperty.Register<DestinationPreviewBox, double>(
            nameof(PreviewFontSize),
            12d);

    public static readonly StyledProperty<Thickness> TextMarginProperty =
        AvaloniaProperty.Register<DestinationPreviewBox, Thickness>(
            nameof(TextMargin),
            new Thickness(0));

    public static readonly StyledProperty<double> MaxPreviewHeightProperty =
        AvaloniaProperty.Register<DestinationPreviewBox, double>(
            nameof(MaxPreviewHeight),
            double.PositiveInfinity);

    public static readonly StyledProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty =
        AvaloniaProperty.Register<DestinationPreviewBox, ScrollBarVisibility>(
            nameof(VerticalScrollBarVisibility),
            ScrollBarVisibility.Hidden);

    public DestinationPreviewBox()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double PreviewFontSize
    {
        get => GetValue(PreviewFontSizeProperty);
        set => SetValue(PreviewFontSizeProperty, value);
    }

    public Thickness TextMargin
    {
        get => GetValue(TextMarginProperty);
        set => SetValue(TextMarginProperty, value);
    }

    public double MaxPreviewHeight
    {
        get => GetValue(MaxPreviewHeightProperty);
        set => SetValue(MaxPreviewHeightProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }
}
