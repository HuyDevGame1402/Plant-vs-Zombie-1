using System;
using System.Collections.Generic;

[Serializable]
public class CampaignWavePlan
{
    public int waveNumber;
    public bool isFlagWave;
    public List<ZombieType> zombies = new();
}

/// <summary>
/// Port của Board::PickZombieWaves / PickZombieType / PutInMissingZombies (PvZ-Portable).
/// Cùng seed + cùng input => cùng roster.
/// </summary>
public static class CampaignWaveGenerator
{
    private const int MaxZombiesPerWave = 100;

    public static List<CampaignWavePlan> Generate(
        CampaignLevelData levelData,
        ZombieDatabase zombieDatabase,
        int seed,
        bool hasFinishedAdventure = false,
        bool canSpawnYetis = false,
        bool yetiAlreadySeen = false)
    {
        if (levelData == null)
            throw new ArgumentNullException(nameof(levelData));
        if (zombieDatabase == null)
            throw new ArgumentNullException(nameof(zombieDatabase));
        if (levelData.baseWaves <= 0)
            throw new ArgumentException("Level must have at least one base wave.", nameof(levelData));

        int g = levelData.GlobalLevel;
        if (g < 1 || g > 50)
            throw new ArgumentOutOfRangeException(nameof(levelData), "Global campaign level must be from 1 to 50.");

        int waveCount = ResolveWaveCount(levelData, hasFinishedAdventure, g, out List<int> importedFlags);

        var random = new Random(seed);
        List<ZombieType> allowed = BuildAllowedList(levelData, zombieDatabase, g, canSpawnYetis);
        bool hasIntro = TryFindIntroducedZombie(zombieDatabase, g, canSpawnYetis, out ZombieType intro);

        var waves = new List<CampaignWavePlan>(waveCount);

        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            bool isFlag = IsFlagWave(waveIndex, waveCount, hasFinishedAdventure, g, importedFlags);
            bool isFinal = waveIndex == waveCount - 1;

            var wave = new CampaignWavePlan { waveNumber = waveIndex + 1, isFlagWave = isFlag };
            var counts = new Dictionary<ZombieType, int>();
            int points = 0;

            // Bungee Blitz (5-5): flag wave chỉ có 5 Bungee.
            if (CampaignRules.IsBungeeBlitzLevel(g) && isFlag)
            {
                for (int i = 0; i < 5; i++)
                    AddZombie(wave, ZombieType.Bungee, zombieDatabase, counts, ref points);
                if (!isFinal)
                {
                    waves.Add(wave);
                    continue;
                }
            }

            // Điểm cơ bản của wave
            points = (hasFinishedAdventure && g != 5)
                ? waveIndex * 2 / 5 + 1
                : waveIndex / 3 + 1;

            if (isFlag)
            {
                int plain = Math.Min(points, 8);
                points = (int)(points * 2.5f);

                for (int i = 0; i < plain; i++)
                    AddZombie(wave, ZombieType.Normal, zombieDatabase, counts, ref points);
                AddZombie(wave, ZombieType.Flag, zombieDatabase, counts, ref points);
            }

            // Hệ số nhân điểm theo loại level
            if (CampaignRules.IsLittleTroubleLevel(g) || CampaignRules.IsWallnutBowlingLevel(g))
                points *= 4;
            else if (CampaignRules.IsMiniBossLevel(g))
                points *= 3;
            else if (CampaignRules.IsStormyNightLevel(g))
                points *= 3;
            else if (CampaignRules.IsBungeeBlitzLevel(g))
                points *= 2;

            // Zombie mới được giới thiệu ở level này có wave cố định
            if (hasIntro && intro != ZombieType.DuckyTube)
            {
                bool spawnIntro;
                if (intro == ZombieType.Digger || intro == ZombieType.Balloon)
                    spawnIntro = waveIndex + 1 == 7 || isFinal;
                else if (intro == ZombieType.Yeti)
                    spawnIntro = waveIndex == waveCount / 2 && !yetiAlreadySeen;
                else
                    spawnIntro = waveIndex == waveCount / 2 || isFinal;

                if (spawnIntro)
                    AddZombie(wave, intro, zombieDatabase, counts, ref points);
            }

            // Level 5-10 (global 50): wave cuối thêm 1 Gargantuar
            if (g == 50 && isFinal)
                AddZombie(wave, ZombieType.Gargantuar, zombieDatabase, counts, ref points);

            // Wave cuối: mọi loại zombie hợp lệ của level phải xuất hiện (theo thứ tự enum, trừ Yeti)
            if (isFinal)
            {
                foreach (ZombieType type in Enum.GetValues(typeof(ZombieType)))
                {
                    if (type == ZombieType.Yeti) continue;
                    if (!allowed.Contains(type)) continue;
                    if (counts.TryGetValue(type, out int c) && c > 0) continue;
                    AddZombie(wave, type, zombieDatabase, counts, ref points);
                }
            }

            // Tiêu hết điểm còn lại bằng random có trọng số
            while (points > 0 && wave.zombies.Count < MaxZombiesPerWave)
            {
                if (!TryPickZombie(allowed, zombieDatabase, waveIndex + 1, points, random, out ZombieType picked))
                    break;
                AddZombie(wave, picked, zombieDatabase, counts, ref points);
            }

            waves.Add(wave);
        }

        return waves;
    }

    // ---------------------------------------------------------------- wave count / flag

    private static int ResolveWaveCount(
        CampaignLevelData d, bool finished, int g, out List<int> importedFlags)
    {
        importedFlags = null;

        if (CampaignRules.IsWhackAZombieLevel(g))
            return 8;

        if (finished && d.replayWaveCount > 0)
        {
            importedFlags = d.replayFlagWaveNumbers;
            return d.replayWaveCount;
        }

        if (!finished && d.firstPlayWaveCount > 0)
        {
            importedFlags = d.firstPlayFlagWaveNumbers;
            return d.firstPlayWaveCount;
        }

        int count = d.baseWaves;
        if (finished && !CampaignRules.IsMiniBossLevel(g))
            count = count < 10 ? 20 : count + 10;
        return count;
    }

    private static bool IsFlagWave(
        int waveIndex, int waveCount, bool finished, int g, List<int> importedFlags)
    {
        if (importedFlags != null && importedFlags.Count > 0)
            return importedFlags.Contains(waveIndex + 1);

        bool firstTime = !finished;
        if (firstTime && g == 1)
            return false;

        int wavesPerFlag = firstTime && waveCount < 10 ? waveCount : 10;
        return wavesPerFlag > 0 && waveIndex % wavesPerFlag == wavesPerFlag - 1;
    }

    // ---------------------------------------------------------------- allowed / intro

    /// <summary>Board::CanZombieSpawnOnLevel — danh sách theo thứ tự enum.</summary>
    private static List<ZombieType> BuildAllowedList(
        CampaignLevelData levelData, ZombieDatabase db, int g, bool canSpawnYetis)
    {
        var set = new SortedSet<int>();

        if (levelData.allowedZombies != null)
        {
            foreach (ZombieType type in levelData.allowedZombies)
            {
                if (type == ZombieType.Yeti) continue; // Yeti chỉ phụ thuộc canSpawnYetis
                if (!db.TryGetZombie(type, out ZombieDefinition def)) continue;
                if (g < def.startingLevel || def.pickWeight <= 0) continue;
                set.Add((int)type);
            }
        }

        if (canSpawnYetis && db.TryGetZombie(ZombieType.Yeti, out ZombieDefinition yeti) && yeti.pickWeight > 0)
            set.Add((int)ZombieType.Yeti);

        var list = new List<ZombieType>(set.Count);
        foreach (int v in set)
            list.Add((ZombieType)v);
        return list;
    }

    /// <summary>Board::GetIntroducedZombieType</summary>
    private static bool TryFindIntroducedZombie(
        ZombieDatabase db, int g, bool canSpawnYetis, out ZombieType result)
    {
        result = ZombieType.Normal;
        if (g == 1)
            return false;

        foreach (ZombieType type in Enum.GetValues(typeof(ZombieType)))
        {
            if (type == ZombieType.Yeti && !canSpawnYetis) continue;
            if (!db.TryGetZombie(type, out ZombieDefinition def)) continue;
            if (def.startingLevel != g) continue;

            result = type;
            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- picking

    private static bool TryPickZombie(
        List<ZombieType> allowed,
        ZombieDatabase db,
        int waveNumber,
        int points,
        Random random,
        out ZombieType picked)
    {
        var candidates = new List<ZombieDefinition>();
        int totalWeight = 0;

        foreach (ZombieType type in allowed)
        {
            if (!db.TryGetZombie(type, out ZombieDefinition def)) continue;
            if (def.pickWeight <= 0) continue;
            if (waveNumber < def.firstAllowedWave || points < def.zombieValue) continue;

            candidates.Add(def);
            totalWeight += def.pickWeight;
        }

        if (totalWeight <= 0)
        {
            picked = ZombieType.Normal;
            return false;
        }

        int roll = random.Next(totalWeight);
        foreach (ZombieDefinition def in candidates)
        {
            roll -= def.pickWeight;
            if (roll < 0)
            {
                picked = def.zombieType;
                return true;
            }
        }

        picked = ZombieType.Normal;
        return false;
    }

    private static void AddZombie(
        CampaignWavePlan wave,
        ZombieType type,
        ZombieDatabase db,
        Dictionary<ZombieType, int> counts,
        ref int remainingPoints)
    {
        if (!db.TryGetZombie(type, out ZombieDefinition def))
            return;

        wave.zombies.Add(type);
        counts[type] = counts.TryGetValue(type, out int c) ? c + 1 : 1;
        remainingPoints -= def.zombieValue;
    }
}