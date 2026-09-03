namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Utils;

/// <summary>
/// Converts Rise <c>EnemyDef.EmTypes</c> enum values to overlay static ids (<c>monster_XXX_YY</c>).
/// Encoding: low byte = species, next byte = variant (see MH Rise modding Monster-IDs wiki).
/// </summary>
public static class RiseEmTypes
{
    /// <summary>Small-monster EmTypes use the 0x1000 bit (EmsType*).</summary>
    private const int SmallMonsterBit = 0x1000;

    /// <summary>
    /// Large-monster species bytes that exist in Rise/Sunbreak (from EmType wiki).
    /// Rejects filler like 0x16 → fake <c>monster_022_00</c>.
    /// </summary>
    private static readonly HashSet<int> KnownLargeSpecies =
    [
        1, 2, 3, 4, 7,
        19, 20, 23, 24, 25, 27,
        32, 37, 42, 44, 47, 54, 57, 58, 59, 60, 61, 62,
        71, 72, 77, 81, 82, 86, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100,
        102, 107, 108, 109, 118, 124,
        131, 132, 133, 134, 135, 136,
    ];

    public static bool IsLargeMonsterEmType(int emType)
    {
        if (emType <= 0)
            return false;
        if ((emType & SmallMonsterBit) != 0)
            return false;

        int species = emType & 0xFF;
        return KnownLargeSpecies.Contains(species);
    }

    public static string? ToMonsterStaticId(int emType)
    {
        if (!IsLargeMonsterEmType(emType))
            return null;

        int species = emType & 0xFF;
        int variant = (emType >> 8) & 0xFF;
        return $"monster_{species:D3}_{variant:D2}";
    }
}
