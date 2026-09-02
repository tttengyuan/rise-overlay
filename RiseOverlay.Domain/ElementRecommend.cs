namespace RiseOverlay.Domain;

public static class ElementRecommend
{
    // UI display order (not enum ordinal): Fire, Water, Ice, Thunder, Dragon
    public static readonly IReadOnlyList<ElementId> DisplayOrder =
    [
        ElementId.Fire,
        ElementId.Water,
        ElementId.Ice,
        ElementId.Thunder,
        ElementId.Dragon,
    ];

    public static IReadOnlyList<ElementId> FromHitzones(IReadOnlyDictionary<ElementId, int> values)
    {
        var max = values.Values.DefaultIfEmpty(0).Max();
        if (max <= 0)
            return [];

        return DisplayOrder
            .Where(e => values.TryGetValue(e, out var v) && v == max)
            .ToArray();
    }
}
