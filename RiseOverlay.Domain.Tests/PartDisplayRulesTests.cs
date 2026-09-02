using RiseOverlay.Domain;

public class PartDisplayRulesTests
{
    [Theory]
    [InlineData(false, false, false, "可破", true, false)]
    [InlineData(false, true, false, "已破坏", false, true)]
    [InlineData(true, false, false, "可断", true, false)]
    [InlineData(true, true, false, "已断尾", false, true)]
    [InlineData(false, false, true, "怪异核", true, false)]
    public void Resolve_returns_unambiguous_part_status(
        bool isSeverable,
        bool isBroken,
        bool isQurio,
        string expectedText,
        bool expectedActiveBar,
        bool expectedComplete)
    {
        var state = PartDisplayRules.Resolve(isSeverable, isBroken, isQurio);

        Assert.Equal(expectedText, state.Text);
        Assert.Equal(expectedActiveBar, state.ShowActiveBar);
        Assert.Equal(expectedComplete, state.IsComplete);
    }
}
