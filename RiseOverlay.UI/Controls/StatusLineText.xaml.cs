using System.Windows;
using System.Windows.Controls;

namespace RiseOverlay.UI.Controls;

public partial class StatusLineText : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusLineText),
        new PropertyMetadata(string.Empty));

    public StatusLineText()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
