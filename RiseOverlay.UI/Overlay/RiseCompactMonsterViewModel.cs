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

    public bool HasActiveMonster
    {
        get;
        set
        {
            if (field == value)
                return;
            SetValue(ref field, value);
            // Nudge dependent property for bindings
            var visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ContentVisibility = visibility;
        }
    }

    public Visibility ContentVisibility
    {
        get;
        private set => SetValue(ref field, value);
    } = Visibility.Collapsed;
}
