namespace RiseOverlay.UI.Themes;

public static class RiseThemeIds
{
    public const string Classic = "Classic";
    public const string Glass = "Glass";
    public const string Parchment = "Parchment";
    public const string Soft = "Soft";
    public const string OledCoral = "OledCoral";
    public const string GlassCoral = "GlassCoral";
    public const string Transparent = "Transparent";

    // ---- 视觉特效风格包（源自 2026-09-28 HUD 特效设计 12 版稿，剔除「能量核心」） ----
    public const string NeonCyber = "NeonCyber";
    public const string Hologram = "Hologram";
    public const string Molten = "Molten";
    public const string MinimalLine = "MinimalLine";
    public const string PixelRetro = "PixelRetro";
    public const string SegmentGauge = "SegmentGauge";
    public const string LiquidFill = "LiquidFill";
    public const string DarkSharp = "DarkSharp";
    public const string CrimsonWa = "CrimsonWa";
    public const string Frost = "Frost";

    public static readonly string[] All =
    [
        Classic,
        Glass,
        Parchment,
        Soft,
        OledCoral,
        GlassCoral,
        Transparent,
        NeonCyber,
        Hologram,
        Molten,
        MinimalLine,
        PixelRetro,
        SegmentGauge,
        LiquidFill,
        DarkSharp,
        CrimsonWa,
        Frost,
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
        Transparent => "透明",
        NeonCyber => "霓虹赛博",
        Hologram => "全息投影",
        Molten => "熔岩熔金",
        MinimalLine => "极简线条",
        PixelRetro => "像素复古",
        SegmentGauge => "分段仪表",
        LiquidFill => "液态填充",
        DarkSharp => "暗黑锐利",
        CrimsonWa => "朱红和风",
        Frost => "冰霜",
        _ => "现版金青",
    };

    public static string PackUri(string themeId) =>
        $"pack://application:,,,/RiseOverlay.UI;component/Themes/Theme.{Normalize(themeId)}.xaml";
}
