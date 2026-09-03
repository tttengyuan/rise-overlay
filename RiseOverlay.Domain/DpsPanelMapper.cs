namespace RiseOverlay.Domain;

/// <summary>
/// Pure party → <see cref="DpsPanelDto"/> mapping for the compact DPS panel.
/// DPS values are computed with HunterPie's configured original strategy and passed through here.
/// </summary>
public static class DpsPanelMapper
{
    public const int MaxDisplayedEntries = 4;

    /// <summary>
    /// Builds a panel DTO sorted by total damage (desc), capped at <see cref="MaxDisplayedEntries"/>.
    /// </summary>
    public static DpsPanelDto FromSnapshots(
        IEnumerable<DpsMemberSnapshot> members,
        double? huntDurationSeconds = null,
        long? questTotalDamage = null,
        long? lockedTargetDamage = null,
        string? lockedTargetName = null)
    {
        ArgumentNullException.ThrowIfNull(members);

        var entries = members
            .Select(ToEntry)
            .OrderByDescending(e => e.TotalDamage)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .Take(MaxDisplayedEntries)
            .ToArray();

        long? questTotal = questTotalDamage;
        if (questTotal is null)
        {
            long sum = entries.Sum(e => e.TotalDamage);
            questTotal = sum > 0 ? sum : null;
        }

        return new DpsPanelDto(
            entries,
            huntDurationSeconds is > 0 ? huntDurationSeconds : null,
            QuestTotalDamage: questTotal,
            LockedTargetDamage: lockedTargetDamage is >= 0 ? lockedTargetDamage : null,
            LockedTargetName: string.IsNullOrWhiteSpace(lockedTargetName) ? null : lockedTargetName);
    }

    private static DpsEntryDto ToEntry(DpsMemberSnapshot member)
        => new(
            Name: string.IsNullOrWhiteSpace(member.Name) ? "?" : member.Name,
            IsSelf: member.IsSelf,
            Dps: Math.Max(0, member.Dps),
            TotalDamage: Math.Max(0, member.TotalDamage));
}

/// <summary>Lightweight party-member snapshot for pure mapping (no game deps).</summary>
public readonly record struct DpsMemberSnapshot(string Name, bool IsSelf, long TotalDamage, double Dps);
