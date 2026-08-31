using RiseOverlay.Domain;

public class StatusLineFormatterTests
{
    [Fact]
    public void Formats_enrage_stun_stamina()
    {
        var s = new StatusLineModel(
            EnrageRemaining: TimeSpan.FromSeconds(14),
            StunBuildupPercent: 88,
            StunActive: false,
            StunActiveRemaining: null,
            StaminaPercent: 62,
            DownRemaining: null);
        Assert.Equal("愤怒 0:14 · 晕眩 88% · 耐力 62%", StatusLineFormatter.Format(s));
    }

    [Fact]
    public void Inserts_active_stun_and_down()
    {
        var s = new StatusLineModel(
            TimeSpan.FromSeconds(9), null, true, TimeSpan.FromSeconds(3), 18,
            TimeSpan.FromSeconds(2));
        Assert.Equal("愤怒 0:09 · 晕眩中 0:03 · 耐力 18% · 倒地 0:02", StatusLineFormatter.Format(s));
    }
}
