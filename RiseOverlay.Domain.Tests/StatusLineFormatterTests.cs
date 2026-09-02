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
    public void Inserts_active_stun_but_omits_unavailable_down_state()
    {
        var s = new StatusLineModel(
            TimeSpan.FromSeconds(9), null, true, TimeSpan.FromSeconds(3), 18,
            TimeSpan.FromSeconds(2));
        Assert.Equal("愤怒 0:09 · 晕眩中 3.0s · 耐力 18%", StatusLineFormatter.Format(s));
    }

    [Theory]
    [InlineData(3.2, "3.2s")]
    [InlineData(0.4, "0.4s")]
    [InlineData(72, "1:12")]
    public void FormatStunCountdown_prefers_decimal_seconds(double seconds, string expected)
        => Assert.Equal(expected, StatusLineFormatter.FormatStunCountdown(TimeSpan.FromSeconds(seconds)));
}
