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
            return _generation;
        }
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
            || string.IsNullOrWhiteSpace(species)
            || !IsValid(elapsed))
            return;

        lock (_sync)
        {
            if (generation != _generation
                || _completionsByInstance.ContainsKey(instanceKey))
                return;

            _completionsByInstance.Add(instanceKey, new Completion(species, elapsed));
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
            return confirmed;

        return confirmed <= fallbackElapsed + 1
            ? confirmed
            : safeFallback;
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

    private static bool IsValid(double elapsed)
        => double.IsFinite(elapsed)
           && elapsed > 0
           && elapsed <= MaxReasonableQuestDurationSeconds;

    private readonly record struct Completion(string Species, double Elapsed);
}

