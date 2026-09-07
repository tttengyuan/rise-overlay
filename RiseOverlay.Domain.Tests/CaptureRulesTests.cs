using RiseOverlay.Domain;

public class CaptureRulesTests
{
    [Fact]
    public void Briefing_anomaly_marks_only_first_quest_target_as_afflicted()
    {
        QuestBriefingTargetDto[] targets =
        [
            Target("岩龙", capturable: true),
            Target("泡狐龙", capturable: true),
            Target("激昂金狮子", capturable: true),
        ];

        var resolved = CaptureRules.ResolveBriefingTargetStates(
            targets,
            isAnomalyQuest: true,
            isSlayQuest: false);

        Assert.Equal(CaptureDisplayState.Anomaly, resolved[0].CaptureState);
        Assert.False(resolved[0].IsCapturable);
        Assert.Equal(CaptureDisplayState.Capturable, resolved[1].CaptureState);
        Assert.True(resolved[1].IsCapturable);
        Assert.Equal(CaptureDisplayState.Capturable, resolved[2].CaptureState);
        Assert.True(resolved[2].IsCapturable);
    }

    [Fact]
    public void Briefing_slay_restricts_quest_targets_but_not_invaders()
    {
        QuestBriefingTargetDto[] targets =
        [
            Target("任务怪", capturable: true),
            Target("乱入怪", capturable: true) with { Kind = BriefingTargetKind.Invasion },
        ];

        var resolved = CaptureRules.ResolveBriefingTargetStates(
            targets,
            isAnomalyQuest: false,
            isSlayQuest: true);

        Assert.Equal(CaptureDisplayState.QuestRestricted, resolved[0].CaptureState);
        Assert.False(resolved[0].IsCapturable);
        Assert.Equal(CaptureDisplayState.Capturable, resolved[1].CaptureState);
        Assert.True(resolved[1].IsCapturable);
    }

    private static QuestBriefingTargetDto Target(string name, bool capturable) => new(
        Name: name,
        IsCapturable: capturable,
        HasSeverableTail: false,
        FocusPartLabel: null,
        OverallElementsOrdered: [],
        Recommended: [],
        CaptureState: capturable
            ? CaptureDisplayState.Capturable
            : CaptureDisplayState.SpeciesUncapturable);

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

    [Theory]
    [InlineData(true, true, false, CaptureDisplayState.Capturable)]
    [InlineData(false, true, false, CaptureDisplayState.SpeciesUncapturable)]
    [InlineData(true, false, false, CaptureDisplayState.QuestRestricted)]
    [InlineData(true, false, true, CaptureDisplayState.Anomaly)]
    public void ResolveDisplayState_distinguishes_capture_restriction_reason(
        bool speciesCapturable,
        bool questAllowsCapture,
        bool isAnomaly,
        CaptureDisplayState expected)
    {
        Assert.Equal(
            expected,
            CaptureRules.ResolveDisplayState(speciesCapturable, questAllowsCapture, isAnomaly));
    }

    [Theory]
    [InlineData("霸主·青熊兽", true)]
    [InlineData("霸主・青熊兽", true)]
    [InlineData("Apex Arzuros", true)]
    [InlineData("雪鬼兽", false)]
    public void IsKnownUncapturableSpeciesName_recognizes_apex_variants(string name, bool expected)
    {
        Assert.Equal(expected, CaptureRules.IsKnownUncapturableSpeciesName(name));
    }
}
