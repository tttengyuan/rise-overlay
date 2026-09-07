namespace RiseOverlay.Domain;

public static class TargetSelectionRules
{
    /// <summary>
    /// Resolves scanner target races without depending on collection insertion order.
    /// A fresh lock-on event wins; otherwise keep a still-valid current target. Fall back
    /// only when exactly one candidate is valid.
    /// </summary>
    public static T? Select<T>(
        IReadOnlyCollection<T> candidates,
        T? current,
        T? promoted,
        Func<T, bool> isValid)
        where T : class
    {
        if (promoted is not null && isValid(promoted))
            return promoted;

        if (current is not null && isValid(current))
            return current;

        T? single = null;
        foreach (T candidate in candidates)
        {
            if (!isValid(candidate))
                continue;
            if (single is not null)
                return null;
            single = candidate;
        }

        return single;
    }
}
