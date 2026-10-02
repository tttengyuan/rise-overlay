using System.Globalization;

namespace RiseOverlay.UI.Shell;

public static class HudDisplayRangeFormatter
{
    public static string Scale(double value)
        => $"{Math.Clamp(value, 0.5, 2.0).ToString("0.0", CultureInfo.InvariantCulture)}×";

    public static string Opacity(double value)
        => $"{Math.Round(Math.Clamp(value, 0.1, 1.0) * 100):0}%";

    public static string Position(double value)
        => $"{Math.Round(Math.Clamp(value, 0.0, 1.0) * 100):0}%";
}
