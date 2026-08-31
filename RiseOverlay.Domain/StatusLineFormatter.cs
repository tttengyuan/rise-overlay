namespace RiseOverlay.Domain;

public static class StatusLineFormatter
{
    public static string Format(StatusLineModel s)
    {
        var parts = new List<string>();
        if (s.EnrageRemaining is { } e)
            parts.Add($"愤怒 {e.Minutes}:{e.Seconds:D2}");
        if (s.StunActive && s.StunActiveRemaining is { } sa)
            parts.Add($"晕眩中 {sa.Minutes}:{sa.Seconds:D2}");
        else if (s.StunBuildupPercent is { } sp)
            parts.Add($"晕眩 {Math.Round(sp):0}%");
        if (s.StaminaPercent is { } st)
            parts.Add($"耐力 {Math.Round(st):0}%");
        if (s.DownRemaining is { } d)
            parts.Add($"倒地 {d.Minutes}:{d.Seconds:D2}");
        return string.Join(" · ", parts);
    }
}
