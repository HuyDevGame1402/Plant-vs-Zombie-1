#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Populates the selected ZombieDatabase with the campaign values from PvZ-Portable.</summary>
public static class ZombieDatabasePopulator
{
    private struct Row
    {
        public ZombieType type;
        public int value;
        public int weight;
        public int startingLevel;
        public int firstAllowedWave;

        public Row(ZombieType type, int value, int weight, int startingLevel, int firstAllowedWave)
        {
            this.type = type;
            this.value = value;
            this.weight = weight;
            this.startingLevel = startingLevel;
            this.firstAllowedWave = firstAllowedWave;
        }
    }

    // Kept in sync with the first 25 campaign ZombieType entries in PvZ-Portable.
    private static readonly Row[] Rows =
    {
        new Row(ZombieType.Normal,        1, 4000,  1,  1),
        new Row(ZombieType.Flag,          1,    0,  1,  1),
        new Row(ZombieType.Conehead,      2, 4000,  3,  1),
        new Row(ZombieType.PoleVault,     2, 2000,  6,  5),
        new Row(ZombieType.Buckethead,    4, 3000,  8,  1),
        new Row(ZombieType.Newspaper,     2, 1000, 11,  1),
        new Row(ZombieType.ScreenDoor,    4, 3500, 13,  5),
        new Row(ZombieType.Football,      7, 2000, 16,  5),
        new Row(ZombieType.Dancing,       5, 1000, 18,  5),
        new Row(ZombieType.BackupDancer,  1,    0, 18,  1),
        new Row(ZombieType.DuckyTube,     1,    0, 21,  5),
        new Row(ZombieType.Snorkel,       3, 2000, 23, 10),
        new Row(ZombieType.Zomboni,       7, 2000, 26, 10),
        new Row(ZombieType.BobsledTeam,   3, 2000, 26, 10),
        new Row(ZombieType.DolphinRider,  3, 1500, 28, 10),
        new Row(ZombieType.JackInTheBox,  3, 1000, 31, 10),
        new Row(ZombieType.Balloon,       2, 2000, 33, 10),
        new Row(ZombieType.Digger,        4, 1000, 36, 10),
        new Row(ZombieType.Pogo,          4, 1000, 38, 10),
        new Row(ZombieType.Yeti,          4,    1, 40,  1),
        new Row(ZombieType.Bungee,        3, 1000, 41, 10),
        new Row(ZombieType.Ladder,        4, 1000, 43, 10),
        new Row(ZombieType.Catapult,      5, 1500, 46, 10),
        new Row(ZombieType.Gargantuar,   10, 1500, 48, 15),
        new Row(ZombieType.Imp,          10,    0, 48,  1),
    };

    [MenuItem("PvZ/Campaign/Populate Selected Zombie Database", true)]
    private static bool CanPopulateSelectedDatabase()
        => Selection.activeObject is ZombieDatabase;

    [MenuItem("PvZ/Campaign/Populate Selected Zombie Database")]
    private static void PopulateSelectedDatabase()
    {
        var database = Selection.activeObject as ZombieDatabase;
        if (database == null)
        {
            EditorUtility.DisplayDialog(
                "Zombie Database",
                "Select a ZombieDatabase asset in the Project window first.",
                "OK");
            return;
        }

        if (Rows.Length != Enum.GetValues(typeof(ZombieType)).Length)
        {
            EditorUtility.DisplayDialog(
                "Zombie Database",
                "The ZombieType enum and source data have different counts. No changes were made.",
                "OK");
            return;
        }

        Undo.RecordObject(database, "Populate Zombie Database");
        var serializedDatabase = new SerializedObject(database);
        serializedDatabase.Update();
        SerializedProperty zombies = serializedDatabase.FindProperty("zombies");
        if (zombies == null || !zombies.isArray)
        {
            Debug.LogError("ZombieDatabase.zombies serialized list was not found.", database);
            return;
        }

        zombies.arraySize = Rows.Length;
        for (int i = 0; i < Rows.Length; i++)
        {
            SerializedProperty element = zombies.GetArrayElementAtIndex(i);
            SetInt(element, "zombieType", (int)Rows[i].type);
            SetInt(element, "zombieValue", Rows[i].value);
            SetInt(element, "pickWeight", Rows[i].weight);
            SetInt(element, "startingLevel", Rows[i].startingLevel);
            SetInt(element, "firstAllowedWave", Rows[i].firstAllowedWave);
        }

        serializedDatabase.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "Zombie Database",
            $"Populated {Rows.Length} zombie definitions from the PvZ-Portable campaign table.",
            "OK");
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
