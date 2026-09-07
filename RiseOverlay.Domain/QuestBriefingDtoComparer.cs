namespace RiseOverlay.Domain;

public static class QuestBriefingDtoComparer
{
    public static bool Equivalent(QuestBriefingDto? left, QuestBriefingDto? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Targets.Count != right.Targets.Count)
            return false;

        for (int i = 0; i < left.Targets.Count; i++)
        {
            QuestBriefingTargetDto x = left.Targets[i];
            QuestBriefingTargetDto y = right.Targets[i];
            if (!string.Equals(x.Name, y.Name, StringComparison.Ordinal)
                || x.IsCapturable != y.IsCapturable
                || x.HasSeverableTail != y.HasSeverableTail
                || !string.Equals(x.FocusPartLabel, y.FocusPartLabel, StringComparison.Ordinal)
                || x.Kind != y.Kind
                || x.IsDefeated != y.IsDefeated
                || x.CaptureState != y.CaptureState
                || !x.OverallElementsOrdered.SequenceEqual(y.OverallElementsOrdered)
                || !x.Recommended.SequenceEqual(y.Recommended))
                return false;
        }

        return true;
    }
}
