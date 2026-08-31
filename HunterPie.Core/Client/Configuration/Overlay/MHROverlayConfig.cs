using HunterPie.Core.Client.Configuration.Overlay.Monster;

namespace HunterPie.Core.Client.Configuration.Overlay;

public class MHROverlayConfig : OverlayConfig
{
    /// <summary>
    /// Legacy HunterPie monster widget. Default Initialize=false for Rise Overlay
    /// (see <see cref="MHRMonsterWidgetConfig"/>); compact HUD uses <see cref="RiseCompactMonsterWidget"/>.
    /// </summary>
    public override MonsterWidgetConfig BossesWidget { get; set; } = new MHRMonsterWidgetConfig();

    public RiseCompactMonsterWidgetConfig RiseCompactMonsterWidget { get; set; } = new();
}