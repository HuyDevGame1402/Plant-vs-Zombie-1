using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CampaignLevelDatabase",
    menuName = "PvZ/Campaign/Level Database"
)]
public class CampaignLevelDatabase : ScriptableObject
{
    [SerializeField]
    private List<CampaignLevelData> levels = new();

    public IReadOnlyList<CampaignLevelData> Levels => levels;

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
}