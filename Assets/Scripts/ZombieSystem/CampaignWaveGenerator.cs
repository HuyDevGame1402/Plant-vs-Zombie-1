using System;
using System.Collections.Generic;

[Serializable]
public class CampaignWavePlan
{
    public int waveNumber;
    public bool isFlagWave;
    public List<ZombieType> zombies = new();
}

/// <summary>Builds a deterministic Adventure-mode wave roster from the level and zombie databases.</summary>
public static class CampaignWaveGenerator
{
    private const int MaxZombiesPerWave = 100;

    /// <param name="seed">Same seed + same inputs produces the same wave rosters.</param>
    /// <param name="isFirstAdventurePlay">Uses first-play wave-count/flag rules when true.</param>
    /// <param name="hasFinishedAdventure">Applies the original replay wave-count increase when true.</param>
    /// <param name="isMiniBossLevel">Mini-boss levels do not get the replay wave-count increase.</param>
    public static List<CampaignWavePlan> Generate(
        CampaignLevelData levelData,
        ZombieDatabase zombieDatabase,
        int seed,
        bool isFirstAdventurePlay = true,
        bool hasFinishedAdventure = false,
        bool isMiniBossLevel = false)
    {
        if (levelData == null)
            throw new ArgumentNullException(nameof(levelData));
        if (zombieDatabase == null)
            throw new ArgumentNullException(nameof(zombieDatabase));
        if (levelData.baseWaves <= 0)
            throw new ArgumentException("Level must have at least one base wave.", nameof(levelData));

        int globalLevel = levelData.GlobalLevel;
        if (globalLevel < 1 || globalLevel > 50)
            throw new ArgumentOutOfRangeException(nameof(levelData), "Global campaign level must be from 1 to 50.");

        int waveCount;
        if (hasFinishedAdventure && levelData.replayWaveCount > 0)
        {
            waveCount = levelData.replayWaveCount;
        }
        else if (!hasFinishedAdventure && levelData.firstPlayWaveCount > 0)
        {
            waveCount = levelData.firstPlayWaveCount;
        }
        else
        {
            waveCount = levelData.baseWaves;
            if (hasFinishedAdventure && !isMiniBossLevel)
                waveCount = waveCount < 10 ? 20 : waveCount + 10;
        }

        var random = new Random(seed);
        var waves = new List<CampaignWavePlan>(waveCount);
        List<ZombieType> allowed = levelData.allowedZombies ?? new List<ZombieType>();

        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            bool isFlagWave = IsFlagWave(waveIndex, waveCount, levelData, isFirstAdventurePlay, hasFinishedAdventure, globalLevel);
            var wave = new CampaignWavePlan
            {
                waveNumber = waveIndex + 1,
                isFlagWave = isFlagWave
            };
            var waveCounts = new Dictionary<ZombieType, int>();
            int points = waveIndex / 3 + 1;

            if (isFlagWave)
            {
                int normalCount = Math.Min(points, 8);
                points = (int)(points * 2.5f);

                for (int i = 0; i < normalCount; i++)
                    AddZombie(wave, ZombieType.Normal, zombieDatabase, waveCounts, ref points);

                AddZombie(wave, ZombieType.Flag, zombieDatabase, waveCounts, ref points);
            }

            ZombieType introduced = FindIntroducedZombie(allowed, zombieDatabase, globalLevel);
            bool finalWave = waveIndex == waveCount - 1;
            if (introduced != ZombieType.Normal && introduced != ZombieType.DuckyTube)
            {
                bool spawnIntroduced = IsSpecialIntroType(introduced)
                    ? waveNumberIsSevenOrFinal(waveIndex, finalWave)
                    : waveIndex == waveCount / 2 || finalWave;

                if (spawnIntroduced)
                    AddZombie(wave, introduced, zombieDatabase, waveCounts, ref points);
            }

            // The original Adventure mode ensures every level-eligible random zombie appears by the final wave.
            if (finalWave)
            {
                foreach (ZombieType type in allowed)
                {
                    if (!waveCounts.ContainsKey(type))
                        AddZombie(wave, type, zombieDatabase, waveCounts, ref points);
                }
            }

            while (points > 0 && wave.zombies.Count < MaxZombiesPerWave)
            {
                if (!TryPickZombie(allowed, zombieDatabase, waveIndex + 1, points, random, out ZombieType picked))
                    break;

                AddZombie(wave, picked, zombieDatabase, waveCounts, ref points);
            }

            waves.Add(wave);
        }

        return waves;
    }

    private static bool IsFlagWave(
        int waveIndex,
        int waveCount,
        CampaignLevelData levelData,
        bool firstPlay,
        bool hasFinishedAdventure,
        int globalLevel)
    {
        bool hasImportedSchedule = hasFinishedAdventure
            ? levelData.replayWaveCount > 0
            : levelData.firstPlayWaveCount > 0;
        if (hasImportedSchedule)
        {
            List<int> importedFlagWaves = hasFinishedAdventure
                ? levelData.replayFlagWaveNumbers
                : levelData.firstPlayFlagWaveNumbers;
            return importedFlagWaves != null && importedFlagWaves.Contains(waveIndex + 1);
        }

        if (firstPlay && globalLevel == 1)
            return false;

        int wavesPerFlag = firstPlay && waveCount < 10 ? waveCount : 10;
        return wavesPerFlag > 0 && waveIndex % wavesPerFlag == wavesPerFlag - 1;
    }

    private static bool IsSpecialIntroType(ZombieType type)
        => type == ZombieType.Digger || type == ZombieType.Balloon;

    private static bool waveNumberIsSevenOrFinal(int waveIndex, bool finalWave)
        => waveIndex + 1 == 7 || finalWave;

    private static ZombieType FindIntroducedZombie(
        List<ZombieType> allowed,
        ZombieDatabase database,
        int globalLevel)
    {
        // Preserve enum order, as the source scans ZombieType in order.
        foreach (ZombieType type in Enum.GetValues(typeof(ZombieType)))
        {
            if (!allowed.Contains(type))
                continue;

            ZombieDefinition definition = database.GetZombie(type);
            if (definition != null && definition.startingLevel == globalLevel)
                return type;
        }

        return ZombieType.Normal;
    }

    private static bool TryPickZombie(
        List<ZombieType> allowed,
        ZombieDatabase database,
        int waveNumber,
        int points,
        Random random,
        out ZombieType picked)
    {
        var candidates = new List<ZombieType>();
        int totalWeight = 0;

        foreach (ZombieType type in allowed)
        {
            ZombieDefinition definition = database.GetZombie(type);
            if (definition == null || definition.pickWeight <= 0)
                continue;
            if (waveNumber < definition.firstAllowedWave || points < definition.zombieValue)
                continue;

            candidates.Add(type);
            totalWeight += definition.pickWeight;
        }

        if (totalWeight <= 0)
        {
            picked = ZombieType.Normal;
            return false;
        }

        int roll = random.Next(totalWeight);
        foreach (ZombieType type in candidates)
        {
            roll -= database.GetZombie(type).pickWeight;
            if (roll < 0)
            {
                picked = type;
                return true;
            }
        }

        picked = ZombieType.Normal;
        return false;
    }

    private static void AddZombie(
        CampaignWavePlan wave,
        ZombieType type,
        ZombieDatabase database,
        Dictionary<ZombieType, int> waveCounts,
        ref int remainingPoints)
    {
        ZombieDefinition definition = database.GetZombie(type);
        if (definition == null)
            return;

        wave.zombies.Add(type);
        waveCounts[type] = waveCounts.TryGetValue(type, out int waveCount) ? waveCount + 1 : 1;
        remainingPoints -= definition.zombieValue;
    }
}
