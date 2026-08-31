using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RiseOverlay.Domain;

namespace RiseOverlay.UI.Controls;

public partial class ElementChip : UserControl
{
    private static readonly Brush FireBrush = CreateFrozenBrush(0xB8, 0x5C, 0x5C);
    private static readonly Brush WaterBrush = CreateFrozenBrush(0x4F, 0x87, 0xA6);
    private static readonly Brush ThunderBrush = CreateFrozenBrush(0xB0, 0x9A, 0x4A);
    private static readonly Brush IceBrush = CreateFrozenBrush(0x5F, 0x97, 0xA8);
    private static readonly Brush DragonBrush = CreateFrozenBrush(0x7D, 0x6A, 0x96);

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

    public ElementChip()
    {
        InitializeComponent();
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

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ElementChip chip)
            chip.ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (Root is null || LabelText is null)
            return;

        (Root.Background, LabelText.Text) = Element switch
        {
            ElementId.Water => (WaterBrush, "水"),
            ElementId.Thunder => (ThunderBrush, "雷"),
            ElementId.Ice => (IceBrush, "冰"),
            ElementId.Dragon => (DragonBrush, "龙"),
            _ => (FireBrush, "火"),
        };

        Opacity = Dimmed ? 0.4 : 1.0;

        if (Small)
        {
            Root.Padding = new Thickness(3, 0, 3, 0);
            Root.MinWidth = 14;
            Root.CornerRadius = new CornerRadius(1);
            LabelText.FontSize = 10;
        }
        else
        {
            Root.Padding = new Thickness(4, 1, 4, 1);
            Root.MinWidth = 18;
            Root.CornerRadius = new CornerRadius(2);
            LabelText.FontSize = 11;
        }
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
