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
            Recommended: mapped.Recommended);
    }

    public static MonsterHudDto MergeLive(MonsterStaticMapped staticSnapshot, LiveMonsterSnapshot live)
    {
        ArgumentNullException.ThrowIfNull(staticSnapshot);
        ArgumentNullException.ThrowIfNull(live);

        var showCapture = CaptureRules.ShowCaptureUi(staticSnapshot.IsCapturable, live.QuestAllowsCapture);
        var threshold = showCapture ? staticSnapshot.CaptureThresholdPercent : null;

        var staticByName = staticSnapshot.Parts
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var parts = (live.Parts ?? Array.Empty<LivePartSnapshot>())
            .Select(lp =>
            {
                staticByName.TryGetValue(lp.Name, out var sp);
                return new PartDto(
                    Name: lp.Name,
                    CurrentHp: lp.CurrentHp,
                    MaxHp: lp.MaxHp,
                    IsSeverable: sp?.IsSeverable ?? false,
                    IsBroken: lp.IsBroken,
                    WeakElements: sp?.WeakElements ?? Array.Empty<ElementId>());
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
            IsCapturable: showCapture,
            CaptureThresholdPercent: threshold,
            OverallElementsOrdered: staticSnapshot.OverallElementsOrdered,
            Recommended: staticSnapshot.Recommended,
            Status: live.Status,
            Parts: parts,
            Ailments: ailments);
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
        var partRows = monster.Parts ?? Array.Empty<StaticPartRow>();
        if (partRows.Count == 0)
        {
            // Fall back to unique hitzone part names when Parts table is empty.
            partRows = (monster.Hitzones ?? Array.Empty<StaticHitzoneRow>())
                .Select(h => h.Part)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(n => new StaticPartRow(n, null, null))
                .ToArray();
        }

        return partRows
            .Select(p =>
            {
                var elements = AggregateMaxElements(
                    (monster.Hitzones ?? Array.Empty<StaticHitzoneRow>())
                        .Where(h => string.Equals(h.Part, p.Part, StringComparison.OrdinalIgnoreCase))
                        .ToArray());
                return new MappedPartStatic(
                    Name: p.Part,
                    IsSeverable: IsSeverable(p),
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
                     .GroupBy(h => h.Part, StringComparer.OrdinalIgnoreCase))
        {
            var max = AggregateMaxElements(group.ToArray());
            var score = max.Values.DefaultIfEmpty(0).Max();
            if (score <= 0)
                continue;

            var name = group.Key;
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
            return ToShortLabel(breakable.Part);

        return parts.FirstOrDefault()?.Name is { } n ? ToShortLabel(n) : null;
    }

    private static bool IsHeadLike(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && (name.Contains("头", StringComparison.Ordinal) || name.Contains("頭部", StringComparison.Ordinal));

    private static string ToShortLabel(string partName)
    {
        // Keep simple: "头部" → "头"; otherwise use the part name as-is.
        if (partName.EndsWith("部", StringComparison.Ordinal) && partName.Length >= 2)
            return partName[..^1];
        return partName;
    }
}
