namespace RiseOverlay.Domain;

public readonly record struct OverlayScreenBounds(
    double Left,
    double Top,
    double Right,
    double Bottom);

public static class OverlayPositionRules
{
    public static (double X, double Y) ResolveNormalizedPosition(
        OverlayScreenBounds bounds,
        double widgetWidth,
        double widgetHeight,
        double scale,
        double horizontal,
        double vertical,
        double margin)
    {
        double width = Math.Max(1, bounds.Right - bounds.Left);
        double height = Math.Max(1, bounds.Bottom - bounds.Top);
        double safeScale = double.IsFinite(scale) ? Math.Clamp(scale, 0.5, 2) : 1;
        double safeHorizontal = double.IsFinite(horizontal) ? Math.Clamp(horizontal, 0, 1) : 1;
        double safeVertical = double.IsFinite(vertical) ? Math.Clamp(vertical, 0, 1) : 0.18;
        double safeMargin = double.IsFinite(margin) ? Math.Max(0, margin) : 18;
        double scaledWidth = Math.Max(1, widgetWidth * safeScale);
        double scaledHeight = Math.Max(1, widgetHeight * safeScale);
        double availableX = Math.Max(0, width - scaledWidth - safeMargin * 2);
        double maximumY = Math.Max(bounds.Top + safeMargin, bounds.Bottom - scaledHeight - safeMargin);

        return (
            bounds.Left + safeMargin + availableX * safeHorizontal,
            Math.Clamp(bounds.Top + height * safeVertical, bounds.Top + safeMargin, maximumY));
    }

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
