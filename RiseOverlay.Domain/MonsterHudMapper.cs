namespace RiseOverlay.Domain;

/// <summary>
/// Pure mapping: static snapshots + live combat state → overlay DTOs.
/// Recommended elements use max-per-element across all hitzone rows (every part/phase),
/// then <see cref="ElementRecommend.FromHitzones"/> — so Valstrax-like head 25/25/25/25/0 yields four recommends.
/// </summary>
public static class MonsterHudMapper
{
    public const double DefaultCaptureThresholdPercent = 25;

    // UI display order: Fire, Water, Ice, Thunder, Dragon (matches ElementRecommend).
    private static readonly ElementId[] OverallDisplayOrder =
    [
        ElementId.Fire,
        ElementId.Water,
        ElementId.Ice,
        ElementId.Thunder,
        ElementId.Dragon,
    ];

    public static MonsterStaticMapped BuildFromStatic(StaticMonsterSnapshot monster)
    {
        ArgumentNullException.ThrowIfNull(monster);

        var aggregated = AggregateMaxElements(monster.Hitzones);
        var recommended = ElementRecommend.FromHitzones(aggregated);
        var parts = BuildParts(monster);
        var hasSeverableTail = parts.Any(p => p.IsSeverable);
        var focus = PickFocusPartLabel(monster, parts);

        return new MonsterStaticMapped(
            Name: monster.Title,
            IsCapturable: monster.Capturable,
            CaptureThresholdPercent: monster.Capturable ? DefaultCaptureThresholdPercent : null,
            HasSeverableTail: hasSeverableTail,
            FocusPartLabel: focus,
            OverallElementsOrdered: OverallDisplayOrder,
            Recommended: recommended,
            Parts: parts);
    }

    public static QuestBriefingTargetDto ToBriefingTarget(MonsterStaticMapped mapped)
    {
        ArgumentNullException.ThrowIfNull(mapped);
        return new QuestBriefingTargetDto(
            Name: mapped.Name,
            IsCapturable: mapped.IsCapturable,
            HasSeverableTail: mapped.HasSeverableTail,
            FocusPartLabel: mapped.FocusPartLabel,
            OverallElementsOrdered: mapped.OverallElementsOrdered,
            Recommended: mapped.Recommended,
            CaptureState: CaptureRules.ResolveDisplayState(
                mapped.IsCapturable,
                questAllowsCapture: true,
                isAnomaly: false));
    }

    /// <summary>
    /// Fresh hunt shell after quest reset — empty HP, capture/weaken line from static, no live combat residue.
    /// </summary>
    public static MonsterHudDto ToResetHud(MonsterStaticMapped mapped, bool questAllowsCapture)
    {
        ArgumentNullException.ThrowIfNull(mapped);

        bool showCapture = CaptureRules.ShowCaptureUi(mapped.IsCapturable, questAllowsCapture);
        // No static 25% guess: wait for the quest-scaled threshold read from live memory.
        double? weakenThreshold = null;

        return new MonsterHudDto(
            Name: mapped.Name,
            HealthCurrent: 0,
            HealthMax: 0,
            IsCapturable: showCapture,
            CaptureThresholdPercent: weakenThreshold,
            OverallElementsOrdered: mapped.OverallElementsOrdered,
            Recommended: mapped.Recommended ?? Array.Empty<ElementId>(),
            Status: EmptyStatus,
            Parts: Array.Empty<PartDto>(),
            Ailments: Array.Empty<AilmentDto>(),
            CaptureState: CaptureRules.ResolveDisplayState(
                mapped.IsCapturable,
                questAllowsCapture,
                isAnomaly: false));
    }

    public static MonsterHudDto ToEmptyHud()
        => new(
            Name: "",
            HealthCurrent: 0,
            HealthMax: 0,
            IsCapturable: false,
            CaptureThresholdPercent: null,
            OverallElementsOrdered: OverallDisplayOrder,
            Recommended: Array.Empty<ElementId>(),
            Status: EmptyStatus,
            Parts: Array.Empty<PartDto>(),
            Ailments: Array.Empty<AilmentDto>());

    /// <summary>
    /// Death/capture events are authoritative. Rise can keep a small positive HP value
    /// in the health component during the finish animation, so never freeze that residue.
    /// </summary>
    public static MonsterHudDto ToCompleted(MonsterHudDto lastCombatFrame)
    {
        ArgumentNullException.ThrowIfNull(lastCombatFrame);
        return lastCombatFrame with
        {
            HealthCurrent = 0,
            IsCapturable = false,
        };
    }

    private static readonly StatusLineModel EmptyStatus = new(
        EnrageRemaining: null,
        StunBuildupPercent: null,
        StunActive: false,
        StunActiveRemaining: null,
        StaminaPercent: null,
        DownRemaining: null);

    /// <summary>
    /// Fallback when static table miss: HP/parts/ailments still render; no weakness chips.
    /// </summary>
    public static MonsterStaticMapped CreateFallbackStatic(
        string name,
        bool isCapturable,
        double? captureThresholdPercent = DefaultCaptureThresholdPercent)
    {
        return new MonsterStaticMapped(
            Name: name,
            IsCapturable: isCapturable,
            CaptureThresholdPercent: isCapturable ? captureThresholdPercent : null,
            HasSeverableTail: false,
            FocusPartLabel: null,
            OverallElementsOrdered: OverallDisplayOrder,
            Recommended: Array.Empty<ElementId>(),
            Parts: Array.Empty<MappedPartStatic>());
    }

    public static MonsterHudDto MergeLive(MonsterStaticMapped staticSnapshot, LiveMonsterSnapshot live)
    {
        ArgumentNullException.ThrowIfNull(staticSnapshot);
        ArgumentNullException.ThrowIfNull(live);

        var showCapture = CaptureRules.ShowCaptureUi(staticSnapshot.IsCapturable, live.QuestAllowsCapture);
        // Capture banner only while the monster is still alive and quest/memory allow capture.
        bool canCapture = showCapture
                          && live.CaptureThresholdPercent is > 0
                          && live.HealthCurrent > 0;

        // The original HunterPie/Rise path is authoritative here: captureHealth / live MaxHealth.
        // A static 25% fallback races multiplayer HP scaling and can draw the line too early.
        double? weakenThreshold = staticSnapshot.IsCapturable && !live.IsAnomaly
            ? live.CaptureThresholdPercent
            : null;

        var recommended = staticSnapshot.Recommended ?? Array.Empty<ElementId>();
        var recommendedSet = recommended.Count > 0
            ? new HashSet<ElementId>(recommended)
            : null;

        var staticParts = staticSnapshot.Parts ?? Array.Empty<MappedPartStatic>();

        var parts = (live.Parts ?? Array.Empty<LivePartSnapshot>())
            .Select(lp =>
            {
                var sp = FindStaticPart(staticParts, lp.Name);
                var partWeak = sp?.WeakElements ?? Array.Empty<ElementId>();
                bool isRecommendedTarget = recommendedSet is not null
                    && partWeak.Any(recommendedSet.Contains);
                var displayName = PartNameSanitizer.Clean(lp.Name);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = lp.Name;
                return new PartDto(
                    Name: displayName,
                    CurrentHp: lp.CurrentHp,
                    MaxHp: lp.MaxHp,
                    // Live flag only — never inherit static IsSeverable (name mismatch / wrong 已断尾).
                    IsSeverable: lp.IsSeverable,
                    IsBroken: lp.IsBroken,
                    WeakElements: partWeak,
                    IsQurio: lp.IsQurio,
                    IsRecommendedTarget: isRecommendedTarget,
                    IsQurioThreshold: lp.IsQurioThreshold,
                    Flinch: lp.Flinch,
                    MaxFlinch: lp.MaxFlinch);
            })
            .ToArray();

        var ailments = (live.Ailments ?? Array.Empty<LiveAilmentSnapshot>())
            .Where(a => !string.Equals(a.Key, "stun", StringComparison.OrdinalIgnoreCase))
            .Where(a => a.IsActive || a.Percent > 0)
            .Select(a => new AilmentDto(a.Key, a.DisplayName, a.Percent, a.IsActive))
            .ToArray();

        return new MonsterHudDto(
            Name: staticSnapshot.Name,
            HealthCurrent: live.HealthCurrent,
            HealthMax: live.HealthMax,
            IsCapturable: canCapture,
            CaptureThresholdPercent: weakenThreshold,
            OverallElementsOrdered: staticSnapshot.OverallElementsOrdered,
            Recommended: recommended,
            Status: live.Status,
            Parts: parts,
            Ailments: ailments,
            CaptureState: CaptureRules.ResolveDisplayState(
                staticSnapshot.IsCapturable,
                live.QuestAllowsCapture,
                live.IsAnomaly));
    }

    private static MappedPartStatic? FindStaticPart(IReadOnlyList<MappedPartStatic> parts, string liveName)
    {
        foreach (var sp in parts)
        {
            if (PartNameSanitizer.Matches(sp.Name, liveName))
                return sp;
        }

        return null;
    }

    private static Dictionary<ElementId, int> AggregateMaxElements(IReadOnlyList<StaticHitzoneRow> hitzones)
    {
        var max = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 0,
            [ElementId.Water] = 0,
            [ElementId.Ice] = 0,
            [ElementId.Thunder] = 0,
            [ElementId.Dragon] = 0,
        };

        foreach (var row in hitzones ?? Array.Empty<StaticHitzoneRow>())
        {
            max[ElementId.Fire] = Math.Max(max[ElementId.Fire], row.Fire);
            max[ElementId.Water] = Math.Max(max[ElementId.Water], row.Water);
            max[ElementId.Ice] = Math.Max(max[ElementId.Ice], row.Ice);
            max[ElementId.Thunder] = Math.Max(max[ElementId.Thunder], row.Thunder);
            max[ElementId.Dragon] = Math.Max(max[ElementId.Dragon], row.Dragon);
        }

        return max;
    }

    private static IReadOnlyList<MappedPartStatic> BuildParts(StaticMonsterSnapshot monster)
    {
        var hitzones = monster.Hitzones ?? Array.Empty<StaticHitzoneRow>();
        var partRows = monster.Parts ?? Array.Empty<StaticPartRow>();

        // Union break/sever table with every hitzone part name — Parts alone is often only
        // breakable/severable rows, which dropped WeakElements chips on other live parts.
        var orderedNames = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Consider(string? raw)
        {
            var cleaned = PartNameSanitizer.Clean(raw);
            if (string.IsNullOrWhiteSpace(cleaned))
                return;
            // Placeholder rows from bad converters — never map to live PART_* names.
            if (cleaned is "肉质" or "damage")
                return;

            var key = PartNameSanitizer.NormalizeKey(cleaned);
            if (key.Length == 0 || !seenKeys.Add(key))
                return;

            orderedNames.Add(cleaned);
        }

        foreach (var p in partRows)
            Consider(p.Part);
        foreach (var h in hitzones)
            Consider(h.Part);

        if (orderedNames.Count == 0 && partRows.Count > 0)
        {
            foreach (var p in partRows)
                Consider(p.Part);
        }

        return orderedNames
            .Select(name =>
            {
                var matchedRows = hitzones
                    .Where(h => PartNameSanitizer.Matches(h.Part, name))
                    .ToArray();
                var elements = AggregateMaxElements(matchedRows);
                var meta = partRows.FirstOrDefault(p => PartNameSanitizer.Matches(p.Part, name));
                return new MappedPartStatic(
                    Name: name,
                    IsSeverable: meta is not null && IsSeverable(meta),
                    WeakElements: PartWeakness.ForPart(elements));
            })
            .ToArray();
    }

    private static bool IsSeverable(StaticPartRow part)
        => !string.IsNullOrWhiteSpace(part.Sever);

    private static string? PickFocusPartLabel(
        StaticMonsterSnapshot monster,
        IReadOnlyList<MappedPartStatic> parts)
    {
        // Highest peak elemental weakness among parts; prefer head-like names on ties.
        string? bestName = null;
        var bestScore = -1;

        foreach (var group in (monster.Hitzones ?? Array.Empty<StaticHitzoneRow>())
                     .GroupBy(h => PartNameSanitizer.Clean(h.Part), StringComparer.OrdinalIgnoreCase))
        {
            var max = AggregateMaxElements(group.ToArray());
            var score = max.Values.DefaultIfEmpty(0).Max();
            if (score <= 0)
                continue;

            var name = group.Key;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (score > bestScore || (score == bestScore && IsHeadLike(name) && !IsHeadLike(bestName)))
            {
                bestScore = score;
                bestName = name;
            }
        }

        if (bestName is not null)
            return ToShortLabel(bestName);

        var breakable = (monster.Parts ?? Array.Empty<StaticPartRow>())
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Break));
        if (breakable is not null)
            return ToShortLabel(PartNameSanitizer.Clean(breakable.Part));

        return parts.FirstOrDefault()?.Name is { } n ? ToShortLabel(n) : null;
    }

    private static bool IsHeadLike(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && (name.Contains("头", StringComparison.Ordinal) || name.Contains("頭部", StringComparison.Ordinal));

    private static string ToShortLabel(string partName)
    {
        var cleaned = PartNameSanitizer.Clean(partName);
        // Keep simple: "头部" → "头"; otherwise use the part name as-is.
        if (cleaned.EndsWith("部", StringComparison.Ordinal) && cleaned.Length >= 2)
            return cleaned[..^1];
        return cleaned;
    }
}
