using HunterPie.Core.Client.Configuration.Overlay.Monster;

namespace HunterPie.Core.Client.Configuration.Overlay;

/// <summary>Rise Overlay: World overlays stay Instantiated for compile but Initialize=false.</summary>
public class MHWOverlayConfig : OverlayConfig
{
    public MHWOverlayConfig()
    {
        DamageMeterWidget.Initialize = false;
        PlayerHudWidget.Initialize = false;
        ActivitiesWidget.Initialize = false;
        ClockWidget.Initialize = false;
        DebugWidget.Initialize = false;
        PrimarySpecializedToolWidget.Initialize = false;
        SecondarySpecializedToolWidget.Initialize = false;
    }

    public override MonsterWidgetConfig BossesWidget { get; set; } = new MHWMonsterWidgetConfig();
}