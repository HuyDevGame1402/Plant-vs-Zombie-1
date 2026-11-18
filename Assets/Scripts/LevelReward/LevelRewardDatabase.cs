using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelRewardEntry
{
    public int level;
    public LevelReward reward;
}

[CreateAssetMenu(
    fileName = "LevelRewardDatabase",
    menuName = "PvZ/Level Reward Database"
)]
public class LevelRewardDatabase : ScriptableObject
{
    [SerializeField]
    private List<LevelRewardEntry> rewards = new();

    public LevelReward GetReward(int level)
    {
        LevelRewardEntry entry = rewards.Find(x => x.level == level);

        if (entry == null)
        {
            Debug.LogWarning($"Không tìm thấy reward cho level {level}");
            return null;
        }

        return entry.reward;
    }
}