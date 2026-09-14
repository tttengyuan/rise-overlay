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
    public void Completion_requires_an_initialized_health_sample(
        double health,
        double maxHealth,
        bool expected)
    {
        Assert.Equal(
            expected,
            MonsterCompletionRules.IsConfirmedFinished(health, maxHealth));
    }

    /// <summary>
    /// Rise leaves sub-1 float residue in memory after a slay, so a fractional sample is a
    /// corpse rather than a living monster. This deliberately mirrors
    /// <see cref="MonsterHealthDisplay.ForHud"/>: the corpse-scan fallback in
    /// QuestBriefingController already collapses the same values through IsMonsterAlive, so
    /// tightening this to <c>health &lt;= 0</c> would make the two disagree and drop real
    /// completions.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.999)]
    public void Sub_one_health_residue_counts_as_finished(double health)
    {
        Assert.True(MonsterCompletionRules.IsConfirmedFinished(health, 100));
    }

    /// <summary>Exact 1 HP is a living finisher and must never complete an objective.</summary>
    [Fact]
    public void One_health_point_is_not_finished()
    {
        Assert.False(MonsterCompletionRules.IsConfirmedFinished(1, 100));
    }
}
