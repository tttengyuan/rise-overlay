using RiseOverlay.Domain;

public class MonsterHudMapperTests
{
    // Recommended = ElementRecommend over max-per-element across all hitzone rows (all parts/phases).
    private static StaticMonsterSnapshot ValstraxStatic() => new(
        Id: "monster_valstrax_crimson",
        Title: "神秘红光天彗龙",
        Capturable: false,
        Hitzones:
        [
            new("头", 1, Fire: 25, Water: 25, Ice: 25, Thunder: 25, Dragon: 0),
            new("头", 2, Fire: 25, Water: 25, Ice: 25, Thunder: 25, Dragon: 0),
            new("前肢", 1, Fire: 20, Water: 20, Ice: 20, Thunder: 20, Dragon: 0),
            new("尾巴", null, Fire: 15, Water: 15, Ice: 15, Thunder: 15, Dragon: 0),
        ],
        Parts:
        [
            new("头", Break: "Lv1: 700", Sever: null),
            new("前肢", Break: "Lv1: 300", Sever: null),
            new("尾巴", Break: null, Sever: "300"),
        ]);

    private static StaticMonsterSnapshot MagnamaloStatic() => new(
        Id: "monster_089_00",
        Title: "怨虎龙",
        Capturable: true,
        Hitzones:
        [
            new("头部", null, Fire: 0, Water: 15, Ice: 5, Thunder: 10, Dragon: 0),
            new("前肢", null, Fire: 0, Water: 20, Ice: 5, Thunder: 15, Dragon: 0),
            new("腹部", null, Fire: 0, Water: 25, Ice: 5, Thunder: 20, Dragon: 0),
            new("尾巴", null, Fire: 0, Water: 10, Ice: 5, Thunder: 10, Dragon: 0),
        ],
        Parts:
        [
            new("头部", Break: "Lv1: 580", Sever: null),
            new("前肢", Break: "Lv1: 300", Sever: null),
            new("腹部", Break: null, Sever: null),
            new("尾巴", Break: null, Sever: "300"),
        ]);

    [Fact]
    public void BuildFromStatic_Valstrax_recommends_four_and_not_capturable()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(ValstraxStatic());

        Assert.Equal("神秘红光天彗龙", mapped.Name);
        Assert.False(mapped.IsCapturable);
        Assert.Null(mapped.CaptureThresholdPercent);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder },
            mapped.Recommended);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder, ElementId.Dragon },
            mapped.OverallElementsOrdered);
        Assert.True(mapped.HasSeverableTail);

        var briefing = MonsterHudMapper.ToBriefingTarget(mapped);
        Assert.Equal(mapped.Name, briefing.Name);
        Assert.False(briefing.IsCapturable);
        Assert.Equal(mapped.Recommended, briefing.Recommended);
    }

    [Fact]
    public void BuildFromStatic_Magnamalo_is_capturable_with_water_recommend()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());

        Assert.True(mapped.IsCapturable);
        Assert.Equal(25, mapped.CaptureThresholdPercent);
        Assert.Equal(new[] { ElementId.Water }, mapped.Recommended);
        Assert.True(mapped.HasSeverableTail);
        Assert.False(string.IsNullOrWhiteSpace(mapped.FocusPartLabel));
    }

    [Fact]
    public void MergeLive_filters_stun_and_inactive_zero_ailments()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 10000,
            HealthMax: 20000,
            Parts: [new LivePartSnapshot("头部", 500, 1000, false)],
            Ailments:
            [
                new("poison", "毒", 42, false),
                new("stun", "晕眩", 88, false),
                new("sleep", "眠", 0, false),
                new("paralysis", "麻", 0, true),
            ],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.DoesNotContain(dto.Ailments, a => a.Key.Equals("stun", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dto.Ailments, a => a.Key == "sleep");
        Assert.Contains(dto.Ailments, a => a.Key == "poison");
        Assert.Contains(dto.Ailments, a => a.Key == "paralysis" && a.IsActive);
    }

    [Fact]
    public void MergeLive_past_threshold_sets_banner_conditions_via_dto_fields()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 4000,
            HealthMax: 20000, // 20% <= 25%
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true);

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        var healthPercent = dto.HealthMax <= 0 ? 0 : dto.HealthCurrent / dto.HealthMax * 100.0;

        Assert.True(dto.IsCapturable);
        Assert.Equal(25, dto.CaptureThresholdPercent);
        Assert.True(CaptureRules.IsPastThreshold(healthPercent, dto.CaptureThresholdPercent!.Value));
    }

    [Fact]
    public void MergeLive_Valstrax_no_weaken_line()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(ValstraxStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 1000,
            HealthMax: 20000,
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.False(dto.IsCapturable);
        Assert.Null(dto.CaptureThresholdPercent);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder },
            dto.Recommended);
    }

    [Fact]
    public void MergeLive_quest_disallows_capture_hides_capture_ui()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 1000,
            HealthMax: 20000,
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: false);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.False(dto.IsCapturable);
        Assert.Null(dto.CaptureThresholdPercent);
    }
}
