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
        Assert.Contains("搔鸟", vm.PartyTotalText);
        Assert.Contains("本怪", vm.PartyTotalText);
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
