using System.Runtime.InteropServices;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions.Quest;

[StructLayout(LayoutKind.Explicit)]
public struct MHRAnomalyQuestDataStructure
{
    [FieldOffset(0x14)] public int Id;
    [FieldOffset(0x18)] public int Level;

    /// <summary>
    /// <c>_HuntTargetNum</c> — confirmed by single-target dumps (700056/700065/700068: UI「讨伐1头」→ +0x38=1).
    /// +0x24 is often cart lives (<c>_QuestLife</c>), not hunt count.
    /// </summary>
    [FieldOffset(0x38)] public int HuntTargetNum;
}
