using RiseOverlay.Domain;

public class TargetSelectionRulesTests
{
    [Fact]
    public void Select_prefers_the_monster_that_just_became_locked_when_scan_temporarily_marks_two()
    {
        var oldTarget = new Candidate("河童蛙", IsAliveLocked: true);
        var newlyLocked = new Candidate("大名盾蟹", IsAliveLocked: true);

        Candidate? selected = TargetSelectionRules.Select(
            [oldTarget, newlyLocked],
            current: oldTarget,
            promoted: newlyLocked,
            candidate => candidate.IsAliveLocked);

        Assert.Same(newlyLocked, selected);
    }

    [Fact]
    public void Select_keeps_current_target_during_unrelated_hp_updates()
    {
        var current = new Candidate("大名盾蟹", IsAliveLocked: true);
        var stale = new Candidate("河童蛙", IsAliveLocked: true);

        Candidate? selected = TargetSelectionRules.Select(
            [stale, current],
            current,
            promoted: null,
            candidate => candidate.IsAliveLocked);

        Assert.Same(current, selected);
    }

    [Fact]
    public void Select_does_not_guess_by_collection_order_when_two_targets_are_ambiguous()
    {
        var first = new Candidate("河童蛙", IsAliveLocked: true);
        var second = new Candidate("大名盾蟹", IsAliveLocked: true);

        Candidate? selected = TargetSelectionRules.Select(
            [first, second],
            current: null,
            promoted: null,
            candidate => candidate.IsAliveLocked);

        Assert.Null(selected);
    }

    private sealed record Candidate(string Name, bool IsAliveLocked);
}
