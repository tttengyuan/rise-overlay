using RiseOverlay.Domain;

namespace RiseOverlay.Data;

/// <summary>
/// Converts Data-layer static DTOs into Domain snapshots for <see cref="MonsterHudMapper"/>.
/// </summary>
public static class MonsterStaticAdapter
{
    public static StaticMonsterSnapshot ToSnapshot(MonsterStaticDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new StaticMonsterSnapshot(
            Id: dto.Id,
            Title: dto.Title,
            Capturable: dto.Capturable,
            Hitzones: (dto.Hitzones ?? Array.Empty<MonsterHitzoneDto>())
                .Select(h => new StaticHitzoneRow(
                    PartNameSanitizer.Clean(h.Part),
                    h.Phase,
                    h.Fire,
                    h.Water,
                    h.Ice,
                    h.Thunder,
                    h.Dragon))
                .ToArray(),
            Parts: (dto.Parts ?? Array.Empty<MonsterPartStaticDto>())
                .Select(p => new StaticPartRow(PartNameSanitizer.Clean(p.Part), p.Break, p.Sever))
                .ToArray());
    }
}
