using HunterPie.Core.Game.Entity.Game.Quest;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public static class MHRQuestTimerRules
{
    // Rise quests are normally at most 50 minutes. Keep a generous ceiling so a stale or
    // corrupt memory read can never overflow TimeSpan.FromSeconds in QuestEndEventArgs.
    private const float MaxReasonableQuestDurationSeconds = 24 * 60 * 60;

    public static bool IsTimerReset(float previousElapsed, float nextElapsed)
        => IsValidElapsed(previousElapsed, allowZero: true)
           && IsValidElapsed(nextElapsed, allowZero: true)
           && nextElapsed < previousElapsed - 5;

    /// <summary>
    /// MHRPlayer encodes hunting maps as HuntingId + 200 and reserves 5 for training.
    /// Deriving this from one StageId read avoids mixing independently changing player fields.
    /// </summary>
    public static bool IsHuntOrTrainingStage(int stageId)
        => stageId == 5 || stageId >= 200;

    /// <summary>
    /// Both inputs ultimately read Rise's quest elapsed field. Prefer the value read together
    /// with the terminal state and use the cached scan only when that read is invalid. The
    /// custom Rise panel separately freezes the earlier objective-completion timestamp.
    /// </summary>
    public static float ResolveEndElapsed(
        QuestStatus status,
        float questStructureElapsed,
        float globalElapsed)
    {
        bool isTerminal = status is QuestStatus.Success or QuestStatus.Fail or QuestStatus.Quit;
        if (isTerminal && IsValidElapsed(questStructureElapsed, allowZero: false))
        {
            // Rise often collapses structure TimeElapsed to ~0 on the success frame while
            // the cached live timer (or a recovered later read) is still healthy.
            if (questStructureElapsed <= 5
                && IsValidElapsed(globalElapsed, allowZero: false)
                && globalElapsed > questStructureElapsed + 5)
                return globalElapsed;

            return questStructureElapsed;
        }

        return IsValidElapsed(globalElapsed, allowZero: true)
            ? globalElapsed
            : 0;
    }

    /// <summary>
    /// Uses Rise's timer once the caller has verified that it belongs to the current stage.
    /// During its transient zero/stale window, advance from the current stage-entry clock
    /// instead of reusing a timestamp from the previous map.
    /// </summary>
    public static float ResolveLiveElapsed(
        float rawElapsed,
        float previousElapsed,
        float currentStageElapsed,
        bool isHuntOrTraining,
        bool hasObservedRawElapsed,
        bool allowRawElapsed)
    {
        if (!isHuntOrTraining)
            return 0;
        if (allowRawElapsed && IsValidElapsed(rawElapsed, allowZero: false))
            return rawElapsed;

        if (hasObservedRawElapsed
            && IsValidElapsed(previousElapsed, allowZero: false))
            return previousElapsed;

        float previous = IsValidElapsed(previousElapsed, allowZero: true)
            ? previousElapsed
            : 0;
        float stage = IsValidElapsed(currentStageElapsed, allowZero: true)
            ? currentStageElapsed
            : 0;
        return Math.Max(previous, stage);
    }

    /// <summary>
    /// Overlay restart / stage resync mid-hunt samples the same QUEST_TIMER repeatedly, so
    /// "changed since entry" never trips and the wall-clock stage fallback sticks at ~0:02
    /// while 剩余 still tracks the real quest. Trust a raw timer that is already clearly
    /// ahead of the stage fallback.
    /// </summary>
    public static bool ShouldTrustRawElapsed(
        bool awaitingFreshRawElapsed,
        bool rawChangedSinceStageEntry,
        float rawElapsed,
        float stageElapsed)
    {
        if (!awaitingFreshRawElapsed || rawChangedSinceStageEntry)
            return true;

        return IsValidElapsed(rawElapsed, allowZero: false)
               && rawElapsed > 5
               && rawElapsed > stageElapsed + 5;
    }

    /// <summary>
    /// Native hunt stats must survive overlay reconnect and same-quest area transitions.
    /// Only clear when leaving a hunt map, or when entering a hunt from a non-hunt stage
    /// (village → quest). First observation mid-hunt must not wipe in-progress damage.
    /// </summary>
    public static bool ShouldClearHuntStatisticsOnStageChange(
        int previousStageId,
        int nextStageId,
        bool hasObservedStage)
    {
        bool nextHunt = IsHuntOrTrainingStage(nextStageId);
        if (!hasObservedStage)
            return !nextHunt;

        bool previousHunt = IsHuntOrTrainingStage(previousStageId);
        if (!nextHunt)
            return true;

        return !previousHunt;
    }

    private static bool IsValidElapsed(float elapsed, bool allowZero)
    {
        return float.IsFinite(elapsed)
               && (allowZero ? elapsed >= 0 : elapsed > 0)
               && elapsed <= MaxReasonableQuestDurationSeconds;
    }
}
