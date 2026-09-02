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
    /// </summary>
    public string[] TargetMonsterRefs;
}
