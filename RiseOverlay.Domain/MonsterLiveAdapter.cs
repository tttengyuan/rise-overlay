namespace RiseOverlay.Domain;

/// <summary>
/// Pure adapter: fixture / extracted live combat fields → <see cref="LiveMonsterSnapshot"/>.
/// Keeps HunterPie <c>IMonster</c> out of Domain so unit tests use plain fixtures.
/// </summary>
public static class MonsterLiveAdapter
{
    private static readonly Dictionary<string, (string Key, string ShortName)> AilmentCatalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AILMENT_POISON"] = ("poison", "毒"),
            ["AILMENT_PARALYSIS"] = ("paralysis", "麻"),
            ["AILMENT_SLEEP"] = ("sleep", "眠"),
            ["AILMENT_BLAST"] = ("blast", "爆"),
            ["AILMENT_EXHAUST"] = ("exhaust", "减气"),
            ["AILMENT_STUN"] = ("stun", "晕眩"),
            ["STATUS_ENRAGE"] = ("enrage", "愤怒"),
            ["AILMENT_RIDE"] = ("ride", "骑乘"),
            ["AILMENT_MOUNT"] = ("mount", "骑乘"),
            ["AILMENT_FIRE"] = ("fire", "火异"),
            ["AILMENT_WATER"] = ("water", "水异"),
            ["AILMENT_ICE"] = ("ice", "冰异"),
            ["AILMENT_THUNDER"] = ("thunder", "雷异"),
        };

    /// <summary>Combat HUD only lists these status bars (plus stun on the status line).</summary>
    private static readonly HashSet<string> VisibleAilmentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "poison", "paralysis", "sleep", "blast", "exhaust",
    };

    public static LiveMonsterSnapshot ToSnapshot(MonsterLiveFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var parts = (fixture.Parts ?? Array.Empty<MonsterLivePartFixture>())
            .Where(IsRenderablePart)
            .Select(MapPart)
            .ToArray();

        var ailments = (fixture.Ailments ?? Array.Empty<MonsterLiveAilmentFixture>())
            .Where(a => !IsEnrageId(a.Id) && !IsStunId(a.Id))
            .Select(MapAilment)
            .Where(a => VisibleAilmentKeys.Contains(a.Key))
            .ToArray();

        var stun = (fixture.Ailments ?? Array.Empty<MonsterLiveAilmentFixture>())
            .FirstOrDefault(a => IsStunId(a.Id));

        var enrage = fixture.Enrage;
        TimeSpan? enrageRemaining = null;
        if (fixture.IsEnraged && enrage is { Timer: > 0 })
            enrageRemaining = TimeSpan.FromSeconds(enrage.Timer);

        double? stunBuildup = null;
        bool stunActive = false;
        TimeSpan? stunActiveRemaining = null;
        if (stun is not null)
        {
            if (stun.MaxBuildUp > 0)
                stunBuildup = stun.BuildUp / stun.MaxBuildUp * 100.0;
            stunActive = stun.Timer > 0;
            if (stunActive)
                stunActiveRemaining = TimeSpan.FromSeconds(stun.Timer);
        }

        double? staminaPercent = null;
        if (fixture.MaxStamina > 0)
            staminaPercent = fixture.Stamina / fixture.MaxStamina * 100.0;

        double? capturePercent = null;
        // A scanner can briefly observe MaxHealth=0/stale while a multiplayer monster is
        // initializing. Ignore impossible ratios instead of drawing a premature 100% line.
        if (double.IsFinite(fixture.CaptureThreshold)
            && fixture.CaptureThreshold is > 0 and < 1)
            capturePercent = fixture.CaptureThreshold * 100.0;

        return new LiveMonsterSnapshot(
            HealthCurrent: fixture.Health,
            HealthMax: fixture.MaxHealth,
            Parts: parts,
            Ailments: ailments,
            Status: new StatusLineModel(
                EnrageRemaining: enrageRemaining,
                StunBuildupPercent: stunBuildup,
                StunActive: stunActive,
                StunActiveRemaining: stunActiveRemaining,
                StaminaPercent: staminaPercent,
                DownRemaining: null),
            QuestAllowsCapture: fixture.QuestAllowsCapture,
            CaptureThresholdPercent: capturePercent,
            IsAnomaly: fixture.IsAnomaly);
    }

    /// <summary>
    /// Rise memory maps Kill flag → Slay (讨伐). Only Hunt and Capture quests show capture UI.
    /// </summary>
    public static bool ResolveQuestAllowsCapture(string? questTypeName, string? questLevelName = null)
    {
        _ = questLevelName;

        if (string.IsNullOrWhiteSpace(questTypeName))
            return true;

        return questTypeName switch
        {
            "Hunt" or "Capture" => true,
            "Slay" or "Special" or "Delivery" => false,
            _ => true,
        };
    }

    private static LiveAilmentSnapshot MapAilment(MonsterLiveAilmentFixture a)
    {
        var (key, shortName) = ResolveAilmentIdentity(a.Id, a.DisplayName);
        var isActive = a.Timer > 0;
        double percent = 0;
        if (!isActive && a.MaxBuildUp > 0)
            percent = Math.Clamp(a.BuildUp / a.MaxBuildUp * 100.0, 0, 100);
        return new LiveAilmentSnapshot(key, shortName, percent, isActive);
    }

    private static (string Key, string ShortName) ResolveAilmentIdentity(string id, string? displayName)
    {
        if (AilmentCatalog.TryGetValue(id, out var known))
            return known;

        var key = id;
        if (key.StartsWith("AILMENT_", StringComparison.OrdinalIgnoreCase))
            key = key["AILMENT_".Length..];
        else if (key.StartsWith("STATUS_", StringComparison.OrdinalIgnoreCase))
            key = key["STATUS_".Length..];
        key = key.ToLowerInvariant();

        var name = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
        return (key, name);
    }

    private static LivePartSnapshot MapPart(MonsterLivePartFixture p)
    {
        bool broken = !p.IsQurioThreshold && IsPartBroken(p);
        var (cur, max) = ResolvePartHp(p, broken);
        var name = string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName == "???"
            ? p.Id
            : PartNameSanitizer.Clean(p.DisplayName);
        if (string.IsNullOrWhiteSpace(name))
            name = p.Id;

        return new LivePartSnapshot(
            Name: name,
            CurrentHp: cur,
            MaxHp: max,
            IsBroken: broken,
            IsQurio: p.IsQurio,
            IsQurioThreshold: p.IsQurioThreshold,
            IsSeverable: p.IsSeverable,
            Flinch: p.Flinch,
            MaxFlinch: p.MaxFlinch);
    }

    /// <summary>
    /// HunterPie priority: Qurio (even on already-broken parts) &gt; Sever &gt; Break.
    /// Never fall through to flinch for break/sever pools — flinch regenerates.
    /// </summary>
    private static (double Current, double Max) ResolvePartHp(MonsterLivePartFixture p, bool broken)
    {
        // Active infection overlays the row — keep Qurio HP even if the part is already broken.
        if (p.IsQurio && p.MaxHealth > 0)
            return (p.Health, p.MaxHealth);

        if (broken)
        {
            if (p.IsSeverable && p.MaxSever > 0)
                return (0, p.MaxSever);
            if (p.MaxHealth > 0)
                return (0, p.MaxHealth);
            if (p.MaxSever > 0)
                return (0, p.MaxSever);
            return (0, 1);
        }

        // Severable tails often also expose breakable MaxHealth — prefer sever bar.
        if (p.IsSeverable && p.MaxSever > 0)
            return (p.Sever, p.MaxSever);

        if (p.MaxHealth > 0)
            return (p.Health, p.MaxHealth);

        if (p.MaxSever > 0)
            return (p.Sever, p.MaxSever);

        // Breakable/severable with collapsed max: treat as empty, do not show flinch.
        if (p.IsBreakable || p.IsSeverable)
            return (0, 1);

        return (0, 0);
    }

    private static bool IsRenderablePart(MonsterLivePartFixture p)
    {
        if (string.Equals(p.DisplayName, "???", StringComparison.Ordinal)
            || string.Equals(p.Id, "PART_UNKNOWN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Id, "PART_TO_BE_MAPPED", StringComparison.OrdinalIgnoreCase))
            return false;

        if (p.IsQurioThreshold)
            return true;

        if (p.IsQurio)
            return p.MaxHealth > 0 || IsPartBroken(p);

        // Hide pure flinch/stagger rows — they clutter the list and look like duplicate parts.
        if (p.IsBreakable || p.IsSeverable)
            return true;

        // Defensive: structural HP without flags still counts as a break/sever row.
        if (p.MaxHealth > 0 || p.MaxSever > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Rise break detection — avoid init false positives when MaxHealth is still 0.
    /// </summary>
    private static bool IsPartBroken(MonsterLivePartFixture p)
    {
        if (p.IsQurioThreshold)
            return false;

        if (p.IsStructurallyBroken)
            return true;

        // MHRMonsterPart already cross-validates native break/sever state. Once that
        // authoritative result is available, legacy flinch heuristics must not override it.
        if (p.HasAuthoritativeBreakState)
            return false;

        if (p.BreakCount > 0)
            return true;

        if (p.IsSeverable && p.MaxSever > 0 && p.Sever >= p.MaxSever
            && p.MaxFlinch > 0 && p.Flinch < p.MaxFlinch)
            return true;

        if (p.MaxHealth > 0 && p.Health <= 0 && !p.IsQurio)
            return true;

        // Rise collapses MaxHealth after a break. BreakCount is latched by the Rise controller
        // (flinch regenerates and must not un-break the row).
        if (p.IsBreakable && p.MaxHealth <= 0 && p.BreakCount > 0)
            return true;

        return false;
    }

    private static bool IsEnrageId(string id)
        => string.Equals(id, "STATUS_ENRAGE", StringComparison.OrdinalIgnoreCase)
           || string.Equals(id, "enrage", StringComparison.OrdinalIgnoreCase);

    private static bool IsStunId(string id)
        => string.Equals(id, "AILMENT_STUN", StringComparison.OrdinalIgnoreCase)
           || string.Equals(id, "stun", StringComparison.OrdinalIgnoreCase);
}

public sealed record MonsterLivePartFixture(
    string Id,
    string DisplayName,
    double Health,
    double MaxHealth,
    int BreakCount,
    bool IsQurio = false,
    double Flinch = 0,
    double MaxFlinch = 0,
    double Sever = 0,
    double MaxSever = 0,
    bool IsBreakable = false,
    bool IsSeverable = false,
    bool IsQurioThreshold = false,
    bool IsStructurallyBroken = false,
    bool HasAuthoritativeBreakState = false);

public sealed record MonsterLiveAilmentFixture(
    string Id,
    string? DisplayName,
    double Timer,
    double MaxTimer,
    double BuildUp,
    double MaxBuildUp);

public sealed record MonsterLiveFixture(
    string Name,
    int Id,
    double Health,
    double MaxHealth,
    double Stamina,
    double MaxStamina,
    /// <summary>0–1 ratio from game memory (IMonster.CaptureThreshold).</summary>
    double CaptureThreshold,
    bool IsEnraged,
    IReadOnlyList<MonsterLivePartFixture> Parts,
    IReadOnlyList<MonsterLiveAilmentFixture> Ailments,
    MonsterLiveAilmentFixture? Enrage,
    bool QuestAllowsCapture,
    bool IsAnomaly = false);
