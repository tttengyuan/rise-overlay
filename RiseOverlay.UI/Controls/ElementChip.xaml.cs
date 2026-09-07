using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RiseOverlay.Domain;
using RiseOverlay.UI.Themes;

namespace RiseOverlay.UI.Controls;

public partial class ElementChip : UserControl
{
    public static readonly DependencyProperty ElementProperty = DependencyProperty.Register(
        nameof(Element),
        typeof(ElementId),
        typeof(ElementChip),
        new PropertyMetadata(ElementId.Fire, OnVisualPropertyChanged));

    public static readonly DependencyProperty DimmedProperty = DependencyProperty.Register(
        nameof(Dimmed),
        typeof(bool),
        typeof(ElementChip),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty SmallProperty = DependencyProperty.Register(
        nameof(Small),
        typeof(bool),
        typeof(ElementChip),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty HighlightedProperty = DependencyProperty.Register(
        nameof(Highlighted),
        typeof(bool),
        typeof(ElementChip),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    public ElementChip()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyVisualState();
        RiseThemeService.ThemeChanged += OnThemeOrMotionChanged;
        Unloaded += (_, _) =>
        {
            RiseThemeService.ThemeChanged -= OnThemeOrMotionChanged;
        };
        ApplyVisualState();
    }

    public ElementId Element
    {
        get => (ElementId)GetValue(ElementProperty);
        set => SetValue(ElementProperty, value);
    }

    public bool Dimmed
    {
        get => (bool)GetValue(DimmedProperty);
        set => SetValue(DimmedProperty, value);
    }

    public bool Small
    {
        get => (bool)GetValue(SmallProperty);
        set => SetValue(SmallProperty, value);
    }

    public bool Highlighted
    {
        get => (bool)GetValue(HighlightedProperty);
        set => SetValue(HighlightedProperty, value);
    }

    private void OnThemeOrMotionChanged() =>
        Dispatcher.BeginInvoke(ApplyVisualState);

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ElementChip chip)
            chip.ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (Root is null || LabelText is null)
            return;

        string brushKey = Element switch
        {
            ElementId.Water => "Brushes.Element.Water",
            ElementId.Thunder => "Brushes.Element.Thunder",
            ElementId.Ice => "Brushes.Element.Ice",
            ElementId.Dragon => "Brushes.Element.Dragon",
            _ => "Brushes.Element.Fire",
        };

        Root.Background = TryFindResource(brushKey) as Brush
                          ?? CreateFrozenBrush(0xB8, 0x5C, 0x5C);
        LabelText.Foreground = TryFindResource("Brushes.Element.Foreground") as Brush
                               ?? Brushes.White;
        LabelText.Text = Element switch
        {
            ElementId.Water => "水",
            ElementId.Thunder => "雷",
            ElementId.Ice => "冰",
            ElementId.Dragon => "龙",
            _ => "火",
        };

        Opacity = Dimmed ? 0.38 : 1.0;

        if (Small)
        {
            Height = 15;
            MinHeight = 15;
            Root.Padding = new Thickness(3, 0, 3, 0);
            Root.MinWidth = 14;
            Root.CornerRadius = new CornerRadius(1);
            LabelText.FontSize = 9;
        }
        else
        {
            Height = 22;
            MinHeight = 22;
            Root.Padding = new Thickness(6, 2, 6, 2);
            Root.MinWidth = 22;
            Root.CornerRadius = new CornerRadius(3);
            LabelText.FontSize = 12;
        }

        Root.RenderTransform = Transform.Identity;
        Root.BorderThickness = Highlighted && !Dimmed ? new Thickness(1) : new Thickness(0);
        Root.BorderBrush = Highlighted && !Dimmed
            ? TryFindResource("Brushes.AccentTeal") as Brush ?? Brushes.Cyan
            : null;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
