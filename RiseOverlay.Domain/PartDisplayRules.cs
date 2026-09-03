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

        // HunterPie-style: active Qurio overlays the part row (including already-broken parts).
        // After the core clears, IsBroken still shows 「已破坏」.
        if (isQurio)
            return new("怪异化", ShowActiveBar: true, IsComplete: false);

        if (isBroken)
            return new(isSeverable ? "已断尾" : "已破坏", ShowActiveBar: false, IsComplete: true);

        return new(isSeverable ? "可断" : "可破", ShowActiveBar: true, IsComplete: false);
    }
}
