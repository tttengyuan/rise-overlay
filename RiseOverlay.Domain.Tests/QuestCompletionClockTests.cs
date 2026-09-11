using RiseOverlay.Domain;

public class QuestCompletionClockTests
{
    [Fact]
    public void Successful_result_uses_last_required_target_completion()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(2);
        clock.ConfigureTargets(quest, ["rathian", "khezu"]);
        clock.RecordCompletion(quest, "monster-a", "rathian", 900);
        clock.RecordCompletion(quest, "monster-b", "khezu", 1138.85);

        Assert.Equal(1138.85, clock.ResolveResultElapsed(true, 1260), 2);
    }

    [Fact]
    public void Death_then_capture_for_same_instance_only_consumes_one_duplicate_species_target()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(2);
        clock.ConfigureTargets(quest, ["khezu", "khezu"]);

        clock.RecordCompletion(quest, "monster-a", "khezu", 500);
        clock.RecordCompletion(quest, "monster-a", "khezu", 501);

        Assert.Null(clock.ConfirmedElapsed);
        Assert.Equal(1, clock.CompletedCount(quest, "khezu"));
    }

    [Fact]
    public void Corpse_despawn_does_not_remove_latched_completion()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["khezu"]);
        clock.RecordCompletion(quest, "monster-a", "khezu", 500);

        clock.ConfigureTargets(quest, ["khezu"]);

        Assert.Equal(500, clock.ConfirmedElapsed);
    }

    [Fact]
    public void Late_filled_target_list_invalidates_early_candidate_until_all_targets_finish()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["rathian"]);
        clock.RecordCompletion(quest, "monster-a", "rathian", 500);
        Assert.Equal(500, clock.ConfirmedElapsed);

        clock.ConfigureTargets(quest, ["rathian", "khezu"]);
        Assert.Null(clock.ConfirmedElapsed);

        clock.RecordCompletion(quest, "monster-b", "khezu", 700);
        Assert.Equal(700, clock.ConfirmedElapsed);
    }

    [Fact]
    public void Incomplete_target_list_cannot_confirm_before_authoritative_hint_is_reached()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(2);
        clock.ConfigureTargets(quest, ["rathian"]);
        clock.RecordCompletion(quest, "monster-a", "rathian", 500);

        Assert.Null(clock.ConfirmedElapsed);
    }

    [Fact]
    public void Late_filled_authoritative_hint_invalidates_a_partial_confirmation()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["rathian"]);
        clock.RecordCompletion(quest, "monster-a", "rathian", 500);
        Assert.Equal(500, clock.ConfirmedElapsed);

        clock.ConfigureTargets(quest, ["rathian"], expectedTargetCount: 2);

        Assert.Null(clock.ConfirmedElapsed);
    }

    [Fact]
    public void Stale_completion_from_previous_same_species_quest_is_ignored()
    {
        var clock = new QuestCompletionClock();
        long oldQuest = clock.BeginQuest(1);
        clock.ConfigureTargets(oldQuest, ["khezu"]);

        long newQuest = clock.BeginQuest(1);
        clock.ConfigureTargets(newQuest, ["khezu"]);
        clock.RecordCompletion(oldQuest, "old-monster", "khezu", 500);

        Assert.Null(clock.ConfirmedElapsed);
    }

    [Fact]
    public void Failed_result_ignores_confirmed_completion()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["khezu"]);
        clock.RecordCompletion(quest, "monster-a", "khezu", 900);

        Assert.Equal(1200, clock.ResolveResultElapsed(false, 1200));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(100000)]
    public void Invalid_completion_time_is_ignored(double invalid)
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["khezu"]);
        clock.RecordCompletion(quest, "monster-a", "khezu", invalid);

        Assert.Null(clock.ConfirmedElapsed);
    }

    [Fact]
    public void Confirmed_completion_is_used_when_success_fallback_is_invalid()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["khezu"]);
        clock.RecordCompletion(quest, "monster-a", "khezu", 1138.85);

        Assert.Equal(1138.85, clock.ResolveResultElapsed(true, double.NaN), 2);
    }

    [Fact]
    public void Successful_result_keeps_confirmed_time_when_end_timer_collapsed()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["khezu"]);
        clock.ObserveLiveElapsed(640);
        clock.RecordCompletion(quest, "monster-a", "khezu", 0.2);

        Assert.Equal(640, clock.ConfirmedElapsed);
        Assert.Equal(640, clock.ResolveResultElapsed(true, 0.2));
    }

    [Fact]
    public void Suspicious_confirmed_after_healthy_end_timer_still_uses_fallback()
    {
        var clock = new QuestCompletionClock();
        long quest = clock.BeginQuest(1);
        clock.ConfigureTargets(quest, ["khezu"]);
        clock.RecordCompletion(quest, "monster-a", "khezu", 1260);

        Assert.Equal(1138, clock.ResolveResultElapsed(true, 1138));
    }
}

