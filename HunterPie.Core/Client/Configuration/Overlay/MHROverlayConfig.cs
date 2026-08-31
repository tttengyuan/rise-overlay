using HunterPie.Core.Client.Configuration.Overlay.Monster;

namespace HunterPie.Core.Client.Configuration.Overlay;

public class MHROverlayConfig : OverlayConfig
{
    public MHROverlayConfig()
    {
        // Rise Overlay: only compact monster HUD is on by default; keep other widgets off.
        WirebugWidget.Initialize = false;
        ActivitiesWidget.Initialize = false;
        PlayerHudWidget.Initialize = false;
        ChatWidget.Initialize = false;
        ClockWidget.Initialize = false;
        DebugWidget.Initialize = false;
        InsectGlaiveWidget.Initialize = false;
        ChargeBladeWidget.Initialize = false;
        DualBladesWidget.Initialize = false;
        SwitchAxeWidget.Initialize = false;
        LongSwordWidget.Initialize = false;
        PrimarySpecializedToolWidget.Initialize = false;
        SecondarySpecializedToolWidget.Initialize = false;
    }

    /// <summary>
    /// Legacy HunterPie monster widget. Default Initialize=false for Rise Overlay
    /// (see <see cref="MHRMonsterWidgetConfig"/>); compact HUD uses <see cref="RiseCompactMonsterWidget"/>.
    /// </summary>
    public override MonsterWidgetConfig BossesWidget { get; set; } = new MHRMonsterWidgetConfig();

    /// <summary>
    /// Legacy chart Damage Meter. Default Initialize=false; compact DPS lives in CombatStack.
    /// </summary>
    public override DamageMeterWidgetConfig DamageMeterWidget { get; set; } = new MHRDamageMeterWidgetConfig();

    public RiseCompactMonsterWidgetConfig RiseCompactMonsterWidget { get; set; } = new();
}
