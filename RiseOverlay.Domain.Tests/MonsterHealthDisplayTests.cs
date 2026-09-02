using RiseOverlay.Domain;

public class MonsterHealthDisplayTests
{
    [Theory]
    [InlineData(1, 58950, 0)]
    [InlineData(0, 58950, 0)]
    [InlineData(500, 58950, 500)]
    [InlineData(1, 100, 1)]
    public void ForHud_clamps_ghost_one_hp_on_large_monsters(double health, double max, double expected)
        => Assert.Equal(expected, MonsterHealthDisplay.ForHud(health, max));
}
