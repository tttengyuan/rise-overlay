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
}
