namespace RiseOverlay.UI.Update;

/// <summary>GitHub Releases endpoints for Rise Overlay self-update.</summary>
public static class RiseUpdateEndpoints
{
    /// <summary>owner/name — change if the repo moves.</summary>
    public const string Repository = "tttengyuan/rise-overlay";

    public static string LatestReleaseApi =>
        $"https://api.github.com/repos/{Repository}/releases/latest";

    public static string ReleasesPage =>
        $"https://github.com/{Repository}/releases";

    public const string ZipAssetPrefix = "RiseOverlay-";
}
