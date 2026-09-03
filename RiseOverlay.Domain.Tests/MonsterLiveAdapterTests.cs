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
                new("PART_HEAD", "头部", 620, 1200, 0, IsBreakable: true),
                new("PART_TAIL", "尾巴", 0, 800, 1, IsBreakable: true),
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
    public void ToSnapshot_broken_breakable_freezes_hp_bar()
    {
        var fixture = new MonsterLiveFixture(
            Name: "雌火龙",
            Id: 1,
            Health: 20,
            MaxHealth: 67230,
            Stamina: 1000,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                // Rise collapsed MaxHealth after break; BreakCount promoted by controller.
                new("PART_HEAD", "头部", 0, 0, 1, IsBreakable: true, MaxFlinch: 200, Flinch: 40),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var head = Assert.Single(live.Parts);
        Assert.True(head.IsBroken);
        Assert.Equal(0, head.CurrentHp);
        Assert.False(head.IsQurio);
    }

    [Fact]
    public void ToSnapshot_active_qurio_keeps_live_hp_even_if_break_count_set()
    {
        var fixture = new MonsterLiveFixture(
            Name: "雌火龙",
            Id: 1,
            Health: 5000,
            MaxHealth: 67230,
            Stamina: 1000,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                new("PART_HEAD", "头部", 897, 900, 0,
                    IsQurio: true, IsBreakable: true),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var head = Assert.Single(live.Parts);
        Assert.False(head.IsBroken);
        Assert.True(head.IsQurio);
        Assert.Equal(897, head.CurrentHp);
        Assert.Equal(900, head.MaxHp);
    }

    [Fact]
    public void ToSnapshot_refilled_breakable_after_break_stays_broken()
    {
        // Rise keeps MaxHealth and refills Health after a break — must not show 可破 full bar.
        var fixture = new MonsterLiveFixture(
            Name: "蛮颚龙",
            Id: 1,
            Health: 11,
            MaxHealth: 7360,
            Stamina: 800,
            MaxStamina: 1000,
            CaptureThreshold: 0.3,
            IsEnraged: true,
            Parts:
            [
                new("PART_HEAD", "头部", 898, 898, 1,
                    IsBreakable: true, MaxFlinch: 200, Flinch: 200),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: true);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var head = Assert.Single(live.Parts);
        Assert.True(head.IsBroken);
        Assert.Equal(0, head.CurrentHp);
        var state = PartDisplayRules.Resolve(false, head.IsBroken, false);
        Assert.Equal("已破坏", state.Text);
        Assert.False(state.ShowActiveBar);
    }

    [Fact]
    public void ToSnapshot_collapsed_breakable_does_not_show_regenerating_flinch()
    {
        // After a break Rise zeroes MaxHealth; flinch often refills to full.
        // That must NOT appear as a healed breakable part.
        var fixture = new MonsterLiveFixture(
            Name: "雌火龙",
            Id: 1,
            Health: 50000,
            MaxHealth: 67230,
            Stamina: 1000,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                new("PART_WING_LEFT", "左翼", 0, 0, 1,
                    IsBreakable: true, MaxFlinch: 500, Flinch: 500),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var wing = Assert.Single(live.Parts);
        Assert.True(wing.IsBroken);
        Assert.Equal(0, wing.CurrentHp);
        var state = PartDisplayRules.Resolve(false, wing.IsBroken, wing.IsQurio);
        Assert.Equal("已破坏", state.Text);
        Assert.False(state.ShowActiveBar);
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
                    IsQurio: true, IsQurioThreshold: true, IsBreakable: false),
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
    public void ToSnapshot_broken_head_is_not_labeled_severable()
    {
        var fixture = new MonsterLiveFixture(
            Name: "土砂龙",
            Id: 15,
            Health: 29000,
            MaxHealth: 43000,
            Stamina: 1000,
            MaxStamina: 1000,
            CaptureThreshold: 0,
            IsEnraged: false,
            Parts:
            [
                // MaxSever noise / wrong Type must not make the head 「已断尾」.
                new("PART_HEAD", "头部", 0, 1200, 1,
                    IsBreakable: true, IsSeverable: false, MaxSever: 100, Sever: 100),
                new("PART_HEAD_MUD", "头部(泥)", 0, 0, 1,
                    IsBreakable: true, IsSeverable: false),
                new("PART_TAIL", "尾巴", 640, 800, 0,
                    IsSeverable: true, MaxSever: 800, Sever: 640),
                new("PART_TAIL_MUD", "尾巴(泥)", 0, 500, 1,
                    IsBreakable: true, IsSeverable: false),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: false);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var head = Assert.Single(live.Parts, p => p.Name == "头部");
        Assert.True(head.IsBroken);
        Assert.False(head.IsSeverable);
        Assert.Equal("已破坏", PartDisplayRules.Resolve(head.IsSeverable, head.IsBroken, false).Text);

        var headMud = Assert.Single(live.Parts, p => p.Name.Contains("头") && p.Name.Contains("泥"));
        Assert.True(headMud.IsBroken);
        Assert.False(headMud.IsSeverable);
        Assert.Equal("已破坏", PartDisplayRules.Resolve(headMud.IsSeverable, headMud.IsBroken, false).Text);

        var tail = Assert.Single(live.Parts, p => p.Name == "尾巴");
        Assert.True(tail.IsSeverable);
        Assert.Equal("可断", PartDisplayRules.Resolve(tail.IsSeverable, tail.IsBroken, false).Text);

        var tailMud = Assert.Single(live.Parts, p => p.Name.Contains("尾") && p.Name.Contains("泥"));
        Assert.True(tailMud.IsBroken);
        Assert.False(tailMud.IsSeverable);
        Assert.Equal("已破坏", PartDisplayRules.Resolve(tailMud.IsSeverable, tailMud.IsBroken, false).Text);
    }

    [Fact]
    public void ToSnapshot_severable_tail_prefers_sever_bar_over_break_health()
    {
        var fixture = new MonsterLiveFixture(
            Name: "角龙",
            Id: 7,
            Health: 40000,
            MaxHealth: 50000,
            Stamina: 1000,
            MaxStamina: 1000,
            CaptureThreshold: 0.3,
            IsEnraged: false,
            Parts:
            [
                // Live Diablos tail often exposes both MaxHealth and MaxSever.
                new("PART_TAIL", "尾巴", 500, 500, 0,
                    IsBreakable: true, IsSeverable: true,
                    MaxSever: 800, Sever: 640, MaxFlinch: 200, Flinch: 200),
            ],
            Ailments: [],
            Enrage: null,
            QuestAllowsCapture: true);

        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var tail = Assert.Single(live.Parts);
        Assert.True(tail.IsSeverable);
        Assert.False(tail.IsBroken);
        Assert.Equal(640, tail.CurrentHp);
        Assert.Equal(800, tail.MaxHp);
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
            Parts: [new("PART_HEAD", "头部", 100, 200, 0, IsBreakable: true)],
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
