namespace RiseOverlay.Domain;

public static class ScopedDamageRules
{
    public static double ResolveQuestElapsed(double observedGameElapsed)
        => double.IsFinite(observedGameElapsed)
            ? Math.Max(0, observedGameElapsed)
            : 0;

    /// <summary>
    /// Rise can briefly collapse QUEST_TIMER during the death→result transition while the
    /// HUD still shows a live DPS card. A confirmed objective stamp floors the display against
    /// that collapse, but must not freeze the live hunt clock — false early completions
    /// (area remount / stale death) would otherwise stick 用时 minutes behind the game.
    /// </summary>
    public static double StabilizeDisplayedElapsed(
        double previousDisplayed,
        double incomingGameElapsed,
        double? confirmedObjectiveElapsed,
        bool allowBackwardReset = false)
    {
        if (allowBackwardReset)
            return ResolveQuestElapsed(incomingGameElapsed);

        double floor = 0;
        if (confirmedObjectiveElapsed is { } confirmed
            && LooksHealthy(confirmed)
            && !(previousDisplayed > 5 && confirmed < previousDisplayed - 5))
            floor = confirmed;

        if (double.IsFinite(previousDisplayed)
            && previousDisplayed > 0
            && (!double.IsFinite(incomingGameElapsed) || incomingGameElapsed <= 0))
            return Math.Max(previousDisplayed, floor);

        double incoming = ResolveQuestElapsed(incomingGameElapsed);
        if (previousDisplayed > 5 && incoming < previousDisplayed - 5)
            return Math.Max(previousDisplayed, floor);

        // Collapsed/near-zero live samples after an objective stamp: hold the floor.
        if (floor > 0 && (LooksCollapsed(incoming) || incoming + 5 < floor))
            return Math.Max(previousDisplayed > 0 ? previousDisplayed : floor, floor);

        return Math.Max(incoming, floor);
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
        // Rise can freeze a ~0 result stamp after a healthy live hunt clock.
        if (LooksCollapsed(resolvedResultElapsed) && LooksHealthy(previousLiveElapsed))
            return previousLiveElapsed;
        if (double.IsFinite(resolvedResultElapsed) && resolvedResultElapsed > 0)
            return resolvedResultElapsed;
        if (double.IsFinite(previousLiveElapsed) && previousLiveElapsed > 0)
            return previousLiveElapsed;
        return 1;
    }

    /// <summary>
    /// Near-zero QUEST_TIMER samples during death→result are not trustworthy hunt clocks.
    /// </summary>
    public static bool LooksCollapsed(double elapsed)
        => double.IsFinite(elapsed) && elapsed > 0 && elapsed <= 5;

    public static bool LooksHealthy(double elapsed)
        => double.IsFinite(elapsed) && elapsed > 5;

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

    /// <summary>
    /// Native hunt stats can briefly answer 0 after a stage flicker or monster remount.
    /// Keep the peak per-entity counters so the DPS card does not collapse mid-hunt.
    /// </summary>
    public static (Dictionary<int, long> ByEntity, long Total) MergePeakSnapshot(
        IReadOnlyDictionary<int, long> previous,
        long previousTotal,
        IReadOnlyDictionary<int, long> incoming,
        long incomingTotal)
    {
        var merged = new Dictionary<int, long>();
        foreach ((int index, long damage) in previous)
            merged[index] = Math.Max(0, damage);

        foreach ((int index, long damage) in incoming)
        {
            long next = Math.Max(0, damage);
            merged[index] = merged.TryGetValue(index, out long prior)
                ? Math.Max(prior, next)
                : next;
        }

        long total = Math.Max(Math.Max(0, previousTotal), Math.Max(0, incomingTotal));
        long sum = merged.Values.Sum();
        if (sum > total)
            total = sum;

        return (merged, total);
    }
}
