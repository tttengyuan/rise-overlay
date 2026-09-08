using HunterPie.Core.Game.Entity.Game.Quest;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public static class MHRQuestTimerRules
{
    // Rise quests are normally at most 50 minutes. Keep a generous ceiling so a stale or
    // corrupt memory read can never overflow TimeSpan.FromSeconds in QuestEndEventArgs.
    private const float MaxReasonableQuestDurationSeconds = 24 * 60 * 60;

    /// <summary>
    /// Rise's global quest timer keeps advancing during the completion transition. The quest
    /// structure timer is the value used by the in-game result screen and freezes at completion.
    /// </summary>
    public static float ResolveEndElapsed(
        QuestStatus status,
        float questStructureElapsed,
        float globalElapsed)
    {
        bool isTerminal = status is QuestStatus.Success or QuestStatus.Fail or QuestStatus.Quit;
        if (isTerminal && IsValidElapsed(questStructureElapsed, allowZero: false))
            return questStructureElapsed;

        return IsValidElapsed(globalElapsed, allowZero: true)
            ? globalElapsed
            : 0;
    }

    private static bool IsValidElapsed(float elapsed, bool allowZero)
    {
        return float.IsFinite(elapsed)
               && (allowZero ? elapsed >= 0 : elapsed > 0)
               && elapsed <= MaxReasonableQuestDurationSeconds;
    }
}
