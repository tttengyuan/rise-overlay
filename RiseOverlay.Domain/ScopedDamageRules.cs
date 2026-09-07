namespace RiseOverlay.Domain;

public static class ScopedDamageRules
{
    /// <summary>
    /// The compact panel scopes damage to the selected monster, but its clock remains
    /// the real quest clock just like HunterPie's original damage meter.
    /// </summary>
    public static double? HuntDuration(double questElapsed)
        => questElapsed > 0 ? questElapsed : null;

    /// <summary>
    /// A target switch resets the target's damage/first-hit session, while the stored
    /// timestamp stays in absolute quest time so the original DPS strategies remain valid.
    /// </summary>
    public static double ResolveFirstHitAt(
        double currentFirstHitAt,
        long scopedDamage,
        double questElapsed)
        => scopedDamage > 0 && currentFirstHitAt < 0
            ? Math.Max(0, questElapsed)
            : currentFirstHitAt;

    public static double CalculateDps(
        long scopedDamage,
        double questElapsed,
        double joinedAt,
        double firstHitAt,
        DpsCalculationMode mode)
        => OriginalDpsCalculator.Calculate(
            totalDamage: scopedDamage,
            questElapsed: Math.Max(0, questElapsed),
            joinedAt: Math.Max(0, joinedAt),
            firstHitAt,
            mode);

    public static Dictionary<int, long> SubtractBaseline(
        IReadOnlyDictionary<int, long> current,
        IReadOnlyDictionary<int, long> baseline)
    {
        var result = new Dictionary<int, long>();
        foreach ((int entityIndex, long damage) in current)
        {
            long startingDamage = baseline.GetValueOrDefault(entityIndex, 0);
            result[entityIndex] = Math.Max(0, damage - startingDamage);
        }

        return result;
    }
}
