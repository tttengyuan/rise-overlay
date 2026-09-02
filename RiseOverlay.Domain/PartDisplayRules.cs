namespace RiseOverlay.Domain;

public readonly record struct PartDisplayState(
    string Text,
    bool ShowActiveBar,
    bool IsComplete);

public static class PartDisplayRules
{
    public static PartDisplayState Resolve(
        bool isSeverable,
        bool isBroken,
        bool isQurio,
        bool isQurioThreshold = false)
    {
        if (isQurioThreshold)
            return new("啮生虫", ShowActiveBar: true, IsComplete: false);
        // Broken freezes the row. Active infection never pairs with IsBroken (controller).
        if (isBroken)
            return new(isSeverable ? "已断尾" : "已破坏", ShowActiveBar: false, IsComplete: true);
        if (isQurio)
            return new("怪异化", ShowActiveBar: true, IsComplete: false);
        return new(isSeverable ? "可断" : "可破", ShowActiveBar: true, IsComplete: false);
    }
}
