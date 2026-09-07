using RiseOverlay.Domain;

public class ScopedDamageRulesTests
{
    [Fact]
    public void HuntDuration_keeps_the_quest_clock_when_target_scope_changes()
    {
        Assert.Equal(125, ScopedDamageRules.HuntDuration(questElapsed: 125));
    }

    [Fact]
    public void First_hit_for_a_new_target_uses_absolute_quest_time()
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
    public void SubtractBaseline_starts_a_new_target_from_zero()
    {
        var current = new Dictionary<int, long> { [0] = 150, [1] = 80 };
        var baseline = new Dictionary<int, long> { [0] = 150, [1] = 80 };

        var delta = ScopedDamageRules.SubtractBaseline(current, baseline);

        Assert.Equal(0, delta[0]);
        Assert.Equal(0, delta[1]);
    }

    [Fact]
    public void SubtractBaseline_counts_only_damage_after_target_lock()
    {
        var current = new Dictionary<int, long> { [0] = 225, [1] = 120 };
        var baseline = new Dictionary<int, long> { [0] = 150, [1] = 80 };

        var delta = ScopedDamageRules.SubtractBaseline(current, baseline);

        Assert.Equal(75, delta[0]);
        Assert.Equal(40, delta[1]);
    }

    [Fact]
    public void SubtractBaseline_clamps_native_counter_reset_to_zero()
    {
        var delta = ScopedDamageRules.SubtractBaseline(
            new Dictionary<int, long> { [0] = 20 },
            new Dictionary<int, long> { [0] = 100 });

        Assert.Equal(0, delta[0]);
    }
}
