namespace RiseOverlay.Domain;

/// <summary>Capsule part-row fill/label rules (flinch primary; Qurio HP overlay).</summary>
public static class CapsulePartRules
{
    public const double HardFlinchThreshold = 0.85;
    public const int MaxElementChars = 2;

    public static double FillRatio(
        bool isQurio,
        double healthRatio,
        double flinchRatio,
        double maxFlinch)
    {
        if (isQurio)
            return Clamp01(healthRatio);
        if (maxFlinch > 0)
            return Clamp01(flinchRatio);
        return Clamp01(healthRatio);
    }

    public static int Percent(double fillRatio)
        => (int)Math.Round(Clamp01(fillRatio) * 100);

    public static bool ShowHardMark(bool isQurio, double maxFlinch, double flinchRatio)
        => !isQurio && maxFlinch > 0 && flinchRatio >= HardFlinchThreshold;

    public static string ElementChar(ElementId element) => element switch
    {
        ElementId.Water => "水",
        ElementId.Thunder => "雷",
        ElementId.Ice => "冰",
        ElementId.Dragon => "龙",
        _ => "火",
    };

    public static IReadOnlyList<ElementId> TakeVisibleElements(IReadOnlyList<ElementId> weak)
    {
        if (weak.Count == 0)
            return Array.Empty<ElementId>();
        if (weak.Count <= MaxElementChars)
            return weak;
        return weak.Take(MaxElementChars).ToArray();
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);
}
