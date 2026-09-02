namespace RiseOverlay.Domain;

public static class StatusLineFormatter
{
    public static string Format(StatusLineModel s)
    {
        var parts = new List<string>();
        if (s.EnrageRemaining is { } e)
            parts.Add($"愤怒 {FormatClock(e)}");
        if (s.StunActive && s.StunActiveRemaining is { } sa)
            parts.Add($"晕眩中 {FormatStunCountdown(sa)}");
        else if (s.StunBuildupPercent is { } sp)
            parts.Add($"晕眩 {Math.Round(sp):0}%");
        if (s.StaminaPercent is { } st)
            parts.Add($"耐力 {Math.Round(st):0}%");
        return string.Join(" · ", parts);
    }

    /// <summary>Active stun: prefer one-decimal seconds so the countdown is easy to read.</summary>
    public static string FormatStunCountdown(TimeSpan remaining)
    {
        var sec = Math.Max(0, remaining.TotalSeconds);
        if (sec < 60)
            return $"{sec:0.0}s";
        return FormatClock(remaining);
    }

    private static string FormatClock(TimeSpan t)
        => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
}
