namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Utils;

/// <summary>
/// Converts Rise <c>EnemyDef.EmTypes</c> enum values to overlay static ids (<c>monster_XXX_YY</c>).
/// Encoding: low byte = species, next byte = variant (see MH Rise modding Monster-IDs wiki).
/// </summary>
public static class RiseEmTypes
{
    /// <summary>Small-monster EmTypes use the 0x1000 bit (EmsType*).</summary>
    private const int SmallMonsterBit = 0x1000;

    public static bool IsLargeMonsterEmType(int emType)
    {
        if (emType <= 0)
            return false;
        if ((emType & SmallMonsterBit) != 0)
            return false;

        int species = emType & 0xFF;
        // Rise large monsters roughly Em001–Em136; keep headroom for patches.
        return species is >= 1 and <= 200;
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
