using System.Windows;
using RiseOverlay.UI.ViewModels;

namespace RiseOverlay.UI.Playground;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MonsterHud.DataContext = MonsterHudViewModel.CreateSampleScornedMagnamalo();
        CombatStack.MonsterHudControl.DataContext = MonsterHudViewModel.CreateSampleScornedMagnamalo();
        CombatStack.DpsPanelControl.DataContext = DpsPanelViewModel.CreateSampleParty4();
        BriefingSingle.DataContext = QuestBriefingViewModel.CreateSampleSingle();
        BriefingFour.DataContext = QuestBriefingViewModel.CreateSampleFour();
    }
}
