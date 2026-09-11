using RiseOverlay.Domain;
using RiseOverlay.UI.ViewModels;

public class ViewModelCollectionReuseTests
{
    [Fact]
    public void MonsterHud_reuses_part_collection_and_matching_row()
    {
        var vm = new MonsterHudViewModel();
        vm.ApplyDto(MonsterDto(500));
        var collection = vm.Parts;
        var row = Assert.Single(collection);

        vm.ApplyDto(MonsterDto(400));

        Assert.Same(collection, vm.Parts);
        Assert.Same(row, Assert.Single(vm.Parts));
        Assert.Equal(400, row.CurrentHp);
    }

    [Fact]
    public void MonsterHud_notifies_when_reused_part_loses_weakness_chips()
    {
        var vm = new MonsterHudViewModel();
        vm.ApplyDto(MonsterDto(500, [ElementId.Water]));
        var row = Assert.Single(vm.Parts);
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ApplyDto(MonsterDto(400, []));

        Assert.False(row.HasWeakElements);
        Assert.Contains(nameof(PartRowViewModel.HasWeakElements), changed);
    }

    [Fact]
    public void DpsPanel_reuses_entries_collection_and_matching_player_row()
    {
        var vm = new DpsPanelViewModel();
        vm.ApplyDto(DpsDto(100));
        var collection = vm.Entries;
        var row = Assert.Single(collection, entry => entry.IsSelf);

        vm.ApplyDto(DpsDto(140));

        Assert.Same(collection, vm.Entries);
        Assert.Same(row, Assert.Single(vm.Entries, entry => entry.IsSelf));
        Assert.Equal(140, row.TotalDamage);
        Assert.Contains("本怪", vm.PartyTotalText);
    }

    [Fact]
    public void DpsPanel_keeps_total_damage_independent_from_long_monster_name()
    {
        var vm = new DpsPanelViewModel();
        string longName = "怪异克服天彗龙特别特别长的任务目标名称";

        vm.ApplyDto(DpsDto(123456) with { LockedTargetName = longName });

        Assert.DoesNotContain(longName, vm.PartyTotalText);
        Assert.Equal("本怪 123506", vm.PartyTotalText);
        Assert.Equal("用时 0:10.00", vm.PartyTimeText);
    }

    [Theory]
    [InlineData(10.124, "用时 0:10.12")]
    [InlineData(10.126, "用时 0:10.13")]
    [InlineData(3599.999, "用时 1:00:00.00")]
    public void DpsPanel_displays_hunt_time_to_hundredths(double seconds, string expected)
    {
        var vm = new DpsPanelViewModel();

        vm.ApplyDto(DpsDto(100) with { HuntDurationSeconds = seconds });

        Assert.Equal(expected, vm.PartyTimeText);
    }

    [Fact]
    public void DpsPanel_hides_non_finite_hunt_time()
    {
        var vm = new DpsPanelViewModel();

        vm.ApplyDto(DpsDto(100) with { HuntDurationSeconds = double.NaN });

        Assert.Equal("", vm.PartyTimeText);
    }

    [Fact]
    public void DpsPanel_keeps_solo_damage_independent_from_monster_name()
    {
        var vm = new DpsPanelViewModel();
        string longName = "怪异克服天彗龙特别特别长的任务目标名称";
        var dto = new DpsPanelDto(
            [new DpsEntryDto("我", true, 123.4, 987654)],
            HuntDurationSeconds: 10,
            LockedTargetName: longName);

        vm.ApplyDto(dto);

        Assert.DoesNotContain(longName, vm.SoloLineText);
        Assert.Equal("本怪 987654", vm.SoloDamageText);
    }

    [Fact]
    public void QuestBriefing_reuses_targets_collection_and_matching_row()
    {
        var vm = new QuestBriefingViewModel();
        vm.ApplyDto(BriefingDto(false));
        var collection = vm.Targets;
        var row = Assert.Single(collection);

        vm.ApplyDto(BriefingDto(true));

        Assert.Same(collection, vm.Targets);
        Assert.Same(row, Assert.Single(vm.Targets));
        Assert.True(row.IsDefeated);
    }

    [Theory]
    [InlineData(MonsterCompletionState.Slain, "已讨伐")]
    [InlineData(MonsterCompletionState.Captured, "已捕获")]
    [InlineData(MonsterCompletionState.Completed, "任务完成")]
    public void MonsterHud_exposes_clear_completion_badge(
        MonsterCompletionState state,
        string expectedText)
    {
        var vm = new MonsterHudViewModel();

        vm.ApplyDto(MonsterDto(500) with { CompletionState = state });

        Assert.True(vm.CompletionVisible);
        Assert.Equal(expectedText, vm.CompletionText);
    }

    [Fact]
    public void DpsPanel_exposes_compact_cart_counter()
    {
        var vm = new DpsPanelViewModel();
        vm.ApplyDto(DpsDto(100) with { Deaths = 2, MaxDeaths = 3 });

        Assert.True(vm.ShowCartCounter);
        Assert.Equal("猫车 2/3", vm.CartCounterText);
    }

    [Fact]
    public void DpsPanel_exposes_compact_remaining_time()
    {
        var vm = new DpsPanelViewModel();
        vm.ApplyDto(DpsDto(100) with { QuestTimeRemainingSeconds = 1799 });

        Assert.True(vm.ShowTimeRemaining);
        Assert.Equal("剩余 29:59", vm.TimeRemainingText);
    }

    [Fact]
    public void MonsterHud_exposes_failed_quest_badge()
    {
        var vm = new MonsterHudViewModel();

        vm.ApplyDto(MonsterDto(500) with { CompletionState = MonsterCompletionState.Failed });

        Assert.True(vm.CompletionVisible);
        Assert.Equal("任务失败", vm.CompletionText);
    }

    private static MonsterHudDto MonsterDto(double partHp, IReadOnlyList<ElementId>? weak = null) => new(
        "搔鸟", 1000, 2000, true, 25,
        [ElementId.Water, ElementId.Fire], [ElementId.Water],
        new StatusLineModel(null, null, false, null, null, null),
        [new PartDto("头", partHp, 500, false, false, weak ?? [ElementId.Water])],
        []);

    private static DpsPanelDto DpsDto(long damage) => new(
        [
            new DpsEntryDto("我", true, damage / 10d, damage),
            new DpsEntryDto("队友", false, 5, 50),
        ],
        HuntDurationSeconds: 10,
        LockedTargetDamage: damage,
        LockedTargetName: "搔鸟");

    private static QuestBriefingDto BriefingDto(bool defeated) => new(
        [new QuestBriefingTargetDto(
            "搔鸟", true, false, "头",
            [ElementId.Water, ElementId.Fire], [ElementId.Water],
            IsDefeated: defeated)]);
}
