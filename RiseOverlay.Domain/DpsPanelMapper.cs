namespace RiseOverlay.Domain;

/// <summary>
/// Pure DPS math and party → <see cref="DpsPanelDto"/> mapping for the compact DPS panel.
/// </summary>
public static class DpsPanelMapper
{
    public const int MaxDisplayedEntries = 4;

    /// <summary>
    /// DPS = totalDamage / max(1, questElapsedSeconds).
    /// </summary>
    public static double CalculateDps(long totalDamage, double questElapsedSeconds)
        => totalDamage / Math.Max(1.0, questElapsedSeconds);

    public static DpsEntryDto ToEntry(
        string name,
        bool isSelf,
        long totalDamage,
        double questElapsedSeconds)
    {
        long damage = Math.Max(0, totalDamage);
        return new(
            Name: string.IsNullOrWhiteSpace(name) ? "?" : name,
            IsSelf: isSelf,
            Dps: CalculateDps(damage, questElapsedSeconds),
            TotalDamage: damage);
    }

    /// <summary>
    /// Builds a panel DTO sorted by total damage (desc), capped at <see cref="MaxDisplayedEntries"/>.
    /// </summary>
    public static DpsPanelDto FromSnapshots(
        IEnumerable<DpsMemberSnapshot> members,
        double questElapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(members);

        var entries = members
            .Select(m => ToEntry(m.Name, m.IsSelf, m.TotalDamage, questElapsedSeconds))
            .OrderByDescending(e => e.TotalDamage)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .Take(MaxDisplayedEntries)
            .ToArray();

        return new DpsPanelDto(entries);
    }
}

/// <summary>Lightweight party-member snapshot for pure mapping (no game deps).</summary>
public readonly record struct DpsMemberSnapshot(string Name, bool IsSelf, long TotalDamage);
