using RiseOverlay.Domain;

public class MonsterLiveAdapterTests
{
    [Fact]
    public void ToSnapshot_maps_hp_parts_ailments_and_status()
    {
        var fixture = new MonsterLiveFixture(
            Name: "怨虎龙",
            Id: 89,
            Health: 4820,
            MaxHealth: 24500,
            Stamina: 620,
            MaxStamina: 1000,
            CaptureThreshold: 0.25,
            IsEnraged: true,
            Parts:
            [
                new("PART_HEAD", "头部", 620, 1200, 0),
                new("PART_TAIL", "尾巴", 0, 800, 1),
            ],
            Ailments:
            [
                new("AILMENT_POISON", null, 0, 0, 42, 100),
                new("AILMENT_STUN", null, 0, 0, 88, 100),
                new("AILMENT_PARALYSIS", null, 5, 10, 0, 100),
                new("AILMENT_SLEEP", null, 0, 0, 0, 100),
            ],
            Enrage: new("STATUS_ENRAGE", null, 14, 60, 0, 0),
            QuestAllowsCapture: true);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);

        Assert.Equal(4820, live.HealthCurrent);
        Assert.Equal(24500, live.HealthMax);
        Assert.Equal(25, live.CaptureThresholdPercent);
        Assert.True(live.QuestAllowsCapture);

        Assert.Contains(live.Parts, p => p.Name == "头部" && p.CurrentHp == 620);
        Assert.Contains(live.Parts, p => p.Name == "尾巴" && p.IsBroken);

        Assert.Contains(live.Ailments, a => a.Key == "poison" && a.Percent == 42 && !a.IsActive);
        Assert.Contains(live.Ailments, a => a.Key == "paralysis" && a.IsActive);
        Assert.Contains(live.Ailments, a => a.Key == "sleep" && a.Percent == 0 && !a.IsActive);
        Assert.DoesNotContain(live.Ailments, a => a.Key == "stun"); // stun feeds status line only
        Assert.DoesNotContain(live.Ailments, a => a.Key == "enrage");

        Assert.Equal(TimeSpan.FromSeconds(14), live.Status.EnrageRemaining);
        Assert.Equal(88, live.Status.StunBuildupPercent);
        Assert.False(live.Status.StunActive);
        Assert.Equal(62, live.Status.StaminaPercent);
    }

    [Fact]
    public void ToSnapshot_zero_capture_threshold_leaves_percent_null()
    {
        var fixture = new MonsterLiveFixture(
            Name: "天彗龙",
            Id: 1,
            Health: 1000,
            MaxHealth: 2000,
            Stamina: 0,
            MaxStamina: 0,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts: [],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: true);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        Assert.Null(live.CaptureThresholdPercent);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Hunt", true)]
    [InlineData("Capture", true)]
    [InlineData("Slay", false)]
    [InlineData("Special", true)]
    public void ResolveQuestAllowsCapture_best_effort(string? questType, bool expected)
        => Assert.Equal(expected, MonsterLiveAdapter.ResolveQuestAllowsCapture(questType));

    [Fact]
    public void MergeLive_prefers_live_capture_threshold_percent()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(new StaticMonsterSnapshot(
            Id: "monster_089_00",
            Title: "怨虎龙",
            Capturable: true,
            Hitzones: [new("头部", null, 0, 20, 5, 10, 0)],
            Parts: [new("头部", "Lv1", null)]));

        var live = new LiveMonsterSnapshot(
            HealthCurrent: 4000,
            HealthMax: 20000,
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true,
            CaptureThresholdPercent: 30);

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        Assert.Equal(30, dto.CaptureThresholdPercent);
    }

    [Fact]
    public void CreateFallbackStatic_allows_hp_without_weakness()
    {
        var mapped = MonsterHudMapper.CreateFallbackStatic("未知龙", isCapturable: true);
        var live = MonsterLiveAdapter.ToSnapshot(new MonsterLiveFixture(
            Name: "未知龙",
            Id: 0,
            Health: 500,
            MaxHealth: 1000,
            Stamina: 0,
            MaxStamina: 0,
            CaptureThreshold: 0.25,
            IsEnraged: false,
            Parts: [new("PART_HEAD", "头部", 100, 200, 0)],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: true));

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        Assert.Equal("未知龙", dto.Name);
        Assert.Equal(500, dto.HealthCurrent);
        Assert.Empty(dto.Recommended);
        Assert.True(dto.IsCapturable);
        Assert.Equal(25, dto.CaptureThresholdPercent); // live preferred
    }
}
