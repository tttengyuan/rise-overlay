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
    public void Displayed_elapsed_floors_to_confirmed_when_live_timer_collapses()
    {
        Assert.Equal(
            641,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 641,
                incomingGameElapsed: 0.2,
                confirmedObjectiveElapsed: 640.5));
    }

    [Fact]
    public void Displayed_elapsed_keeps_advancing_after_objective_confirmation()
    {
        Assert.Equal(
            206,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 200,
                incomingGameElapsed: 206,
                confirmedObjectiveElapsed: 200));
    }

    [Fact]
    public void Displayed_elapsed_does_not_freeze_minutes_behind_live_timer()
    {
        Assert.Equal(
            809,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 667,
                incomingGameElapsed: 809,
                confirmedObjectiveElapsed: 667));
    }

    [Fact]
    public void Displayed_elapsed_rejects_collapsed_confirmed_stamp()
    {
        Assert.Equal(
            640,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 640,
                incomingGameElapsed: 0.2,
                confirmedObjectiveElapsed: 0.2));
    }

    [Fact]
    public void Displayed_elapsed_rejects_collapsed_confirmed_against_healthy_incoming()
    {
        Assert.Equal(
            640,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 0.02,
                incomingGameElapsed: 640,
                confirmedObjectiveElapsed: 0.02));
    }

    [Fact]
    public void Displayed_elapsed_ignores_sudden_live_timer_collapse()
    {
        Assert.Equal(
            640,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 640,
                incomingGameElapsed: 0,
                confirmedObjectiveElapsed: null));
    }

    [Fact]
    public void Displayed_elapsed_still_follows_a_healthy_live_timer()
    {
        Assert.Equal(
            641.5,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 640,
                incomingGameElapsed: 641.5,
                confirmedObjectiveElapsed: null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Displayed_elapsed_keeps_last_positive_value_when_timer_is_invalid(double incoming)
    {
        Assert.Equal(
            3.25,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 3.25,
                incomingGameElapsed: incoming,
                confirmedObjectiveElapsed: null));
    }

    [Fact]
    public void Displayed_elapsed_sanitizes_invalid_timer_before_first_valid_sample()
    {
        Assert.Equal(
            0,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 0,
                incomingGameElapsed: double.NaN,
                confirmedObjectiveElapsed: null));
    }

    [Fact]
    public void Displayed_elapsed_accepts_authoritative_timer_restart_after_stage_fallback()
    {
        Assert.Equal(
            0.25,
            ScopedDamageRules.StabilizeDisplayedElapsed(
                previousDisplayed: 30,
                incomingGameElapsed: 0.25,
                confirmedObjectiveElapsed: null,
                allowBackwardReset: true));
    }

    [Theory]
    [InlineData(100, 120, 100)]
    [InlineData(0.02, 640, 640)]
    [InlineData(double.NaN, 120, 120)]
    [InlineData(0, 120, 120)]
    [InlineData(double.PositiveInfinity, double.NaN, 1)]
    public void Terminal_elapsed_trusts_resolved_result_and_only_falls_back_when_invalid(
        double resolvedResultElapsed,
        double previousLiveElapsed,
        double expected)
    {
        Assert.Equal(
            expected,
            ScopedDamageRules.ResolveTerminalElapsed(
                resolvedResultElapsed,
                previousLiveElapsed));
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
    [Fact]
    public void MergePeakSnapshot_keeps_prior_damage_when_incoming_collapses()
    {
        var previous = new Dictionary<int, long> { [0] = 12000, [4] = 3000 };
        var incoming = new Dictionary<int, long> { [0] = 0, [4] = 0 };

        (Dictionary<int, long> merged, long total) = ScopedDamageRules.MergePeakSnapshot(
            previous,
            previousTotal: 15000,
            incoming,
            incomingTotal: 0);

        Assert.Equal(12000, merged[0]);
        Assert.Equal(3000, merged[4]);
        Assert.Equal(15000, total);
    }

    [Fact]
    public void MergePeakSnapshot_still_grows_with_new_hits()
    {
        var previous = new Dictionary<int, long> { [0] = 1000 };
        var incoming = new Dictionary<int, long> { [0] = 2500, [4] = 400 };

        (Dictionary<int, long> merged, long total) = ScopedDamageRules.MergePeakSnapshot(
            previous,
            previousTotal: 1000,
            incoming,
            incomingTotal: 2900);

        Assert.Equal(2500, merged[0]);
        Assert.Equal(400, merged[4]);
        Assert.Equal(2900, total);
    }

}
