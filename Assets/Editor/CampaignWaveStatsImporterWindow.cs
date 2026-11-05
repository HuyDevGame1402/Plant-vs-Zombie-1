#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CampaignWaveStatsImporterWindow : EditorWindow
{
    private CampaignLevelDatabase targetDatabase;
    private TextAsset statsJson;

    [MenuItem("PvZ/Campaign/Import Zombie Wave Stats JSON")]
    private static void Open()
    {
        GetWindow<CampaignWaveStatsImporterWindow>("Import Wave Stats");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Import PvZ-Portable campaign wave data", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Imports per-level wave counts, flag waves, initial delays, candidate types, intro zombie IDs, and campaign timer values.",
            MessageType.Info);
        targetDatabase = (CampaignLevelDatabase)EditorGUILayout.ObjectField(
            "Campaign Level Database", targetDatabase, typeof(CampaignLevelDatabase), false);
        statsJson = (TextAsset)EditorGUILayout.ObjectField("zombie-wave-stats.json", statsJson, typeof(TextAsset), false);

        using (new EditorGUI.DisabledScope(targetDatabase == null || statsJson == null))
        {
            if (GUILayout.Button("Import into selected database"))
                Import();
        }
    }

    private void Import()
    {
        WaveStatsRoot root;
        try
        {
            root = JsonUtility.FromJson<WaveStatsRoot>(statsJson.text);
        }
        catch (Exception exception)
        {
            EditorUtility.DisplayDialog("Import failed", exception.Message, "OK");
            return;
        }

        if (root == null || root.levels == null || root.levels.Length != 50 || root.schedule == null)
        {
            EditorUtility.DisplayDialog("Import failed", "The JSON must contain 50 levels and a schedule section.", "OK");
            return;
        }

        var importedLevels = new List<CampaignLevelData>(50);
        foreach (WaveStatsLevel source in root.levels)
        {
            if (source == null || source.firstAdventurePlay == null || source.afterAdventureCompleted == null)
            {
                EditorUtility.DisplayDialog("Import failed", "A level is missing first-play or replay settings.", "OK");
                return;
            }

            var data = new CampaignLevelData
            {
                world = source.area,
                level = source.levelInArea,
                baseWaves = source.baseWaves,
                background = source.background,
                firstPlayWaveCount = source.firstAdventurePlay.waveCount,
                firstPlayInitialDelayTicks = source.firstAdventurePlay.firstWaveDelayTicks,
                firstPlayFlagWaveNumbers = Copy(source.firstAdventurePlay.flagWaveNumbers),
                replayWaveCount = source.afterAdventureCompleted.waveCount,
                replayInitialDelayTicks = source.afterAdventureCompleted.firstWaveDelayTicks,
                replayFlagWaveNumbers = Copy(source.afterAdventureCompleted.flagWaveNumbers),
                guaranteedIntroZombieTypeIds = Copy(source.guaranteedIntroZombieTypes),
                yetiMayBeIncludedConditionally = source.yetiMayBeIncludedConditionally
            };

            if (source.zombieCandidates != null)
            {
                foreach (WaveStatsZombie candidate in source.zombieCandidates)
                {
                    if (TryMapZombieType(candidate.type, out ZombieType type) && !data.allowedZombies.Contains(type))
                        data.allowedZombies.Add(type);
                    else if (!TryMapZombieType(candidate.type, out _))
                        Debug.LogWarning($"Unsupported campaign zombie type in JSON: {candidate.type}");
                }
            }

            importedLevels.Add(data);
        }

        CampaignTimingData timing = BuildTiming(root.schedule);
        Undo.RecordObject(targetDatabase, "Import PvZ-Portable wave stats");
        targetDatabase.ReplaceImportedData(importedLevels, timing);
        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Import complete", "Imported wave settings for all 50 Adventure levels.", "OK");
    }

    private static CampaignTimingData BuildTiming(WaveStatsSchedule schedule)
    {
        var result = new CampaignTimingData();
        if (schedule.initialDelay != null)
        {
            result.standardInitialDelayTicks = schedule.initialDelay.standardAdventureTicks;
            result.level2FirstTimeInitialDelayTicks = schedule.initialDelay.level2FirstTimeTicks;
            result.miniBossInitialDelayTicks = schedule.initialDelay.miniBossLevelTicks;
        }
        if (schedule.regularBetweenWaveDelay != null && schedule.regularBetweenWaveDelay.ticks != null)
        {
            result.regularBetweenWaveMinTicks = schedule.regularBetweenWaveDelay.ticks.min;
            result.regularBetweenWaveMaxTicks = schedule.regularBetweenWaveDelay.ticks.max;
            // These health-acceleration values are documented in the JSON description and Board.cpp logic.
            result.healthAccelerationMinimumElapsedTicks = 400;
            result.healthAccelerationRemainingTicks = 200;
            result.healthThresholdMinimum = 0.50f;
            result.healthThresholdMaximum = 0.65f;
        }
        if (schedule.flagWaveBetweenDelay != null)
        {
            result.flagWaveBetweenDelayTicks = schedule.flagWaveBetweenDelay.counterTicks;
            result.hugeWaveWarningTicks = schedule.flagWaveBetweenDelay.hugeWaveWarningTicks;
        }
        return result;
    }

    private static List<T> Copy<T>(T[] source)
        => source == null ? new List<T>() : new List<T>(source);

    private static bool TryMapZombieType(string sourceType, out ZombieType type)
    {
        switch (sourceType)
        {
            case "NORMAL": type = ZombieType.Normal; return true;
            case "FLAG": type = ZombieType.Flag; return true;
            case "TRAFFIC_CONE": type = ZombieType.Conehead; return true;
            case "POLEVAULTER": type = ZombieType.PoleVault; return true;
            case "PAIL": type = ZombieType.Buckethead; return true;
            case "NEWSPAPER": type = ZombieType.Newspaper; return true;
            case "DOOR": type = ZombieType.ScreenDoor; return true;
            case "FOOTBALL": type = ZombieType.Football; return true;
            case "DANCER": type = ZombieType.Dancing; return true;
            case "BACKUP_DANCER": type = ZombieType.BackupDancer; return true;
            case "DUCKY_TUBE": type = ZombieType.DuckyTube; return true;
            case "SNORKEL": type = ZombieType.Snorkel; return true;
            case "ZAMBONI": type = ZombieType.Zomboni; return true;
            case "BOBSLED": type = ZombieType.BobsledTeam; return true;
            case "DOLPHIN_RIDER": type = ZombieType.DolphinRider; return true;
            case "JACK_IN_THE_BOX": type = ZombieType.JackInTheBox; return true;
            case "BALLOON": type = ZombieType.Balloon; return true;
            case "DIGGER": type = ZombieType.Digger; return true;
            case "POGO": type = ZombieType.Pogo; return true;
            case "YETI": type = ZombieType.Yeti; return true;
            case "BUNGEE": type = ZombieType.Bungee; return true;
            case "LADDER": type = ZombieType.Ladder; return true;
            case "CATAPULT": type = ZombieType.Catapult; return true;
            case "GARGANTUAR": type = ZombieType.Gargantuar; return true;
            case "IMP": type = ZombieType.Imp; return true;
            default: type = default; return false;
        }
    }

    [Serializable] private class WaveStatsRoot { public WaveStatsSchedule schedule; public WaveStatsLevel[] levels; }
    [Serializable]
    private class WaveStatsSchedule
    {
        public WaveStatsInitialDelay initialDelay;
        public WaveStatsRegularDelay regularBetweenWaveDelay;
        public WaveStatsFlagDelay flagWaveBetweenDelay;
    }
    [Serializable]
    private class WaveStatsInitialDelay
    {
        public int standardAdventureTicks;
        public int level2FirstTimeTicks;
        public int miniBossLevelTicks;
    }
    [Serializable] private class WaveStatsRegularDelay { public WaveStatsTickRange ticks; }
    [Serializable] private class WaveStatsTickRange { public int min; public int max; }
    [Serializable] private class WaveStatsFlagDelay { public int counterTicks; public int hugeWaveWarningTicks; }
    [Serializable]
    private class WaveStatsLevel
    {
        public int area;
        public int levelInArea;
        public int baseWaves;
        public string background;
        public WaveStatsPlaySettings firstAdventurePlay;
        public WaveStatsPlaySettings afterAdventureCompleted;
        public WaveStatsZombie[] zombieCandidates;
        public string[] guaranteedIntroZombieTypes;
        public string yetiMayBeIncludedConditionally;
    }
    [Serializable]
    private class WaveStatsPlaySettings
    {
        public int waveCount;
        public int[] flagWaveNumbers;
        public int firstWaveDelayTicks;
    }
    [Serializable] private class WaveStatsZombie { public string type; }
}
#endif
