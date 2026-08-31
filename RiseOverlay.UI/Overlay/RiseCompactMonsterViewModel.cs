using System.Windows;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.UI.Overlay.Enums;
using HunterPie.UI.Overlay.ViewModels;
using RiseOverlay.UI.ViewModels;

namespace RiseOverlay.UI.Overlay;

public sealed class RiseCompactMonsterViewModel(RiseCompactMonsterWidgetConfig settings)
    : WidgetViewModel(settings, "Rise Compact Monster", WidgetType.ClickThrough)
{
    public RiseCompactMonsterWidgetConfig Config { get; } = settings;

    public MonsterHudViewModel MonsterHud { get; } = new();

    public DpsPanelViewModel DpsPanel { get; } = new();

    public QuestBriefingViewModel QuestBriefing { get; } = new();

    /// <summary>
    /// True when a live large monster is bound for combat HUD data (not host visibility).
    /// Host visibility is owned by <see cref="Integration.QuestBriefingController"/>.
    /// </summary>
    public bool HasActiveMonster
    {
        get;
        set
        {
            if (field == value)
                return;
            SetValue(ref field, value);
        }
    }

    public bool ShowBriefing
    {
        get;
        set
        {
            if (field == value)
                return;
            SetValue(ref field, value);
        }
    }

    public bool ShowCombat
    {
        get;
        set
        {
            if (field == value)
                return;
            SetValue(ref field, value);
        }
    }

    public Visibility BriefingVisibility
    {
        get;
        set => SetValue(ref field, value);
    } = Visibility.Collapsed;

    public Visibility CombatVisibility
    {
        get;
        set => SetValue(ref field, value);
    } = Visibility.Collapsed;

    public Visibility ContentVisibility
    {
        get;
        set => SetValue(ref field, value);
    } = Visibility.Collapsed;
}
