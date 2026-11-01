using System;
using UnityEngine;

[Serializable]
public class ZombieDefinition
{
    [Header("Zombie Identity")]
    public ZombieType zombieType;

    [Header("Campaign Settings")]
    [Min(0)] public int zombieValue;
    [Min(0)] public int pickWeight;
    [Min(1)] public int startingLevel;
    [Min(1)] public int firstAllowedWave = 1;
}
