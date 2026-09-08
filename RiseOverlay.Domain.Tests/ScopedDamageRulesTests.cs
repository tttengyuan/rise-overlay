using RiseOverlay.Domain;

public class ScopedDamageRulesTests
{
    [Fact]
    public void Quest_clock_uses_latest_game_timer_without_monster_health_gating()
    {
        // Monster scanning may temporarily report no alive monsters. The original
        // HunterPie clock still accepts the authoritative game quest timer.
        Assert.Equal(617.5, ScopedDamageRules.ResolveQuestElapsed(617.5));
    }

    [Fact]
    public void HuntDuration_keeps_the_quest_clock_when_target_scope_changes()
    {
        Assert.Equal(125, ScopedDamageRules.HuntDuration(questElapsed: 125));
    }

    [Fact]
    public void First_hit_uses_absolute_quest_time()
    {
        Assert.Equal(
            95,
            ScopedDamageRules.ResolveFirstHitAt(
                currentFirstHitAt: -1,
                scopedDamage: 20,
                questElapsed: 95));
    }

    [Fact]
    public void Current_target_damage_still_uses_original_quest_time_basis()
    {
        Assert.Equal(
            10,
            ScopedDamageRules.CalculateDps(
                scopedDamage: 1000,
                questElapsed: 100,
                joinedAt: 0,
                firstHitAt: 95,
                DpsCalculationMode.RelativeToQuest));
    }

    [Fact]
    public void Target_snapshot_keeps_damage_dealt_before_lock_on()
    {
        var current = new Dictionary<int, long> { [0] = 150, [1] = 80 };

        var snapshot = ScopedDamageRules.UseFullTargetSnapshot(current);

        Assert.Equal(150, snapshot[0]);
        Assert.Equal(80, snapshot[1]);
    }

    [Fact]
    public void Target_snapshot_clamps_invalid_native_damage()
    {
        var snapshot = ScopedDamageRules.UseFullTargetSnapshot(
            new Dictionary<int, long> { [0] = -20 });

        Assert.Equal(0, snapshot[0]);
    }
}
