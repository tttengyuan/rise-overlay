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
    public void ToSnapshot_does_not_mark_uninitialized_breakable_as_broken()
    {
        var fixture = new MonsterLiveFixture(
            Name: "水兽",
            Id: 47,
            Health: 4000,
            MaxHealth: 47300,
            Stamina: 0,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                new("PART_TAIL", "尾巴", 0, 0, 0,
                    IsSeverable: true, MaxSever: 800, Sever: 800, MaxFlinch: 200, Flinch: 200),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var tail = Assert.Single(live.Parts);
        Assert.False(tail.IsBroken);
    }

    [Fact]
    public void ToSnapshot_qurio_threshold_full_at_start_is_not_broken()
    {
        var fixture = new MonsterLiveFixture(
            Name: "水兽",
            Id: 47,
            Health: 53750,
            MaxHealth: 53750,
            Stamina: 1000,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                // QurioHealth = MaxThreshold - Threshold → full at hunt start.
                new("PART_QURIO_THRESHOLD", "啮生虫", 9460, 9460, 0,
                    IsQurio: true, IsQurioThreshold: true),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var qurio = Assert.Single(live.Parts);
        Assert.False(qurio.IsBroken);
        Assert.Equal(9460, qurio.CurrentHp);
        Assert.Equal(9460, qurio.MaxHp);
    }

    [Fact]
    public void ToSnapshot_marks_severed_tail()
    {
        var fixture = new MonsterLiveFixture(
            Name: "水兽",
            Id: 47,
            Health: 4000,
            MaxHealth: 47300,
            Stamina: 0,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                new("PART_TAIL", "尾巴", 0, 0, 0,
                    IsSeverable: true, MaxSever: 800, Sever: 800, MaxFlinch: 200, Flinch: 80),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        Assert.True(Assert.Single(live.Parts).IsBroken);
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
    [InlineData(null, null, true)]
    [InlineData("Hunt", null, true)]
    [InlineData("Capture", null, true)]
    [InlineData("Slay", null, false)]
    [InlineData("Special", null, false)]
    [InlineData("Delivery", null, false)]
    [InlineData("Hunt", "Anomaly", true)]
    [InlineData(null, "Anomaly", true)]
    public void ResolveQuestAllowsCapture_best_effort(string? questType, string? questLevel, bool expected)
        => Assert.Equal(expected, MonsterLiveAdapter.ResolveQuestAllowsCapture(questType, questLevel));

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
