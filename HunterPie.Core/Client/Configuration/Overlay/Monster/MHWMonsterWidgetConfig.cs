using HunterPie.Core.Client.Configuration.Enums;
using HunterPie.Core.Game.Data.Repository;
using HunterPie.Core.Settings.Types;
using System.Linq;

namespace HunterPie.Core.Client.Configuration.Overlay.Monster;

public class MHWMonsterWidgetConfig : MonsterWidgetConfig
{
    public MHWMonsterWidgetConfig()
    {
        // Rise Overlay: World surface disabled by default (code kept for compile).
        Initialize = false;
    }

    public override MonsterDetailsConfiguration Details { get; set; } = new MonsterDetailsConfiguration
    {
        AllowedAilments = new(MonsterAilmentRepository.FindAllBy(GameType.World).Select(it => it.Id))
    };
}