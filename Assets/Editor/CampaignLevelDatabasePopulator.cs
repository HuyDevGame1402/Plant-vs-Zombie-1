#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the 50 Adventure level entries using PvZ-Portable's level tables.</summary>
public static class CampaignLevelDatabasePopulator
{
    // gZombieWaves from src/Lawn/Challenge.h, levels 1 through 50.
    private static readonly int[] BaseWaves =
    {
        4, 6, 8, 10, 8, 10, 20, 10, 20, 20,
        10, 20, 10, 20, 10, 10, 20, 10, 20, 20,
        10, 20, 20, 30, 20, 20, 30, 20, 30, 30,
        10, 20, 10, 20, 20, 10, 20, 10, 20, 20,
        10, 20, 20, 30, 20, 20, 30, 20, 30, 30
    };

    // Bit N corresponds to global Adventure level N+1.
    // These masks are gZombieAllowedLevels intersected with startingLevel and non-zero pickWeight.
    // Types with zero pickWeight (Flag, BackupDancer, DuckyTube, Imp) are inserted by special rules.
    private static readonly string[] AllowedLevelMasks =
    {
        "3FFFFFFFFFFFF", "0", "3FFFFFFFFFBFC", "20010806360", "312D034A04B80",
        "A04C00", "D3000", "80081298000", "E0000", "0",
        "0", "25C00000", "36000000", "36000000", "238000000",
        "30090C0000000", "C300000000", "9800000000", "8E000000000", "0",
        "3430000000000", "35C0000000000", "3600000000000", "3800000000000", "0"
    };

    [MenuItem("PvZ/Campaign/Populate Selected Campaign Level Database", true)]
    private static bool CanPopulate() => Selection.activeObject is CampaignLevelDatabase;

    [MenuItem("PvZ/Campaign/Populate Selected Campaign Level Database")]
    private static void Populate()
    {
        var database = Selection.activeObject as CampaignLevelDatabase;
        if (database == null)
        {
            EditorUtility.DisplayDialog("Campaign Levels", "Select a CampaignLevelDatabase asset first.", "OK");
            return;
        }

        if (BaseWaves.Length != 50 || AllowedLevelMasks.Length != Enum.GetValues(typeof(ZombieType)).Length)
        {
            EditorUtility.DisplayDialog("Campaign Levels", "Expected 50 levels and matching ZombieType entries. No changes were made.", "OK");
            return;
        }

        Undo.RecordObject(database, "Populate Campaign Level Database");
        var serializedDatabase = new SerializedObject(database);
        serializedDatabase.Update();
        SerializedProperty levels = serializedDatabase.FindProperty("levels");
        if (levels == null || !levels.isArray)
        {
            Debug.LogError("CampaignLevelDatabase.levels serialized list was not found.", database);
            return;
        }

        levels.arraySize = 50;
        for (int globalLevel = 1; globalLevel <= 50; globalLevel++)
        {
            int world = (globalLevel - 1) / 10 + 1;
            int localLevel = (globalLevel - 1) % 10 + 1;
            SerializedProperty level = levels.GetArrayElementAtIndex(globalLevel - 1);
            SetInt(level, "world", world);
            SetInt(level, "level", localLevel);
            SetInt(level, "baseWaves", BaseWaves[globalLevel - 1]);

            SerializedProperty allowed = level.FindPropertyRelative("allowedZombies");
            if (allowed == null || !allowed.isArray)
                throw new InvalidOperationException("CampaignLevelData.allowedZombies list was not found.");

            allowed.ClearArray();
            for (int zombieIndex = 0; zombieIndex < AllowedLevelMasks.Length; zombieIndex++)
            {
                ulong mask = Convert.ToUInt64(AllowedLevelMasks[zombieIndex], 16);
                if ((mask & (1UL << (globalLevel - 1))) == 0)
                    continue;

                int item = allowed.arraySize;
                allowed.InsertArrayElementAtIndex(item);
                allowed.GetArrayElementAtIndex(item).enumValueIndex = zombieIndex;
            }
        }

        serializedDatabase.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Campaign Levels", "Created 50 Adventure level entries from the PvZ-Portable tables.", "OK");
    }

    private static void SetInt(SerializedProperty parent, string propertyName, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing serialized field: {propertyName}");
        property.intValue = value;
    }
}
#endif
