namespace RiseOverlay.Domain;

public static class MonsterCompletionRules
{
    /// <summary>
    /// Rise reports zero-health/death before its authoritative capture event, and may then
    /// repeat capture every scan. Accept the first finish and the one-way Slain→Captured upgrade.
    /// </summary>
    public static bool ShouldApplyMonsterFinish(
        MonsterCompletionState existing,
        MonsterCompletionState incoming)
        => existing is MonsterCompletionState.None
           || (existing is MonsterCompletionState.Slain
               && incoming is MonsterCompletionState.Captured);
}
