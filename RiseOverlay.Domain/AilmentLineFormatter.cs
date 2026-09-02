namespace RiseOverlay.Domain;

public static class AilmentLineFormatter
{
    public static string Format(IEnumerable<AilmentDto> ailments)
    {
        ArgumentNullException.ThrowIfNull(ailments);

        return string.Join(
            " ",
            ailments
                .Where(ailment => !string.Equals(ailment.Key, "stun", StringComparison.OrdinalIgnoreCase))
                .Where(ailment => ailment.IsActive || ailment.Percent > 0)
                .Select(ailment => ailment.IsActive
                    ? $"{ailment.DisplayName}生效"
                    : $"{ailment.DisplayName}{Math.Round(ailment.Percent):0}%"));
    }
}
