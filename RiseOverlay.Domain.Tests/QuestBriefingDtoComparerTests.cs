using RiseOverlay.Domain;

public class QuestBriefingDtoComparerTests
{
    [Fact]
    public void Equivalent_treats_separately_allocated_equal_rows_as_unchanged()
    {
        var left = Dto(defeated: false);
        var right = Dto(defeated: false);

        Assert.True(QuestBriefingDtoComparer.Equivalent(left, right));
    }

    [Fact]
    public void Equivalent_detects_defeated_state_change()
    {
        Assert.False(QuestBriefingDtoComparer.Equivalent(Dto(false), Dto(true)));
    }

    private static QuestBriefingDto Dto(bool defeated) => new(
        [new QuestBriefingTargetDto(
            "搔鸟", true, false, "头",
            [ElementId.Water, ElementId.Fire],
            [ElementId.Water],
            IsDefeated: defeated)]);
}
