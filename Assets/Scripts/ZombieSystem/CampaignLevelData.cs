using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CampaignLevelData
{
    [Min(1)] public int world;
    [Range(1, 10)] public int level;
    [Min(0)] public int baseWaves;
    public string background;
    public List<ZombieType> allowedZombies = new();

    [Header("First Adventure play")]
    [Min(0)] public int firstPlayWaveCount;
    [Min(0)] public int firstPlayInitialDelayTicks;
    public List<int> firstPlayFlagWaveNumbers = new();

    [Header("After Adventure completion")]
    [Min(0)] public int replayWaveCount;
    [Min(0)] public int replayInitialDelayTicks;
    public List<int> replayFlagWaveNumbers = new();

    [Header("Special spawns")]
    public List<string> guaranteedIntroZombieTypeIds = new();
    [TextArea] public string yetiMayBeIncludedConditionally;

    // PvZ-Portable uses campaign-wide level numbers from 1 to 50.
    public int GlobalLevel => (world - 1) * 10 + level;
}
