using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CampaignLevelDatabase",
    menuName = "PvZ/Campaign/Level Database"
)]
public class CampaignLevelDatabase : ScriptableObject
{
    [SerializeField] private List<CampaignLevelData> levels = new();
    [SerializeField] private CampaignTimingData timing = new();

    public IReadOnlyList<CampaignLevelData> Levels => levels;
    public CampaignTimingData Timing => timing;

    public CampaignLevelData GetLevel(int world, int level)
    {
        foreach (CampaignLevelData data in levels)
        {
            if (data != null && data.world == world && data.level == level)
                return data;
        }

        Debug.LogError($"Campaign level not found: World {world}, Level {level}", this);
        return null;
    }


#if UNITY_EDITOR
    public void ReplaceImportedData(List<CampaignLevelData> importedLevels, CampaignTimingData importedTiming)
    {
        levels = importedLevels ?? new List<CampaignLevelData>();
        timing = importedTiming ?? new CampaignTimingData();
    }
#endif
}

[System.Serializable]
public class CampaignTimingData
{
    [Min(1)] public int ticksPerSecond = 100;
    [Min(0)] public int standardInitialDelayTicks = 1800;
    [Min(0)] public int level2FirstTimeInitialDelayTicks = 5000;
    [Min(0)] public int miniBossInitialDelayTicks = 100;

    [Min(0)] public int regularBetweenWaveMinTicks = 2500;
    [Min(0)] public int regularBetweenWaveMaxTicks = 3099;
    [Min(0)] public int flagWaveBetweenDelayTicks = 4500;
    [Min(0)] public int hugeWaveWarningTicks = 750;

    [Min(0)] public int healthAccelerationMinimumElapsedTicks = 400;
    [Min(0)] public int healthAccelerationRemainingTicks = 200;
    [Range(0f, 1f)] public float healthThresholdMinimum = 0.50f;
    [Range(0f, 1f)] public float healthThresholdMaximum = 0.65f;

    public float TicksToSeconds(int ticks)
        => ticksPerSecond <= 0 ? 0f : ticks / (float)ticksPerSecond;
}
