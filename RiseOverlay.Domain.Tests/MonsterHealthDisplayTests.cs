using RiseOverlay.Domain;

public class MonsterHealthDisplayTests
{
    [Theory]
    [InlineData(0, 58950, 0)]
    [InlineData(0.5, 58950, 0)] // float dust after slay
    [InlineData(1, 58950, 1)] // living 1 HP must stay visible
    [InlineData(1, 8588, 1)]
    [InlineData(2, 8588, 2)]
    [InlineData(2, 34500, 2)]
    [InlineData(500, 58950, 500)]
    [InlineData(1, 100, 1)]
    public void ForHud_keeps_real_low_hp_and_only_clears_fractional_dust(
        double health, double max, double expected)
        => Assert.Equal(expected, MonsterHealthDisplay.ForHud(health, max));
}
