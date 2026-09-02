namespace RiseOverlay.Domain;

/// <summary>
/// Domain-facing static monster data (filled from Data DTOs at the UI/Data boundary).
/// </summary>
public sealed record StaticHitzoneRow(
    string Part,
    int? Phase,
    int Fire,
    int Water,
    int Ice,
    int Thunder,
    int Dragon);

public sealed record StaticPartRow(
    string Part,
    string? Break,
    string? Sever);

public sealed record StaticMonsterSnapshot(
    string Id,
    string Title,
    bool Capturable,
    IReadOnlyList<StaticHitzoneRow> Hitzones,
    IReadOnlyList<StaticPartRow> Parts);

public sealed record LivePartSnapshot(
    string Name,
    double CurrentHp,
    double MaxHp,
    bool IsBroken,
    bool IsQurio = false);

public sealed record LiveAilmentSnapshot(
    string Key,
    string DisplayName,
    double Percent,
    bool IsActive);

public sealed record LiveMonsterSnapshot(
    double HealthCurrent,
    double HealthMax,
    IReadOnlyList<LivePartSnapshot> Parts,
    IReadOnlyList<LiveAilmentSnapshot> Ailments,
    StatusLineModel Status,
    bool QuestAllowsCapture,
    /// <summary>Live capture threshold as percent (0–100). When set, preferred over static default.</summary>
    double? CaptureThresholdPercent = null,
    bool IsAnomaly = false);

/// <summary>
/// Static-derived overlay fields used by briefing and as the base for <see cref="MonsterHudMapper.MergeLive"/>.
/// </summary>
public sealed record MonsterStaticMapped(
    string Name,
    bool IsCapturable,
    double? CaptureThresholdPercent,
    bool HasSeverableTail,
    string? FocusPartLabel,
    IReadOnlyList<ElementId> OverallElementsOrdered,
    IReadOnlyList<ElementId> Recommended,
    IReadOnlyList<MappedPartStatic> Parts);

public sealed record MappedPartStatic(
    string Name,
    bool IsSeverable,
    IReadOnlyList<ElementId> WeakElements);
