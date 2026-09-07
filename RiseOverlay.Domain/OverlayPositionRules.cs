namespace RiseOverlay.Domain;

public readonly record struct OverlayScreenBounds(
    double Left,
    double Top,
    double Right,
    double Bottom);

public static class OverlayPositionRules
{
    /// <summary>
    /// A position is recoverable only when the widget's top-left leaves enough visible area
    /// on at least one real screen. This intentionally supports negative monitor coordinates.
    /// </summary>
    public static bool IsSufficientlyVisible(
        double x,
        double y,
        IReadOnlyList<OverlayScreenBounds> screens,
        double minimumVisible)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || minimumVisible < 0)
            return false;

        foreach (OverlayScreenBounds screen in screens)
        {
            double width = screen.Right - screen.Left;
            double height = screen.Bottom - screen.Top;
            if (width <= 0 || height <= 0)
                continue;

            double visibleWidth = Math.Min(minimumVisible, width);
            double visibleHeight = Math.Min(minimumVisible, height);
            if (x >= screen.Left
                && y >= screen.Top
                && x <= screen.Right - visibleWidth
                && y <= screen.Bottom - visibleHeight)
                return true;
        }

        return false;
    }
}
