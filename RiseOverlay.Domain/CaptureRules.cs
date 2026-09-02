namespace RiseOverlay.Domain;

public enum CaptureDisplayState
{
    Capturable,
    SpeciesUncapturable,
    /// <summary>Slay/special quest on a capturable species — no badge; capture UI stays hidden.</summary>
    QuestRestricted,
    /// <summary>Afflicted (MonsterType.Qurio) — badge shows「怪异化」only.</summary>
    Anomaly,
}

public static class CaptureRules
{
    public static bool IsPastThreshold(double healthPercent, double thresholdPercent)
        => healthPercent <= thresholdPercent;

    public static bool ShowCaptureUi(bool speciesCapturable, bool questAllowsCapture)
        => speciesCapturable && questAllowsCapture;

    /// <summary>
    /// Limp/weaken line on the HP bar — independent of whether the quest allows capture.
    /// </summary>
    public static double? ResolveWeakenThresholdPercent(
        bool speciesCapturable,
        bool isAnomaly,
        double? liveThresholdPercent,
        double? staticThresholdPercent,
        double defaultPercent)
    {
        if (!speciesCapturable || isAnomaly)
            return null;

        return liveThresholdPercent is > 0
            ? liveThresholdPercent
            : staticThresholdPercent ?? defaultPercent;
    }

    public static CaptureDisplayState ResolveDisplayState(
        bool speciesCapturable,
        bool questAllowsCapture,
        bool isAnomaly)
    {
        if (isAnomaly)
            return CaptureDisplayState.Anomaly;
        if (!speciesCapturable)
            return CaptureDisplayState.SpeciesUncapturable;
        return questAllowsCapture
            ? CaptureDisplayState.Capturable
            : CaptureDisplayState.QuestRestricted;
    }
}
