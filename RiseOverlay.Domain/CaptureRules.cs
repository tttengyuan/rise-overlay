namespace RiseOverlay.Domain;

public static class CaptureRules
{
    public static bool IsPastThreshold(double healthPercent, double thresholdPercent)
        => healthPercent <= thresholdPercent;

    public static bool ShowCaptureUi(bool speciesCapturable, bool questAllowsCapture)
        => speciesCapturable && questAllowsCapture;
}
