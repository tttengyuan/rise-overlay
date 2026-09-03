namespace RiseOverlay.Domain;

/// <summary>
/// Part row HP label. Active Qurio overlays break state; after infection clears,
/// broken parts show flinch numbers (no 「完成」).
/// </summary>
public static class PartHealthText
{
    public static string Format(
        bool isBroken,
        double currentHp,
        double maxHp,
        double flinch,
        double maxFlinch,
        bool isQurio = false)
    {
        if (isQurio && maxHp > 0)
            return $"{Fmt(currentHp)}/{Fmt(maxHp)}";

        if (isBroken && maxFlinch > 0)
            return $"{Fmt(flinch)}/{Fmt(maxFlinch)}";

        return $"{Fmt(currentHp)}/{Fmt(maxHp)}";
    }

    private static string Fmt(double v)
        => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");
}
