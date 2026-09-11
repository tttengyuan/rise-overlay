namespace RiseOverlay.Domain;

public static class MonsterCompletionRules
{
    /// <summary>
    /// A newly attached MHRMonster starts at Health=-1/MaxHealth=0 until its first scan.
    /// That placeholder is not a corpse and must never complete a quest objective.
    /// </summary>
    public static bool IsConfirmedFinished(double health, double maxHealth)
        => double.IsFinite(health)
           && double.IsFinite(maxHealth)
           && health >= 0
           && maxHealth > 0
           && MonsterHealthDisplay.ForHud(health, maxHealth) <= 0;

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
