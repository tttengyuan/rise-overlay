using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public class QuestScanRulesTests
{
    [Fact]
    public void Idle_memory_with_no_quest_type_is_not_probed()
    {
        Assert.False(MHRQuestScanRules.ShouldReadQuestData(hasQuestType: false));
    }

    [Fact]
    public void Accepted_or_active_quest_with_type_is_probed()
    {
        Assert.True(MHRQuestScanRules.ShouldReadQuestData(hasQuestType: true));
    }

    [Fact]
    public void Release_scanning_does_not_write_probe_dumps()
    {
        Assert.False(MHRQuestScanRules.ShouldWriteProbeDump(diagnosticsEnabled: false));
    }

    [Fact]
    public void Identical_target_resolution_is_not_logged_every_scan()
    {
        const string signature = "normal:700198:monster_089_00";

        Assert.False(MHRQuestScanRules.ShouldLogTargetResolution(signature, signature));
        Assert.True(MHRQuestScanRules.ShouldLogTargetResolution(null, signature));
    }
}

public class MHRDamagePollingRulesTests
{
    [Fact]
    public void Keeps_polling_last_target_after_lock_on_drops_for_death_animation()
    {
        nint sticky = 0x1234;

        IReadOnlyList<nint> selected = MHRDamagePollingRules.SelectTargets(
            focused: [],
            sticky,
            present: new HashSet<nint> { sticky });

        Assert.Equal([sticky], selected);
    }

    [Fact]
    public void Newly_focused_target_replaces_sticky_target()
    {
        nint oldTarget = 0x1234;
        nint nextTarget = 0x5678;

        IReadOnlyList<nint> selected = MHRDamagePollingRules.SelectTargets(
            focused: [nextTarget],
            sticky: oldTarget,
            present: new HashSet<nint> { oldTarget, nextTarget });

        Assert.Equal([nextTarget], selected);
    }
}
