namespace RiseOverlay.Domain;

public sealed record StatusLineModel(
    TimeSpan? EnrageRemaining,
    double? StunBuildupPercent,
    bool StunActive,
    TimeSpan? StunActiveRemaining,
    double? StaminaPercent,
    TimeSpan? DownRemaining);

public sealed record PartDto(
    string Name,
    double CurrentHp,
    double MaxHp,
    bool IsSeverable,
    bool IsBroken,
    IReadOnlyList<ElementId> WeakElements,
    bool IsQurio = false,
    bool IsRecommendedTarget = false,
    bool IsQurioThreshold = false,
    double Flinch = 0,
    double MaxFlinch = 0);

public sealed record AilmentDto(
    string Key,
    string DisplayName,
    double Percent,
    bool IsActive);

public sealed record MonsterHudDto(
    string Name,
    double HealthCurrent,
    double HealthMax,
    bool IsCapturable,
    double? CaptureThresholdPercent,
    IReadOnlyList<ElementId> OverallElementsOrdered,
    IReadOnlyList<ElementId> Recommended,
    StatusLineModel Status,
    IReadOnlyList<PartDto> Parts,
    IReadOnlyList<AilmentDto> Ailments,
    CaptureDisplayState CaptureState = CaptureDisplayState.Capturable);

public sealed record DpsEntryDto(string Name, bool IsSelf, double Dps, long TotalDamage);

public sealed record DpsPanelDto(
    IReadOnlyList<DpsEntryDto> Entries,
    double? HuntDurationSeconds = null,
    /// <summary>Total shown by this panel. Rise custom HUD supplies current-target session damage.</summary>
    long? QuestTotalDamage = null,
    /// <summary>Damage accumulated after locking the current monster, when known.</summary>
    long? LockedTargetDamage = null,
    string? LockedTargetName = null);

public enum BriefingTargetKind
{
    Quest,
    Invasion,
}

public sealed record QuestBriefingTargetDto(
    string Name,
    bool IsCapturable,
    bool HasSeverableTail,
    string? FocusPartLabel,
    IReadOnlyList<ElementId> OverallElementsOrdered,
    IReadOnlyList<ElementId> Recommended,
    BriefingTargetKind Kind = BriefingTargetKind.Quest,
    bool IsDefeated = false,
    CaptureDisplayState CaptureState = CaptureDisplayState.Capturable);

public sealed record QuestBriefingDto(IReadOnlyList<QuestBriefingTargetDto> Targets);
