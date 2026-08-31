namespace RiseOverlay.Domain;

public static class PartWeakness
{
    public static IReadOnlyList<ElementId> ForPart(IReadOnlyDictionary<ElementId, int> partElements)
        => ElementRecommend.FromHitzones(partElements);
}
