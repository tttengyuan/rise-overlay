using RiseOverlay.Domain;

public class AilmentLineFormatterTests
{
    [Fact]
    public void Format_places_visible_ailments_on_one_text_line()
    {
        AilmentDto[] ailments =
        [
            new("poison", "毒", 10, false),
            new("paralysis", "麻", 20, false),
            new("sleep", "眠", 0, false),
            new("stun", "晕眩", 88, false),
        ];

        Assert.Equal("毒10% 麻20%", AilmentLineFormatter.Format(ailments));
    }

    [Fact]
    public void Format_uses_active_text_instead_of_an_empty_progress_value()
    {
        AilmentDto[] ailments =
        [
            new("paralysis", "麻", 0, true),
            new("sleep", "眠", 78.4, false),
        ];

        Assert.Equal("麻生效 眠78%", AilmentLineFormatter.Format(ailments));
    }

    [Fact]
    public void Format_omits_inactive_zero_values()
    {
        AilmentDto[] ailments =
        [
            new("poison", "毒", 0, false),
        ];

        Assert.Equal(string.Empty, AilmentLineFormatter.Format(ailments));
    }
}
