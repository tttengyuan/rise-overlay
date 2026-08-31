using System.Windows;
using RiseOverlay.UI.ViewModels;

namespace RiseOverlay.UI.Playground;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MonsterHud.DataContext = MonsterHudViewModel.CreateSampleScornedMagnamalo();
    }
}
