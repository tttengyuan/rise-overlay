namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public static class MHRQuestScanRules
{
    /// <summary>
    /// Quest data pointers contain unrelated values while Rise reports no quest type.
    /// Probing them causes false IDs, expensive target scans, and diagnostic dump spam.
    /// </summary>
    public static bool ShouldReadQuestData(bool hasQuestType) => hasQuestType;

    public static bool ShouldWriteProbeDump(bool diagnosticsEnabled)
        => diagnosticsEnabled;

    public static bool ShouldLogTargetResolution(string? previous, string current)
        => !string.Equals(previous, current, StringComparison.Ordinal);
}
