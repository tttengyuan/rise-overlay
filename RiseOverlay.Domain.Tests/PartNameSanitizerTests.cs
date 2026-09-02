using RiseOverlay.Domain;

public class PartNameSanitizerTests
{
    [Theory]
    [InlineData("Sponge", "海绵质")]
    [InlineData("Left Legs", "左腿")]
    [InlineData("Qurio Threshold", "啮生虫")]
    [InlineData("RabbitConverted", "")]
    [InlineData("RabbitConverted - 右刃", "右刃")]
    [InlineData("RabbitConverted - 头部", "头部")]
    [InlineData("部位00　头", "头")]
    [InlineData("部位03　尾尾", "尾")]
    [InlineData("Head", "头部")]
    [InlineData("Tail", "尾巴")]
    public void Clean_strips_converter_junk_and_maps_english(string input, string expected)
        => Assert.Equal(expected, PartNameSanitizer.Clean(input));

    [Fact]
    public void Matches_head_variants()
    {
        Assert.True(PartNameSanitizer.Matches("头部", "Head"));
        Assert.True(PartNameSanitizer.Matches("头", "头部"));
        Assert.True(PartNameSanitizer.Matches("RabbitConverted - 头部", "头"));
    }
}
