using HunterPie.Core.Client.Configuration.Enums;
using HunterPie.Core.Game.Data.Repository;
using HunterPie.Core.Settings.Types;
using System.Linq;

namespace HunterPie.Core.Client.Configuration.Overlay.Monster;

public class MHRMonsterWidgetConfig : MonsterWidgetConfig
{
    public MHRMonsterWidgetConfig()
    {
        // Rise Overlay: compact Monster HUD replaces the legacy Monster Widget.
        // Persisted client configs under %AppData%/HunterPie (or ClientConfig path) may still
        // have Initialize=true until the user resets BossesWidget / regenerates defaults.
        Initialize = false;
    }

    public override MonsterDetailsConfiguration Details { get; set; } = new MonsterDetailsConfiguration
    {
        AllowedAilments = new(MonsterAilmentRepository.FindAllBy(GameType.Rise).Select(it => it.Id))
    };
}