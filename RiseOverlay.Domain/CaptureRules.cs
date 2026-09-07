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
    /// <summary>
    /// Conservative fallback for a live species that could not be resolved in static data.
    /// Every Apex species in Rise is slay-only, regardless of quest/invader role.
    /// </summary>
    public static bool IsKnownUncapturableSpeciesName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string normalized = name.Trim().Normalize(System.Text.NormalizationForm.FormKC);
        return normalized.StartsWith("霸主", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("Apex ", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPastThreshold(double healthPercent, double thresholdPercent)
        => healthPercent <= thresholdPercent;

    public static bool ShowCaptureUi(bool speciesCapturable, bool questAllowsCapture)
        => speciesCapturable && questAllowsCapture;

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

    /// <summary>
    /// Rise anomaly investigations have one afflicted primary target (the first quest target);
    /// optional additional cover targets and invading monsters keep their species capture rules.
    /// Slay restrictions apply to quest targets only, never to invaders.
    /// </summary>
    public static IReadOnlyList<QuestBriefingTargetDto> ResolveBriefingTargetStates(
        IReadOnlyList<QuestBriefingTargetDto> targets,
        bool isAnomalyQuest,
        bool isSlayQuest)
    {
        bool anomalyAssigned = false;
        var resolved = new QuestBriefingTargetDto[targets.Count];

        for (int i = 0; i < targets.Count; i++)
        {
            QuestBriefingTargetDto target = targets[i];
            bool isQuestTarget = target.Kind == BriefingTargetKind.Quest;

            if (isAnomalyQuest && isQuestTarget && !anomalyAssigned)
            {
                anomalyAssigned = true;
                resolved[i] = target with
                {
                    IsCapturable = false,
                    CaptureState = CaptureDisplayState.Anomaly,
                };
                continue;
            }

            if (isSlayQuest && isQuestTarget)
            {
                resolved[i] = target with
                {
                    IsCapturable = false,
                    CaptureState = CaptureDisplayState.QuestRestricted,
                };
                continue;
            }

            resolved[i] = target with
            {
                CaptureState = target.IsCapturable
                    ? CaptureDisplayState.Capturable
                    : CaptureDisplayState.SpeciesUncapturable,
            };
        }

        return resolved;
    }
}
