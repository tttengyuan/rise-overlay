using RiseOverlay.Domain;

public class DpsPanelMapperTests
{
    [Theory]
    [InlineData(1000, 10, 100.0)]
    [InlineData(1000, 0, 1000.0)]
    [InlineData(1000, 0.5, 1000.0)]
    [InlineData(0, 30, 0.0)]
    [InlineData(500, 1, 500.0)]
    public void CalculateDps_divides_by_elapsed_with_floor_of_one(
        long totalDamage,
        double questElapsedSeconds,
        double expected)
    {
        Assert.Equal(expected, DpsPanelMapper.CalculateDps(totalDamage, questElapsedSeconds));
    }

    [Fact]
    public void FromSnapshots_sorts_by_total_damage_descending()
    {
        var dto = DpsPanelMapper.FromSnapshots(
        [
            new DpsMemberSnapshot("C", false, 100),
            new DpsMemberSnapshot("A", true, 300),
            new DpsMemberSnapshot("B", false, 200),
        ],
        questElapsedSeconds: 10);

        Assert.Equal(3, dto.Entries.Count);
        Assert.Equal(["A", "B", "C"], dto.Entries.Select(e => e.Name).ToArray());
        Assert.Equal(30.0, dto.Entries[0].Dps);
        Assert.True(dto.Entries[0].IsSelf);
    }

    [Fact]
    public void FromSnapshots_takes_top_four_by_damage()
    {
        var members = Enumerable.Range(1, 6)
            .Select(i => new DpsMemberSnapshot($"P{i}", false, i * 10L))
            .ToArray();

        var dto = DpsPanelMapper.FromSnapshots(members, questElapsedSeconds: 5);

        Assert.Equal(4, dto.Entries.Count);
        Assert.Equal(["P6", "P5", "P4", "P3"], dto.Entries.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void ToEntry_uses_question_mark_for_blank_name()
    {
        var entry = DpsPanelMapper.ToEntry("  ", isSelf: false, totalDamage: 50, questElapsedSeconds: 10);
        Assert.Equal("?", entry.Name);
        Assert.Equal(5.0, entry.Dps);
    }

    [Fact]
    public void FromSnapshots_clamps_negative_damage_to_zero()
    {
        var dto = DpsPanelMapper.FromSnapshots(
        [
            new DpsMemberSnapshot("X", true, -10),
        ],
        questElapsedSeconds: 2);

        Assert.Equal(0, dto.Entries[0].TotalDamage);
        Assert.Equal(0.0, dto.Entries[0].Dps);
    }
}
