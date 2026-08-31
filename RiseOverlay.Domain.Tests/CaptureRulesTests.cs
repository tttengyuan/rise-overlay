using RiseOverlay.Domain;

public class CaptureRulesTests
{
    [Theory]
    [InlineData(25, 25, true)]
    [InlineData(24.9, 25, true)]
    [InlineData(25.1, 25, false)]
    [InlineData(0, 25, true)]
    [InlineData(100, 25, false)]
    public void IsPastThreshold_compares_health_to_threshold(
        double healthPercent,
        double thresholdPercent,
        bool expected)
    {
        Assert.Equal(expected, CaptureRules.IsPastThreshold(healthPercent, thresholdPercent));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShowCaptureUi_true_only_when_both_true(
        bool speciesCapturable,
        bool questAllowsCapture,
        bool expected)
    {
        Assert.Equal(expected, CaptureRules.ShowCaptureUi(speciesCapturable, questAllowsCapture));
    }
}
