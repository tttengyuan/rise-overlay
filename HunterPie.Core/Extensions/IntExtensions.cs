namespace HunterPie.Core.Extensions;

public static class IntExtensions
{
    private const int PET_ID_START = 10;
    public static int ToPetId(this int self) => self + PET_ID_START;

    public static bool IsMHWQuestOver(this int self) => self == 3;
}