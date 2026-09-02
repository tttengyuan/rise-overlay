namespace RiseOverlay.Domain;

public readonly record struct PartDisplayState(
    string Text,
    bool ShowActiveBar,
    bool IsComplete);

public static class PartDisplayRules
{
    public static PartDisplayState Resolve(bool isSeverable, bool isBroken, bool isQurio)
    {
        if (isQurio)
            return new("怪异核", ShowActiveBar: true, IsComplete: false);
        if (isBroken)
            return new(isSeverable ? "已断尾" : "已破坏", ShowActiveBar: false, IsComplete: true);
        return new(isSeverable ? "可断" : "可破", ShowActiveBar: true, IsComplete: false);
    }
}
