namespace RiseOverlay.UI.Themes;

public static class RiseThemeIds
{
    public const string Classic = "Classic";
    public const string Glass = "Glass";
    public const string Parchment = "Parchment";
    public const string Soft = "Soft";
    public const string OledCoral = "OledCoral";
    public const string GlassCoral = "GlassCoral";

    public static readonly string[] All =
    [
        Classic,
        Glass,
        Parchment,
        Soft,
        OledCoral,
        GlassCoral,
    ];

    public static string Normalize(string? themeId) =>
        All.Contains(themeId, StringComparer.Ordinal)
            ? themeId!
            : Classic;

    public static string DisplayName(string themeId) => themeId switch
    {
        Glass => "玻璃",
        Parchment => "羊皮纸",
        Soft => "日系浅色",
        OledCoral => "OLED 珊瑚",
        GlassCoral => "玻璃珊瑚",
        _ => "现版金青",
    };

    public static string PackUri(string themeId) =>
        $"pack://application:,,,/RiseOverlay.UI;component/Themes/Theme.{Normalize(themeId)}.xaml";
}
