using HunterPie.Core.Game.Entity.Game.Quest;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions.Quest;

public struct MHRQuestData
{
    public int Id;
    public QuestLevel Level;
    public int Stars;

    /// <summary>
    /// Target monster static ids for briefing (e.g. <c>monster_037_00</c>).
    /// Populated for anomaly investigations when EmTypes can be read from memory.
    /// Same species may appear more than once (multi-target hunts).
    /// </summary>
    public string[] TargetMonsterRefs;

    /// <summary>
    /// Expected hunt target count from <c>_HuntTargetNum</c> (or resolved refs).
    /// Useful when EmTypes are not readable yet but the quest board already shows N heads.
    /// </summary>
    public int TargetCountHint;
}
