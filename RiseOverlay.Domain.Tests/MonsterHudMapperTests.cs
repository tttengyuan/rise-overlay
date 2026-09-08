using RiseOverlay.Domain;

public class MonsterHudMapperTests
{
    [Fact]
    public void ToCompleted_zeroes_residual_health_and_capture_state()
    {
        MonsterHudDto live = new(
            Name: "大名盾蟹",
            HealthCurrent: 18,
            HealthMax: 100,
            IsCapturable: true,
            CaptureThresholdPercent: 20,
            OverallElementsOrdered: [ElementId.Fire],
            Recommended: [ElementId.Fire],
            Status: new StatusLineModel(null, null, false, null, null, null),
            Parts: [new PartDto("头部", 20, 30, false, false, [ElementId.Fire])],
            Ailments: []);

        MonsterHudDto completed = MonsterHudMapper.ToCompleted(live);

        Assert.Equal(0, completed.HealthCurrent);
        Assert.Equal(100, completed.HealthMax);
        Assert.False(completed.IsCapturable);
        Assert.Equal(live.Name, completed.Name);
        Assert.Same(live.Parts, completed.Parts);
    }

    [Fact]
    public void ToCaptured_preserves_last_live_health_and_marks_capture()
    {
        MonsterHudDto live = new(
            Name: "搔鸟",
            HealthCurrent: 180,
            HealthMax: 1000,
            IsCapturable: true,
            CaptureThresholdPercent: 20,
            OverallElementsOrdered: [ElementId.Water],
            Recommended: [ElementId.Water],
            Status: new StatusLineModel(null, null, false, null, null, null),
            Parts: [],
            Ailments: []);

        MonsterHudDto captured = MonsterHudMapper.ToCaptured(live);

        Assert.Equal(180, captured.HealthCurrent);
        Assert.False(captured.IsCapturable);
        Assert.Equal(MonsterCompletionState.Captured, captured.CompletionState);
    }

    [Fact]
    public void ToQuestCompleted_uses_neutral_state_when_finish_kind_has_not_arrived()
    {
        MonsterHudDto live = MonsterHudMapper.ToEmptyHud() with
        {
            Name = "迅龙",
            HealthCurrent = 12,
            HealthMax = 1000,
        };

        MonsterHudDto completed = MonsterHudMapper.ToQuestCompleted(live);

        Assert.Equal(0, completed.HealthCurrent);
        Assert.Equal(MonsterCompletionState.Completed, completed.CompletionState);
    }

    [Fact]
    public void Quest_completion_does_not_overwrite_authoritative_capture_event()
    {
        MonsterHudDto captured = MonsterHudMapper.ToCaptured(
            MonsterHudMapper.ToEmptyHud() with
            {
                Name = "搔鸟",
                HealthCurrent = 180,
                HealthMax = 1000,
            });

        MonsterHudDto completed = MonsterHudMapper.ToQuestCompleted(captured);

        Assert.Equal(MonsterCompletionState.Captured, completed.CompletionState);
        Assert.Equal(180, completed.HealthCurrent);
    }

    [Fact]
    public void ToQuestFailed_preserves_last_trustworthy_health_and_marks_failure()
    {
        MonsterHudDto live = MonsterHudMapper.ToEmptyHud() with
        {
            Name = "大名盾蟹",
            HealthCurrent = 7250,
            HealthMax = 10000,
            IsCapturable = true,
            CaptureThresholdPercent = 20,
        };

        MonsterHudDto failed = MonsterHudMapper.ToQuestFailed(live);

        Assert.Equal(7250, failed.HealthCurrent);
        Assert.Equal(10000, failed.HealthMax);
        Assert.False(failed.IsCapturable);
        Assert.Null(failed.CaptureThresholdPercent);
        Assert.Equal(MonsterCompletionState.Failed, failed.CompletionState);
    }

    [Fact]
    public void ToQuestFailed_prefers_last_live_frame_over_false_zero_health_death_freeze()
    {
        MonsterHudDto live = MonsterHudMapper.ToEmptyHud() with
        {
            Name = "大名盾蟹",
            HealthCurrent = 7250,
            HealthMax = 10000,
        };
        MonsterHudDto teardownDeath = MonsterHudMapper.ToCompleted(live);

        MonsterHudDto failed = MonsterHudMapper.ToQuestFailed(live, teardownDeath);

        Assert.Equal(7250, failed.HealthCurrent);
        Assert.Equal(MonsterCompletionState.Failed, failed.CompletionState);
    }

    [Fact]
    public void ToQuestFailed_keeps_zero_health_for_an_earlier_trusted_kill()
    {
        MonsterHudDto live = MonsterHudMapper.ToEmptyHud() with
        {
            Name = "搔鸟",
            HealthCurrent = 50,
            HealthMax = 1000,
        };
        MonsterHudDto slain = MonsterHudMapper.ToCompleted(live);

        MonsterHudDto failed = MonsterHudMapper.ToQuestFailed(
            live,
            slain,
            preferFrozenCompletion: true);

        Assert.Equal(0, failed.HealthCurrent);
        Assert.Equal(MonsterCompletionState.Failed, failed.CompletionState);
    }

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
            QuestAllowsCapture: true,
            CaptureThresholdPercent: 25);

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        var healthPercent = dto.HealthMax <= 0 ? 0 : dto.HealthCurrent / dto.HealthMax * 100.0;

        Assert.True(dto.IsCapturable);
        Assert.Equal(25, dto.CaptureThresholdPercent);
        Assert.True(CaptureRules.IsPastThreshold(healthPercent, dto.CaptureThresholdPercent!.Value));
    }

    [Fact]
    public void MergeLive_zero_health_hides_capture_banner_flag()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 0,
            HealthMax: 20000,
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true,
            CaptureThresholdPercent: 25);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.False(dto.IsCapturable);
        Assert.Equal(25, dto.CaptureThresholdPercent); // weaken line marker can remain
    }

    [Fact]
    public void MergeLive_zero_live_threshold_does_not_guess_weaken_line()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 4000,
            HealthMax: 20000,
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true,
            CaptureThresholdPercent: null);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.False(dto.IsCapturable);
        Assert.Null(dto.CaptureThresholdPercent);
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
    public void MergeLive_slay_quest_keeps_weaken_line_but_hides_capture_ui()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 1000,
            HealthMax: 20000,
            Parts: [new LivePartSnapshot("腹部", 500, 1000, false)],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: false,
            CaptureThresholdPercent: 25);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.False(dto.IsCapturable);
        Assert.Equal(25, dto.CaptureThresholdPercent);
        Assert.Equal(CaptureDisplayState.QuestRestricted, dto.CaptureState);
        var belly = Assert.Single(dto.Parts);
        Assert.True(belly.IsRecommendedTarget);
        Assert.Equal(new[] { ElementId.Water }, belly.WeakElements);
    }

    [Fact]
    public void MergeLive_marks_qurio_parts()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 10000,
            HealthMax: 20000,
            Parts: [new LivePartSnapshot("啮生虫", 100, 200, false, IsQurio: true)],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: false);

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        Assert.True(Assert.Single(dto.Parts).IsQurio);
    }

    [Fact]
    public void MergeLive_marks_anomaly_capture_state_and_hides_capture_threshold()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 10000,
            HealthMax: 20000,
            Parts: [],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: false,
            CaptureThresholdPercent: null,
            IsAnomaly: true);

        var dto = MonsterHudMapper.MergeLive(mapped, live);

        Assert.Equal(CaptureDisplayState.Anomaly, dto.CaptureState);
        Assert.False(dto.IsCapturable);
        Assert.Null(dto.CaptureThresholdPercent);
    }

    [Fact]
    public void BuildFromStatic_unions_hitzone_parts_for_weak_chips()
    {
        // Parts table only lists the severable tail — live HUD still needs head chips.
        var snapshot = new StaticMonsterSnapshot(
            Id: "monster_test",
            Title: "测试龙",
            Capturable: true,
            Hitzones:
            [
                new("头部", null, Fire: 30, Water: 0, Ice: 5, Thunder: 5, Dragon: 0),
                new("躯干", null, Fire: 15, Water: 0, Ice: 5, Thunder: 5, Dragon: 0),
                new("尾巴", null, Fire: 20, Water: 0, Ice: 5, Thunder: 10, Dragon: 0),
            ],
            Parts:
            [
                new("尾巴", Break: null, Sever: "300"),
            ]);

        var mapped = MonsterHudMapper.BuildFromStatic(snapshot);
        Assert.Contains(mapped.Parts, p => p.Name == "头部" && p.WeakElements.Contains(ElementId.Fire));
        Assert.Contains(mapped.Parts, p => p.Name == "尾巴" && p.IsSeverable);

        var live = new LiveMonsterSnapshot(
            HealthCurrent: 1000,
            HealthMax: 2000,
            Parts:
            [
                new LivePartSnapshot("头部", 100, 200, false),
                new LivePartSnapshot("尾巴", 50, 100, false),
            ],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true);

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        var head = Assert.Single(dto.Parts, p => p.Name == "头部");
        Assert.Equal(new[] { ElementId.Fire }, head.WeakElements);
        Assert.True(head.IsRecommendedTarget);
    }

    [Fact]
    public void BuildFromStatic_Ludroth_named_hitzones_mark_sponge_and_head()
    {
        var ludroth = new StaticMonsterSnapshot(
            Id: "monster_047_00",
            Title: "水兽",
            Capturable: true,
            Hitzones:
            [
                new("头部", null, Fire: 30, Water: 0, Ice: 5, Thunder: 5, Dragon: 5),
                new("海绵质", null, Fire: 30, Water: 0, Ice: 10, Thunder: 15, Dragon: 0),
                new("躯干", null, Fire: 10, Water: 25, Ice: 5, Thunder: 5, Dragon: 0),
                new("左腿", null, Fire: 20, Water: 0, Ice: 10, Thunder: 10, Dragon: 0),
                new("右腿", null, Fire: 20, Water: 0, Ice: 5, Thunder: 10, Dragon: 0),
                new("尾巴", null, Fire: 30, Water: 0, Ice: 5, Thunder: 10, Dragon: 5),
            ],
            Parts:
            [
                new("尾巴", Break: null, Sever: "450"),
            ]);

        var mapped = MonsterHudMapper.BuildFromStatic(ludroth);
        Assert.Equal(new[] { ElementId.Fire }, mapped.Recommended);

        var live = new LiveMonsterSnapshot(
            HealthCurrent: 50000,
            HealthMax: 80000,
            Parts:
            [
                new LivePartSnapshot("海绵质", 500, 1000, false),
                new LivePartSnapshot("头部", 100, 1000, false),
                new LivePartSnapshot("躯干", 2000, 3000, false),
            ],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: false);

        var dto = MonsterHudMapper.MergeLive(mapped, live);
        Assert.Equal(new[] { ElementId.Fire }, Assert.Single(dto.Parts, p => p.Name == "海绵质").WeakElements);
        Assert.Equal(new[] { ElementId.Fire }, Assert.Single(dto.Parts, p => p.Name == "头部").WeakElements);
        Assert.Equal(new[] { ElementId.Water }, Assert.Single(dto.Parts, p => p.Name == "躯干").WeakElements);
    }

    [Fact]
    public void MergeLive_shows_each_parts_own_best_element_not_only_overall_recommendation()
    {
        var snapshot = new StaticMonsterSnapshot(
            Id: "monster_part_weakness",
            Title: "测试龙",
            Capturable: true,
            Hitzones:
            [
                new("头部", null, Fire: 0, Water: 35, Ice: 5, Thunder: 0, Dragon: 0),
                new("尾巴", null, Fire: 0, Water: 5, Ice: 30, Thunder: 0, Dragon: 0),
            ],
            Parts: [new("尾巴", Break: null, Sever: "300")]);
        var mapped = MonsterHudMapper.BuildFromStatic(snapshot);
        var live = new LiveMonsterSnapshot(
            HealthCurrent: 1000,
            HealthMax: 2000,
            Parts: [new LivePartSnapshot("尾巴", 100, 300, false)],
            Ailments: [],
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true);

        var tail = Assert.Single(MonsterHudMapper.MergeLive(mapped, live).Parts);

        Assert.Equal(new[] { ElementId.Ice }, tail.WeakElements);
        Assert.False(tail.IsRecommendedTarget);
    }

    [Fact]
    public void ToResetHud_slay_quest_does_not_guess_weaken_line()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var dto = MonsterHudMapper.ToResetHud(mapped, questAllowsCapture: false);

        Assert.False(dto.IsCapturable);
        Assert.Null(dto.CaptureThresholdPercent);
        Assert.Equal(CaptureDisplayState.QuestRestricted, dto.CaptureState);
    }

    [Fact]
    public void ToResetHud_clears_hp_and_waits_for_live_weaken_threshold()
    {
        var mapped = MonsterHudMapper.BuildFromStatic(MagnamaloStatic());
        var dto = MonsterHudMapper.ToResetHud(mapped, questAllowsCapture: true);

        Assert.Equal("怨虎龙", dto.Name);
        Assert.Equal(0, dto.HealthCurrent);
        Assert.Equal(0, dto.HealthMax);
        Assert.True(dto.IsCapturable);
        Assert.Null(dto.CaptureThresholdPercent);
        Assert.Empty(dto.Parts);
        Assert.Equal(new[] { ElementId.Water }, dto.Recommended);
    }

    [Fact]
    public void BuildFromStatic_Volvidon_skips_RabbitConverted_focus_label()
    {
        var volvidon = new StaticMonsterSnapshot(
            Id: "monster_062_00",
            Title: "赤甲兽",
            Capturable: true,
            Hitzones:
            [
                new("RabbitConverted", null, Fire: 0, Water: 30, Ice: 20, Thunder: 15, Dragon: 0),
            ],
            Parts:
            [
                new("头部", Break: "Lv1: 300", Sever: null),
                new("尾巴", Break: null, Sever: null),
            ]);

        var mapped = MonsterHudMapper.BuildFromStatic(volvidon);

        Assert.Equal("头", mapped.FocusPartLabel);
    }
}
