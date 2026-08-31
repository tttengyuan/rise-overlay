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
        };

    public static LiveMonsterSnapshot ToSnapshot(MonsterLiveFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var parts = (fixture.Parts ?? Array.Empty<MonsterLivePartFixture>())
            .Select(p => new LivePartSnapshot(
                Name: string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName,
                CurrentHp: p.Health,
                MaxHp: p.MaxHealth,
                IsBroken: IsPartBroken(p)))
            .ToArray();

        var ailments = (fixture.Ailments ?? Array.Empty<MonsterLiveAilmentFixture>())
            .Where(a => !IsEnrageId(a.Id) && !IsStunId(a.Id))
            .Select(MapAilment)
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
        if (fixture.CaptureThreshold > 0)
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
            CaptureThresholdPercent: capturePercent);
    }

    /// <summary>
    /// Best-effort: Slay quests disallow capture; Hunt/Capture allow; unknown/null defaults to true.
    /// Species capturability (elder etc.) is applied later via static data + <see cref="CaptureRules"/>.
    /// </summary>
    public static bool ResolveQuestAllowsCapture(string? questTypeName)
    {
        if (string.IsNullOrWhiteSpace(questTypeName))
            return true;

        return !string.Equals(questTypeName, "Slay", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsPartBroken(MonsterLivePartFixture p)
        => p.BreakCount > 0
           || (p.MaxHealth > 0 && p.Health <= 0)
           || (p.MaxHealth <= 0 && p.BreakCount > 0);

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
    int BreakCount);

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
    bool QuestAllowsCapture);
