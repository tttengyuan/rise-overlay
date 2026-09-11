namespace RiseOverlay.Domain;

/// <summary>
/// Tracks objective completion per quest generation and per monster instance.
/// A target list may be filled late; changing it always recomputes the candidate.
/// </summary>
public sealed class QuestCompletionClock
{
    private const double MaxReasonableQuestDurationSeconds = 24 * 60 * 60;
    private readonly object _sync = new();
    private readonly Dictionary<string, Completion> _completionsByInstance =
        new(StringComparer.Ordinal);
    private List<string> _targetSpecies = [];
    private long _generation;
    private int _expectedTargetCount;
    private double? _confirmedElapsed;
    private double _peakLiveElapsed;

    public double? ConfirmedElapsed
    {
        get
        {
            lock (_sync)
                return _confirmedElapsed;
        }
    }

    public long BeginQuest(int expectedTargetCount)
    {
        lock (_sync)
        {
            _generation++;
            _expectedTargetCount = Math.Max(0, expectedTargetCount);
            _targetSpecies = [];
            _completionsByInstance.Clear();
            _confirmedElapsed = null;
            _peakLiveElapsed = 0;
            return _generation;
        }
    }

    /// <summary>
    /// Remembers the highest healthy live quest timer seen this hunt so a collapsed
    /// death→result QUEST_TIMER cannot stamp objective completion as ~0.
    /// </summary>
    public void ObserveLiveElapsed(double elapsed)
    {
        if (!IsValid(elapsed))
            return;

        lock (_sync)
            _peakLiveElapsed = Math.Max(_peakLiveElapsed, elapsed);
    }

    public void ConfigureTargets(
        long generation,
        IReadOnlyList<string> targetSpecies,
        int expectedTargetCount = 0)
    {
        lock (_sync)
        {
            if (generation != _generation)
                return;

            _expectedTargetCount = Math.Max(_expectedTargetCount, expectedTargetCount);
            _targetSpecies = targetSpecies
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            RecomputeConfirmedElapsed();
        }
    }

    public void RecordCompletion(
        long generation,
        string instanceKey,
        string species,
        double elapsed)
    {
        if (string.IsNullOrWhiteSpace(instanceKey)
            || string.IsNullOrWhiteSpace(species))
            return;

        lock (_sync)
        {
            if (generation != _generation
                || _completionsByInstance.ContainsKey(instanceKey))
                return;

            double stamped = elapsed;
            if (IsValid(elapsed))
                _peakLiveElapsed = Math.Max(_peakLiveElapsed, elapsed);

            // Rise often zeros QUEST_TIMER on death before QuestEnd. Prefer the peak
            // live timer observed during the hunt over a collapsed stamp.
            if (_peakLiveElapsed > 5
                && (!IsValid(stamped) || stamped < _peakLiveElapsed - 5))
                stamped = _peakLiveElapsed;

            if (!IsValid(stamped))
                return;

            _completionsByInstance.Add(instanceKey, new Completion(species, stamped));
            RecomputeConfirmedElapsed();
        }
    }

    public int CompletedCount(long generation, string species)
    {
        lock (_sync)
        {
            if (generation != _generation)
                return 0;

            return _completionsByInstance.Values.Count(c =>
                string.Equals(c.Species, species, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Reset() => BeginQuest(expectedTargetCount: 0);

    /// <summary>
    /// Area remounts can look like a kill (old pointer dies) while the same species is still
    /// fighting under a new address. If we already latched a full species completion and a
    /// living instance is still on the map, drop those stamps so 用时 can keep advancing.
    /// </summary>
    public void RetractSpeciesCompletionIfStillAlive(
        long generation,
        string species,
        bool hasAliveInstance)
    {
        if (!hasAliveInstance || string.IsNullOrWhiteSpace(species))
            return;

        lock (_sync)
        {
            if (generation != _generation)
                return;

            int required = _targetSpecies.Count(s =>
                string.Equals(s, species, StringComparison.OrdinalIgnoreCase));
            if (required <= 0)
                return;

            int completed = _completionsByInstance.Values.Count(c =>
                string.Equals(c.Species, species, StringComparison.OrdinalIgnoreCase));
            if (completed < required)
                return;

            string[] orphanKeys = _completionsByInstance
                .Where(kv => string.Equals(kv.Value.Species, species, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToArray();
            foreach (string key in orphanKeys)
                _completionsByInstance.Remove(key);

            RecomputeConfirmedElapsed();
        }
    }

    public double ResolveResultElapsed(bool succeeded, double fallbackElapsed)
    {
        bool hasValidFallback = IsValid(fallbackElapsed);
        double safeFallback = hasValidFallback ? fallbackElapsed : 0;
        double? completion;
        lock (_sync)
            completion = _confirmedElapsed;
        if (!succeeded || completion is not { } confirmed)
            return safeFallback;

        if (!hasValidFallback)
        {
            lock (_sync)
            {
                if (LooksCollapsed(confirmed) && _peakLiveElapsed > 5)
                    return _peakLiveElapsed;
            }
            return confirmed;
        }

        // A collapsed objective stamp must not beat a healthier end/live clock.
        if (LooksCollapsed(confirmed))
        {
            double peak;
            lock (_sync)
                peak = _peakLiveElapsed;
            double best = Math.Max(safeFallback, peak);
            return best > confirmed ? best : confirmed;
        }

        // Prefer objective completion when it is at/before the end state time.
        // False early stamps from remounts must be retracted while the target is still alive;
        // do not second-guess a healthy confirmed stamp here (carve delay can be minutes).
        if (confirmed <= fallbackElapsed + 1)
            return confirmed;

        // Confirmed is later than end state: only trust a healthy end timer. A collapsed
        // Rise QUEST_TIMER (near zero while confirmed is minutes) must not win.
        if (fallbackElapsed <= 5 || fallbackElapsed < confirmed * 0.5)
            return confirmed;

        return safeFallback;
    }

    private void RecomputeConfirmedElapsed()
    {
        _confirmedElapsed = null;
        int requiredCount = Math.Max(_expectedTargetCount, _targetSpecies.Count);
        if (_targetSpecies.Count == 0 || _targetSpecies.Count < requiredCount)
            return;

        double latestRequiredCompletion = 0;
        foreach (IGrouping<string, string> targetGroup in _targetSpecies.GroupBy(
                     s => s,
                     StringComparer.OrdinalIgnoreCase))
        {
            double[] matching = _completionsByInstance.Values
                .Where(c => string.Equals(c.Species, targetGroup.Key, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Elapsed)
                .OrderBy(t => t)
                .ToArray();
            int requiredForSpecies = targetGroup.Count();
            if (matching.Length < requiredForSpecies)
                return;

            latestRequiredCompletion = Math.Max(
                latestRequiredCompletion,
                matching[requiredForSpecies - 1]);
        }

        _confirmedElapsed = latestRequiredCompletion > 0
            ? latestRequiredCompletion
            : null;
    }

    private static bool LooksCollapsed(double elapsed)
        => IsValid(elapsed) && elapsed <= 5;

    private static bool IsValid(double elapsed)
        => double.IsFinite(elapsed)
           && elapsed > 0
           && elapsed <= MaxReasonableQuestDurationSeconds;

    private readonly record struct Completion(string Species, double Elapsed);
}

