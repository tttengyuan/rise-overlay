namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public static class MHRDamagePollingRules
{
    public static IReadOnlyList<nint> SelectTargets(
        IReadOnlyList<nint> focused,
        nint? sticky,
        IReadOnlySet<nint> present)
    {
        nint[] active = focused
            .Where(present.Contains)
            .Distinct()
            .ToArray();
        if (active.Length > 0)
            return active;

        return sticky is { } address && present.Contains(address)
            ? [address]
            : [];
    }
}
