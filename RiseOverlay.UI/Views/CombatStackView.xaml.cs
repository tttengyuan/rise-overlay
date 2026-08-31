using System.Windows.Controls;

namespace RiseOverlay.UI.Views;

public partial class CombatStackView : UserControl
{
    public CombatStackView()
    {
        InitializeComponent();
    }

    public MonsterHudView MonsterHudControl => MonsterHud;

    public DpsPanelView DpsPanelControl => DpsPanel;
}
