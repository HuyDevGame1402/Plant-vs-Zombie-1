#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using PvZ.Data;
using UnityEditor;
using UnityEngine;

namespace PvZ.Editor
{
    public sealed class PvzZombieJsonImporter : EditorWindow
    {
        private TextAsset source;
        private DefaultAsset destination;

        [MenuItem("Tools/PvZ/Import Zombie Stats JSON")]
        private static void Open() => GetWindow<PvzZombieJsonImporter>("PvZ Zombie Importer");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Import PvZ zombie stats", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Imports one ZombieStatsAsset per zombie. Requires com.unity.nuget.newtonsoft-json.", MessageType.Info);
            source = (TextAsset)EditorGUILayout.ObjectField("Stats JSON", source, typeof(TextAsset), false);
            destination = (DefaultAsset)EditorGUILayout.ObjectField("Output folder", destination, typeof(DefaultAsset), false);
            using (new EditorGUI.DisabledScope(source == null || destination == null))
            {
                if (GUILayout.Button("Create Zombie Assets")) Import();
            }
        }

        private void Import()
        {
            string folder = AssetDatabase.GetAssetPath(destination);
            if (!AssetDatabase.IsValidFolder(folder) || !folder.StartsWith("Assets", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Invalid folder", "Select a folder inside the Unity project's Assets folder.", "OK");
                return;
            }

            try
            {
                JObject root = JObject.Parse(source.text);
                JArray zombies = root["zombies"] as JArray;
                if (zombies == null || zombies.Count == 0)
                    throw new FormatException("JSON does not contain a non-empty zombies array.");

                var seenIds = new HashSet<int>();
                var assets = new List<ZombieStatsAsset>(zombies.Count);
                float chilledSpeed = ReadChilledSpeedMultiplier(zombies);

                foreach (JObject zombie in zombies)
                {
                    int id = Int(zombie["id"]);
                    if (!seenIds.Add(id)) throw new FormatException("Duplicate zombie ID: " + id);
                    assets.Add(BuildAsset(zombie, chilledSpeed));
                }

                int created = 0;
                foreach (ZombieStatsAsset asset in assets)
                {
                    string enumPart = string.IsNullOrWhiteSpace(asset.enumName) ? "Zombie" : asset.enumName;
                    string fileName = Sanitize(idString(asset.id) + "_" + enumPart);
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset");
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Import complete", $"Created {created} individual zombie assets in:\n{folder}", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Import failed", ex.Message, "OK");
            }
        }

        private static ZombieStatsAsset BuildAsset(JObject z, float chilledSpeed)
        {
            var asset = ScriptableObject.CreateInstance<ZombieStatsAsset>();
            asset.id = Int(z["id"]);
            asset.enumName = String(z["enum"]);
            asset.displayName = String(z["name"]);

            JObject health = z["health"] as JObject;
            asset.bodyHealth = Int(health?["base"]?["value"]);
            asset.armor = (health?["armor"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(a => new ZombieArmor
                {
                    slot = String(a["slot"]),
                    displayName = String(a["name"]),
                    health = Int(a["health"]?["value"]),
                    firstDamageStageHealthRatio = 2f / 3f,
                    secondDamageStageHealthRatio = 1f / 3f,
                    dropsAtZeroHealth = true
                }).ToArray();

            asset.healthModifiers = (health?["conditions"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(c => new ZombieHealthModifier
                {
                    condition = String(c["condition"]) + (string.IsNullOrEmpty(String(c["effect"])) ? "" : " — " + String(c["effect"])),
                    multiplier = 1f,
                    divisor = (String(c["effect"]) ?? "").Contains("integer-divided by 4") ? 4 : 1
                }).ToArray();

            asset.attacks = (z["damage"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(d =>
                {
                    string condition = String(d["condition"]);
                    return new ZombieAttack
                    {
                        target = String(d["target"]),
                        attackType = String(d["attack"]),
                        damage = Float(d["amount"]),
                        hasNumericDamage = IsNumber(d["amount"]),
                        damageUnit = String(d["unit"]),
                        intervalTicks = Int(d["intervalTicks"]),
                        hasInterval = IsNumber(d["intervalTicks"]),
                        chilledIntervalTicks = condition != null && condition.Contains("8-tick") ? 8 : 0,
                        condition = condition
                    };
                }).ToArray();

            JObject movement = z["movement"] as JObject;
            asset.movementStates = (movement?["speeds"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(s => BuildMovement(s))
                .ToArray();
            asset.chilledSpeedMultiplier = chilledSpeed;

            JArray thresholds = z["bodyPartThresholds"] as JArray ?? new JArray();
            asset.canLoseBodyParts = thresholds.OfType<JObject>().Any(t =>
            {
                string condition = String(t["condition"]);
                return (String(t["part"]) == "arm" || String(t["part"]) == "head") &&
                       !string.IsNullOrEmpty(condition) &&
                       condition.IndexOf("not applicable", StringComparison.OrdinalIgnoreCase) < 0;
            });
            asset.armDropHealthRatio = 2f / 3f;
            asset.headDropHealthRatio = 1f / 3f;
            return asset;
        }

        private static ZombieMovementState BuildMovement(JObject s)
        {
            JToken value = s["value"];
            var result = new ZombieMovementState { state = String(s["state"]), speedUnit = String(s["unit"]) };
            if (value is JObject range)
            {
                result.minSpeed = Float(range["min"]);
                result.maxSpeed = Float(range["max"]);
                result.hasSpeedRange = IsNumber(range["min"]) && IsNumber(range["max"]);
            }
            else if (IsNumber(value))
            {
                result.fixedSpeed = Float(value);
                result.hasFixedSpeed = true;
            }
            return result;
        }

        private static float ReadChilledSpeedMultiplier(JArray zombies)
        {
            foreach (JObject z in zombies.OfType<JObject>())
                foreach (JObject rule in (z["movement"]?["sharedRules"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    string condition = String(rule["condition"]);
                    string effect = String(rule["effect"]);
                    if (condition == null || effect == null || condition.IndexOf("chilled", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    const string marker = "multiply by ";
                    int index = effect.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (index < 0) continue;
                    string number = effect.Substring(index + marker.Length).Split(' ')[0];
                    if (float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float multiplier))
                        return multiplier;
                }
            return 1f;
        }

        private static bool IsNumber(JToken t) => t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float);
        private static int Int(JToken t) => IsNumber(t) ? (int)t : 0;
        private static float Float(JToken t) => IsNumber(t) ? (float)t : 0f;
        private static string String(JToken t) => t == null || t.Type == JTokenType.Null ? null : t.ToString();
        private static string idString(int id) => id.ToString("00", CultureInfo.InvariantCulture);
        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "Zombie" : value;
        }
    }
}
#endif
