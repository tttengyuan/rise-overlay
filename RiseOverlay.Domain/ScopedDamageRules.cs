namespace RiseOverlay.Domain;

public static class ScopedDamageRules
{
    public static double ResolveQuestElapsed(double observedGameElapsed)
        => double.IsFinite(observedGameElapsed)
            ? Math.Max(0, observedGameElapsed)
            : 0;

    /// <summary>
    /// Rise can briefly collapse QUEST_TIMER during the death→result transition while the
    /// HUD still shows a live DPS card. Prefer a confirmed objective completion time, and
    /// otherwise ignore sudden backward jumps of the live timer.
    /// </summary>
    public static double StabilizeDisplayedElapsed(
        double previousDisplayed,
        double incomingGameElapsed,
        double? confirmedObjectiveElapsed,
        bool allowBackwardReset = false)
    {
        if (allowBackwardReset)
            return ResolveQuestElapsed(incomingGameElapsed);

        if (confirmedObjectiveElapsed is { } confirmed
            && double.IsFinite(confirmed)
            && confirmed > 0
            // A collapsed objective stamp must not override a healthy live card clock.
            && !(previousDisplayed > 5 && confirmed < previousDisplayed - 5))
            return confirmed;

        if (double.IsFinite(previousDisplayed)
            && previousDisplayed > 0
            && (!double.IsFinite(incomingGameElapsed) || incomingGameElapsed <= 0))
            return previousDisplayed;

        double incoming = ResolveQuestElapsed(incomingGameElapsed);
        if (previousDisplayed > 5 && incoming < previousDisplayed - 5)
            return previousDisplayed;

        return incoming;
    }

    /// <summary>
    /// A result clock has already applied success/failure semantics. Do not run it back
    /// through live-clock anti-collapse rules; only use the last live sample when the
    /// resolved result itself is unusable.
    /// </summary>
    public static double ResolveTerminalElapsed(
        double resolvedResultElapsed,
        double previousLiveElapsed)
    {
        if (double.IsFinite(resolvedResultElapsed) && resolvedResultElapsed > 0)
            return resolvedResultElapsed;
        if (double.IsFinite(previousLiveElapsed) && previousLiveElapsed > 0)
            return previousLiveElapsed;
        return 1;
    }

    /// <summary>
    /// The compact panel scopes damage to the selected monster, but its clock remains
    /// the real quest clock just like HunterPie's original damage meter.
    /// </summary>
    public static double? HuntDuration(double questElapsed)
        => questElapsed > 0 ? questElapsed : null;

    /// <summary>
    /// The first-hit timestamp stays in absolute quest time so the original HunterPie
    /// DPS strategies remain valid while the numerator is scoped per monster.
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

    /// <summary>
    /// Per-monster native counters already have the correct scope. Keep the whole
    /// snapshot so damage dealt before lock-on, or while another monster was selected,
    /// is not discarded.
    /// </summary>
    public static Dictionary<int, long> UseFullTargetSnapshot(
        IReadOnlyDictionary<int, long> current)
    {
        var result = new Dictionary<int, long>();
        foreach ((int entityIndex, long damage) in current)
            result[entityIndex] = Math.Max(0, damage);

        return result;
    }
}
