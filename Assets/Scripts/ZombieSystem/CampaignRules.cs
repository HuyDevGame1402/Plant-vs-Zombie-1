/// <summary>
/// Hằng số / luật dùng chung, port từ LawnApp.cpp, Board.cpp và Zombie.cpp (PvZ-Portable).
/// Mọi level đều dùng "global level" 1..50.
/// </summary>
public static class CampaignRules
{
    public const int MaxLanes = 6;

    public static bool IsWallnutBowlingLevel(int g) => g == 5;
    public static bool IsMiniBossLevel(int g) => g == 10 || g == 20 || g == 30;
    public static bool IsWhackAZombieLevel(int g) => g == 15;
    public static bool IsLittleTroubleLevel(int g) => g == 25;
    public static bool IsStormyNightLevel(int g) => g == 40;
    public static bool IsBungeeBlitzLevel(int g) => g == 45;
    public static bool IsFinalBossLevel(int g) => g == 50;

    /// <summary>Pool (3-x) và Fog (4-x) có 6 hàng và có hồ bơi.</summary>
    public static bool IsPoolStage(int g) => g >= 21 && g <= 40;

    public static int LaneCountForLevel(int g) => IsPoolStage(g) ? 6 : 5;

    /// <summary>Level mà game gốc không dùng wave thường (Challenge tự spawn).</summary>
    public static bool UsesSpecialSpawning(int g)
        => IsWhackAZombieLevel(g) || IsFinalBossLevel(g);

    public static bool IsPoolOnly(ZombieType t)
        => t == ZombieType.Snorkel || t == ZombieType.DolphinRider;

    /// <summary>Zombie::ZombieTypeCanGoInPool</summary>
    public static bool CanGoInPool(ZombieType t)
        => t == ZombieType.Normal
        || t == ZombieType.Flag
        || t == ZombieType.Conehead
        || t == ZombieType.Buckethead
        || t == ZombieType.Snorkel
        || t == ZombieType.DolphinRider;
}