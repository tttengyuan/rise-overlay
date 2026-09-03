namespace RiseOverlay.Domain;

/// <summary>
/// Rise sometimes leaves sub-1 float residue in memory after a slay — normalize for HUD.
/// Do not collapse real 1–few HP left on a living monster (that wrongly forced 0% mid-fight).
/// </summary>
public static class MonsterHealthDisplay
{
    public static double ForHud(double health, double maxHealth)
    {
        if (health <= 0 || maxHealth <= 0)
            return Math.Max(0, health);

        // Only fractional dust (&lt; 1 HP). Exact 1+ can be a living finisher.
        if (health < 1.0)
            return 0;

        return health;
    }
}
