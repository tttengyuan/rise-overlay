using RiseOverlay.Domain;

namespace RiseOverlay.Domain.Tests;

public sealed class OverlayPositionRulesTests
{
    private static readonly OverlayScreenBounds Primary = new(0, 0, 1920, 1080);
    private static readonly OverlayScreenBounds LeftMonitor = new(-1920, 0, 0, 1080);

    [Theory]
    [InlineData(20, 20, true)]
    [InlineData(1888, 1048, true)]
    [InlineData(1900, 100, false)]
    [InlineData(4000, 4000, false)]
    [InlineData(double.NaN, 20, false)]
    [InlineData(double.PositiveInfinity, 20, false)]
    public void IsSufficientlyVisible_checks_real_screen_bounds(double x, double y, bool expected)
    {
        Assert.Equal(
            expected,
            OverlayPositionRules.IsSufficientlyVisible(x, y, [Primary], minimumVisible: 32));
    }

    [Fact]
    public void IsSufficientlyVisible_supports_negative_coordinate_monitors()
    {
        Assert.True(OverlayPositionRules.IsSufficientlyVisible(
            x: -1200,
            y: 100,
            screens: [Primary, LeftMonitor],
            minimumVisible: 32));
    }

    [Fact]
    public void ResolveNormalizedPosition_anchors_to_the_game_client_area()
    {
        (double x, double y) = OverlayPositionRules.ResolveNormalizedPosition(
            bounds: new OverlayScreenBounds(100, 50, 1380, 770),
            widgetWidth: 300,
            widgetHeight: 520,
            scale: 1,
            horizontal: 1,
            vertical: 0.18,
            margin: 18);

        Assert.Equal(1062, x, precision: 2);
        Assert.Equal(179.6, y, precision: 2);
    }

    [Fact]
    public void ResolveNormalizedPosition_clamps_invalid_values_inside_the_game_client_area()
    {
        (double x, double y) = OverlayPositionRules.ResolveNormalizedPosition(
            bounds: Primary,
            widgetWidth: 300,
            widgetHeight: 520,
            scale: 1,
            horizontal: double.PositiveInfinity,
            vertical: -4,
            margin: 18);

        Assert.Equal(1602, x, precision: 2);
        Assert.Equal(18, y, precision: 2);
    }
}
