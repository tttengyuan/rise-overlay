using RiseOverlay.Domain;

public class PartHealthTextTests
{
    [Fact]
    public void Format_intact_shows_break_pool()
    {
        Assert.Equal("2016/2958", PartHealthText.Format(
            isBroken: false, currentHp: 2016, maxHp: 2958, flinch: 100, maxFlinch: 400));
    }

    [Fact]
    public void Format_broken_with_flinch_shows_flinch_not_完成()
    {
        var text = PartHealthText.Format(
            isBroken: true, currentHp: 0, maxHp: 2958, flinch: 180, maxFlinch: 400);
        Assert.Equal("180/400", text);
        Assert.DoesNotContain("完成", text);
    }

    [Fact]
    public void Format_qurio_overlays_broken_part()
    {
        Assert.Equal("505/735", PartHealthText.Format(
            isBroken: true, currentHp: 505, maxHp: 735, flinch: 180, maxFlinch: 400, isQurio: true));
    }

    [Fact]
    public void Format_broken_without_flinch_keeps_break_numbers()
    {
        Assert.Equal("0/809", PartHealthText.Format(
            isBroken: true, currentHp: 0, maxHp: 809, flinch: 0, maxFlinch: 0));
    }
}
