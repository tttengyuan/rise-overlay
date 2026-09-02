namespace RiseOverlay.Domain;

/// <summary>
/// Rise often leaves ~1 HP in memory after a slay until despawn — normalize for HUD numbers.
/// </summary>
public static class MonsterHealthDisplay
{
    /// <summary>Fraction of max HP below which we treat remaining health as zero.</summary>
    private const double GhostHpRatio = 0.00005;

    public static double ForHud(double health, double maxHealth)
    {
        if (health <= 0 || maxHealth <= 0)
            return Math.Max(0, health);

        // Absolute ghost residual (1/34500 etc. exceeds a tight ratio on mid-size monsters).
        if (health <= 1.0 && maxHealth >= 500)
            return 0;

        if (health / maxHealth <= GhostHpRatio)
            return 0;

        return health;
    }
}
