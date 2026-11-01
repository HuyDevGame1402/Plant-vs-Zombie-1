using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CampaignLevelData
{
    [Min(1)] public int world; // 1–5
    [Range(1, 10)] public int level; // level trong world
    [Min(0)] public int baseWaves;
    public List<ZombieType> allowedZombies = new();

    // PvZ-Portable đánh số level toàn chiến dịch từ 1 đến 50.
    public int GlobalLevel => (world - 1) * 10 + level;
}