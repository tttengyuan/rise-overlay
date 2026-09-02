namespace RiseOverlay.Domain;

/// <summary>
/// Rise often leaves 1 HP in memory after a slay until despawn — normalize for HUD numbers.
/// </summary>
public static class MonsterHealthDisplay
{
    /// <summary>Fraction of max HP below which we treat remaining health as zero (≈1 HP on large monsters).</summary>
    private const double GhostHpRatio = 0.00002;

    public static double ForHud(double health, double maxHealth)
    {
        if (health <= 0 || maxHealth <= 0)
            return Math.Max(0, health);

        if (health / maxHealth <= GhostHpRatio)
            return 0;

        return health;
    }
}
