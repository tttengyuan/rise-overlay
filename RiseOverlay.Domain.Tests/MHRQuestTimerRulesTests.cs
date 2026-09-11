using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public class MHRQuestTimerRulesTests
{
    [Theory]
    [InlineData(QuestStatus.Success, 1138.85f, 1144.0f, 1138.85f)]
    [InlineData(QuestStatus.Fail, 1200.25f, 1205.0f, 1200.25f)]
    [InlineData(QuestStatus.Quit, 421.5f, 426.0f, 421.5f)]
    public void ResolveEndElapsed_prefers_quest_structure_for_terminal_results(
        QuestStatus status,
        float questStructureElapsed,
        float globalElapsed,
        float expected)
    {
        Assert.Equal(
            expected,
            MHRQuestTimerRules.ResolveEndElapsed(status, questStructureElapsed, globalElapsed),
            precision: 2);
    }

    [Theory]
    [InlineData(QuestStatus.None, 1138.85f, 1144.0f)]
    [InlineData(QuestStatus.Success, 0f, 1144.0f)]
    [InlineData(QuestStatus.Success, -1f, 1144.0f)]
    public void ResolveEndElapsed_falls_back_to_global_timer_when_structure_is_not_authoritative(
        QuestStatus status,
        float questStructureElapsed,
        float globalElapsed)
    {
        Assert.Equal(
            globalElapsed,
            MHRQuestTimerRules.ResolveEndElapsed(status, questStructureElapsed, globalElapsed));
    }

    [Fact]
    public void ResolveEndElapsed_rejects_collapsed_structure_timer()
    {
        Assert.Equal(
            640f,
            MHRQuestTimerRules.ResolveEndElapsed(
                QuestStatus.Success,
                0.02f,
                640f));
    }

    [Fact]
    public void ResolveEndElapsed_rejects_finite_structure_memory_garbage()
    {
        Assert.Equal(
            1144f,
            MHRQuestTimerRules.ResolveEndElapsed(
                QuestStatus.Success,
                float.MaxValue,
                1144f));
    }

    [Fact]
    public void ResolveEndElapsed_never_returns_an_unsafe_global_fallback()
    {
        Assert.Equal(
            0f,
            MHRQuestTimerRules.ResolveEndElapsed(
                QuestStatus.Success,
                float.NaN,
                float.MaxValue));
    }

    [Theory]
    [InlineData(12.34f, 10f, 2f, true, true, true, 12.34f)]
    [InlineData(0f, 0f, 0.05f, true, false, true, 0.05f)]
    [InlineData(0f, 10f, 10.05f, true, true, true, 10f)]
    [InlineData(12.34f, 10f, 10.05f, false, true, true, 0f)]
    [InlineData(float.NaN, 0f, 2f, true, false, true, 2f)]
    [InlineData(900f, 0f, 0.05f, true, false, false, 0.05f)]
    public void ResolveLiveElapsed_uses_game_time_then_current_stage_fallback(
        float rawElapsed,
        float previousElapsed,
        float stageElapsed,
        bool isHuntOrTraining,
        bool hasObservedRawElapsed,
        bool allowRawElapsed,
        float expected)
    {
        Assert.Equal(
            expected,
            MHRQuestTimerRules.ResolveLiveElapsed(
                rawElapsed,
                previousElapsed,
                stageElapsed,
                isHuntOrTraining,
                hasObservedRawElapsed,
                allowRawElapsed),
            precision: 2);
    }

    [Theory]
    [InlineData(30f, 0.25f, true)]
    [InlineData(30f, 29.9f, false)]
    [InlineData(0f, 10f, false)]
    public void Timer_reset_only_means_a_large_backward_jump(
        float previous,
        float next,
        bool expected)
    {
        Assert.Equal(expected, MHRQuestTimerRules.IsTimerReset(previous, next));
    }

    [Theory]
    [InlineData(false, false, 110f, 2f, true)]
    [InlineData(true, true, 110f, 2f, true)]
    [InlineData(true, false, 110f, 2f, true)]
    [InlineData(true, false, 3f, 2f, false)]
    [InlineData(true, false, 110f, 108f, false)]
    public void Mid_hunt_raw_timer_is_trusted_without_waiting_for_a_delta(
        bool awaitingFresh,
        bool rawChanged,
        float rawElapsed,
        float stageElapsed,
        bool expected)
    {
        Assert.Equal(
            expected,
            MHRQuestTimerRules.ShouldTrustRawElapsed(
                awaitingFresh,
                rawChanged,
                rawElapsed,
                stageElapsed));
    }

    [Theory]
    // First observation mid-hunt must keep native stats.
    [InlineData(0, 201, false, false)]
    // Village → hunt starts a fresh counter set.
    [InlineData(1, 201, true, true)]
    // Hunt → village clears.
    [InlineData(201, 1, true, true)]
    // Same-quest area transition keeps counters.
    [InlineData(201, 202, true, false)]
    public void Hunt_statistics_clear_only_on_village_boundaries(
        int previousStageId,
        int nextStageId,
        bool hasObservedStage,
        bool expectedClear)
    {
        Assert.Equal(
            expectedClear,
            MHRQuestTimerRules.ShouldClearHuntStatisticsOnStageChange(
                previousStageId,
                nextStageId,
                hasObservedStage));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(199, false)]
    [InlineData(5, true)]
    [InlineData(200, true)]
    [InlineData(215, true)]
    public void Hunt_state_is_derived_from_the_atomic_stage_id_snapshot(
        int stageId,
        bool expected)
    {
        Assert.Equal(expected, MHRQuestTimerRules.IsHuntOrTrainingStage(stageId));
    }
}
