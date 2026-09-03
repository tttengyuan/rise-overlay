namespace RiseOverlay.Domain;

/// <summary>
/// Unique key for HUD part rows. Afflicted hunts often list the same localized name twice
/// (breakable 「可破」 + Qurio 「怪异化」). Syncing by display name alone throws on
/// <c>ToDictionary</c> and freezes all subsequent part updates.
/// </summary>
public static class PartRowSyncKey
{
    public static string For(string? name, bool isQurio, bool isQurioThreshold)
    {
        char kind = isQurioThreshold ? 'T' : isQurio ? 'Q' : 'N';
        return $"{name ?? string.Empty}\u001f{kind}";
    }
}
