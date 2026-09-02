namespace RiseOverlay.Domain;

public enum DpsCalculationMode
{
    RelativeToQuest,
    RelativeToJoin,
    RelativeToFirstHit,
}

/// <summary>
/// HunterPie damage meter DPS formula, kept separate so the compact HUD can
/// share the same three timing strategies without using a rolling window.
/// </summary>
public static class OriginalDpsCalculator
{
    public static double Calculate(
        double totalDamage,
        double questElapsed,
        double joinedAt,
        double firstHitAt,
        DpsCalculationMode mode)
    {
        double timeElapsed = mode switch
        {
            DpsCalculationMode.RelativeToQuest => questElapsed,
            DpsCalculationMode.RelativeToJoin => questElapsed - Math.Min(questElapsed, joinedAt),
            DpsCalculationMode.RelativeToFirstHit => questElapsed - Math.Min(questElapsed, firstHitAt),
            _ => 1,
        };

        return totalDamage / Math.Max(1, timeElapsed);
    }
}
