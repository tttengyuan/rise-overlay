using RiseOverlay.Domain;

public class DpsPanelMapperTests
{
    [Fact]
    public void FromSnapshots_sorts_by_total_damage_descending()
    {
        var dto = DpsPanelMapper.FromSnapshots(
        [
            new DpsMemberSnapshot("C", false, 100, 10),
            new DpsMemberSnapshot("A", true, 300, 30),
            new DpsMemberSnapshot("B", false, 200, 20),
        ]);

        Assert.Equal(3, dto.Entries.Count);
        Assert.Equal(["A", "B", "C"], dto.Entries.Select(e => e.Name).ToArray());
        Assert.Equal(30.0, dto.Entries[0].Dps);
        Assert.True(dto.Entries[0].IsSelf);
    }

    [Fact]
    public void FromSnapshots_takes_top_four_by_damage()
    {
        var members = Enumerable.Range(1, 6)
            .Select(i => new DpsMemberSnapshot($"P{i}", false, i * 10L, i))
            .ToArray();

        var dto = DpsPanelMapper.FromSnapshots(members);

        Assert.Equal(4, dto.Entries.Count);
        Assert.Equal(["P6", "P5", "P4", "P3"], dto.Entries.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void FromSnapshots_clamps_negative_damage_to_zero()
    {
        var dto = DpsPanelMapper.FromSnapshots(
        [
            new DpsMemberSnapshot("X", true, -10, -5),
        ]);

        Assert.Equal(0, dto.Entries[0].TotalDamage);
        Assert.Equal(0.0, dto.Entries[0].Dps);
    }

    [Fact]
    public void FromSnapshots_passes_quest_and_locked_damage_scopes()
    {
        var dto = DpsPanelMapper.FromSnapshots(
        [
            new DpsMemberSnapshot("我", true, 1000, 50),
        ],
        huntDurationSeconds: 20,
        questTotalDamage: 2500,
        lockedTargetDamage: 900,
        lockedTargetName: "千刃龙");

        Assert.Equal(2500, dto.QuestTotalDamage);
        Assert.Equal(900, dto.LockedTargetDamage);
        Assert.Equal("千刃龙", dto.LockedTargetName);
    }

    [Fact]
    public void FromSnapshots_uses_question_mark_for_blank_name()
    {
        var dto = DpsPanelMapper.FromSnapshots(
        [
            new DpsMemberSnapshot("  ", false, 50, 12.5),
        ]);

        Assert.Equal("?", dto.Entries[0].Name);
        Assert.Equal(12.5, dto.Entries[0].Dps);
    }
}

public class OriginalDpsCalculatorTests
{
    [Theory]
    [InlineData(DpsCalculationMode.RelativeToQuest, 0, 0, 100)]
    [InlineData(DpsCalculationMode.RelativeToJoin, 5, 0, 200)]
    [InlineData(DpsCalculationMode.RelativeToFirstHit, 0, 8, 500)]
    public void Calculate_matches_original_three_strategies(
        DpsCalculationMode mode,
        double joinedAt,
        double firstHitAt,
        double expected)
    {
        Assert.Equal(
            expected,
            OriginalDpsCalculator.Calculate(
                totalDamage: 1000,
                questElapsed: 10,
                joinedAt,
                firstHitAt,
                mode));
    }

    [Fact]
    public void Calculate_clamps_denominator_to_one_second_like_original()
    {
        Assert.Equal(
            1000,
            OriginalDpsCalculator.Calculate(
                totalDamage: 1000,
                questElapsed: 10,
                joinedAt: 10,
                firstHitAt: 10,
                DpsCalculationMode.RelativeToJoin));
    }
}
