using System.ComponentModel;
using System.Windows;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.UI.Overlay.Enums;
using HunterPie.UI.Overlay.ViewModels;
using RiseOverlay.UI.ViewModels;

namespace RiseOverlay.UI.Overlay;

public sealed class RiseCompactMonsterViewModel : WidgetViewModel
{
    public RiseCompactMonsterViewModel(RiseCompactMonsterWidgetConfig settings)
        : base(settings, "Rise血条", WidgetType.ClickThrough)
    {
        Config = settings;
        SyncDisplayToggles();
        Config.ShowParts.PropertyChanged += OnDisplayToggleChanged;
        Config.ShowAilments.PropertyChanged += OnDisplayToggleChanged;
        Config.ShowDps.PropertyChanged += OnDisplayToggleChanged;
    }

    public RiseCompactMonsterWidgetConfig Config { get; }

    public MonsterHudViewModel MonsterHud { get; } = new();

    public DpsPanelViewModel DpsPanel { get; } = new();

    public QuestBriefingViewModel QuestBriefing { get; } = new();

    public Visibility DpsVisibility
    {
        get;
        private set => SetValue(ref field, value);
    } = Visibility.Visible;

    private void OnDisplayToggleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Value" or null)
            SyncDisplayToggles();
    }

    private void SyncDisplayToggles()
    {
        MonsterHud.ShowParts = Config.ShowParts.Value;
        MonsterHud.ShowAilments = Config.ShowAilments.Value;
        DpsVisibility = Config.ShowDps.Value ? Visibility.Visible : Visibility.Collapsed;
    }

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

    /// <summary>Shown when attached but not yet in briefing/combat — proves overlay is on-screen.</summary>
    public Visibility AttachedVisibility
    {
        get;
        set => SetValue(ref field, value);
    } = Visibility.Visible;

    public Visibility ContentVisibility
    {
        get;
        set => SetValue(ref field, value);
    } = Visibility.Visible;
}

