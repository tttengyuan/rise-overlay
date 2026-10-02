using RiseOverlay.Domain;

namespace RiseOverlay.Domain.Tests;

public sealed class CapsulePartRulesTests
{
    [Fact]
    public void FillRatio_PrefersQurioHealth()
    {
        Assert.Equal(0.4, CapsulePartRules.FillRatio(true, 0.4, 0.9, 100));
    }

    [Fact]
    public void FillRatio_UsesFlinchWhenPresent()
    {
        Assert.Equal(0.92, CapsulePartRules.FillRatio(false, 0.1, 0.92, 50));
    }

    [Fact]
    public void FillRatio_FallsBackToHealthWithoutFlinch()
    {
        Assert.Equal(0.55, CapsulePartRules.FillRatio(false, 0.55, 0, 0));
    }

    [Fact]
    public void ShowHardMark_AtThreshold()
    {
        Assert.True(CapsulePartRules.ShowHardMark(false, 100, 0.85));
        Assert.False(CapsulePartRules.ShowHardMark(false, 100, 0.84));
        Assert.False(CapsulePartRules.ShowHardMark(true, 100, 1));
    }

    [Fact]
    public void TakeVisibleElements_CapsAtTwo()
    {
        var all = new[] { ElementId.Fire, ElementId.Thunder, ElementId.Ice };
        Assert.Equal(new[] { ElementId.Fire, ElementId.Thunder }, CapsulePartRules.TakeVisibleElements(all));
    }

    [Fact]
    public void Percent_Rounds()
    {
        Assert.Equal(92, CapsulePartRules.Percent(0.924));
    }
}
