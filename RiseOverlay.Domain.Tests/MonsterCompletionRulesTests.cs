using RiseOverlay.Domain;

public class MonsterCompletionRulesTests
{
    [Theory]
    [InlineData(MonsterCompletionState.None, MonsterCompletionState.Slain, true)]
    [InlineData(MonsterCompletionState.None, MonsterCompletionState.Captured, true)]
    [InlineData(MonsterCompletionState.Slain, MonsterCompletionState.Slain, false)]
    [InlineData(MonsterCompletionState.Slain, MonsterCompletionState.Captured, true)]
    [InlineData(MonsterCompletionState.Captured, MonsterCompletionState.Slain, false)]
    [InlineData(MonsterCompletionState.Captured, MonsterCompletionState.Captured, false)]
    [InlineData(MonsterCompletionState.Completed, MonsterCompletionState.Captured, false)]
    [InlineData(MonsterCompletionState.Failed, MonsterCompletionState.Captured, false)]
    public void ShouldApplyMonsterFinish_accepts_first_event_and_capture_upgrade_only(
        MonsterCompletionState existing,
        MonsterCompletionState incoming,
        bool expected)
    {
        Assert.Equal(expected, MonsterCompletionRules.ShouldApplyMonsterFinish(existing, incoming));
    }

    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(100, 100, false)]
    [InlineData(0, 100, true)]
    [InlineData(0.5, 100, false)]
    public void Completion_requires_an_initialized_health_sample(
        double health,
        double maxHealth,
        bool expected)
    {
        Assert.Equal(
            expected,
            MonsterCompletionRules.IsConfirmedFinished(health, maxHealth));
    }
}
