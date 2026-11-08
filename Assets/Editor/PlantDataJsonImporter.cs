using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool for importing the PvZ plant-stats.json format into PlantData assets.
/// Place this file in an Editor folder inside your Unity project.
/// </summary>
public class PlantDataJsonImporter : EditorWindow
{
    [SerializeField] private TextAsset jsonAsset;
    [SerializeField] private DefaultAsset outputFolder;

    [MenuItem("Tools/Plants/Import Plant Stats JSON")]
    private static void Open()
    {
        GetWindow<PlantDataJsonImporter>("Plant JSON Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Import plant data text file", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Kéo file .txt hoặc .json đã nằm trong Project vào ô Input TextAsset. Chọn thư mục trong Assets làm nơi chứa tất cả PlantData .asset được tạo.", MessageType.Info);

        jsonAsset = (TextAsset)EditorGUILayout.ObjectField("Input TXT / JSON", jsonAsset, typeof(TextAsset), false);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output folder (Assets)", outputFolder, typeof(DefaultAsset), false);
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!CanImport()))
        {
            if (GUILayout.Button("Create / Update PlantData Assets", GUILayout.Height(32))) Import();
        }
    }

    private bool CanImport()
    {
        if (jsonAsset == null || outputFolder == null) return false;
        string path = AssetDatabase.GetAssetPath(outputFolder);
        return AssetDatabase.IsValidFolder(path);
    }

    private void Import()
    {
        try
        {
            object root = MiniJson.Parse(jsonAsset.text);
            var rootMap = root as Dictionary<string, object>;
            var plants = rootMap != null ? GetList(rootMap, "plants") : null;
            if (plants == null) throw new Exception("JSON không có mảng 'plants'.");

            string folder = AssetDatabase.GetAssetPath(outputFolder);
            int created = 0, updated = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object item in plants)
            {
                var p = item as Dictionary<string, object>;
                if (p == null) continue;
                string id = GetString(p, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                string safeName = MakeSafeFileName(id);
                if (!seen.Add(safeName)) throw new Exception("ID bị trùng trong JSON: " + id);
                string assetPath = folder.TrimEnd('/') + "/" + safeName + ".asset";
                PlantData data = AssetDatabase.LoadAssetAtPath<PlantData>(assetPath);
                bool isNew = data == null;
                if (isNew) data = CreateInstance<PlantData>();
                Fill(data, p);
                if (isNew) { AssetDatabase.CreateAsset(data, assetPath); created++; }
                else { EditorUtility.SetDirty(data); updated++; }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Plant import complete", "Created: " + created + "\nUpdated: " + updated + "\nTotal: " + (created + updated), "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Plant import failed", e.Message, "OK");
        }
    }

    private static void Fill(PlantData d, Dictionary<string, object> p)
    {
        d.id = GetString(p, "id");
        d.displayName = GetString(p, "displayName"); // Not present in source; leave blank for localization/manual entry.
        d.seedEnum = GetString(p, "seedEnum");
        d.nameId = GetString(p, "nameId");
        d.reanimationType = GetString(p, "reanimationType");
        d.subclass = GetString(p, "subclass");
        d.packetIndex = GetInt(p, "packetIndex");
        d.seedCostSun = GetInt(p, "seedCostSun");
        d.seedPacketRefreshTicks = GetInt(p, "seedPacketRefreshTicks");
        d.launchRateTicks = GetInt(p, "launchRateTicks");
        d.launchRateMeaning = GetString(p, "launchRateMeaning");

        var health = GetMap(p, "health");
        d.initialHp = GetInt(health, "initialHp", 300);
        d.maxHpAtPlanting = GetInt(health, "maxHpAtPlanting", 300);
        var multiplier = GetMap(health, "conditionalModeMultiplier");
        d.hasConditionalHealthMultiplier = multiplier != null && HasNonNull(multiplier, "multiplier");
        d.conditionalHealthMode = GetString(multiplier, "mode");
        d.conditionalHealthMultiplier = d.hasConditionalHealthMultiplier ? GetFloat(multiplier, "multiplier") : 1f;

        var attack = GetMap(p, "attack");
        d.projectileType = GetString(attack, "projectileType");
        d.hasDamagePerHit = HasNonNull(attack, "damagePerHit");
        d.damagePerHit = d.hasDamagePerHit ? GetInt(attack, "damagePerHit") : 0;
        d.hasProjectilesPerSequence = HasNonNull(attack, "projectilesPerSequence");
        d.projectilesPerSequence = d.hasProjectilesPerSequence ? GetInt(attack, "projectilesPerSequence") : 0;
        d.hasAttackInterval = HasNonNull(attack, "nominalIntervalTicks");
        d.nominalIntervalTicks = d.hasAttackInterval ? GetInt(attack, "nominalIntervalTicks") : 0;
        d.intervalJitterTicks = GetInt(attack, "intervalJitterTicks");
        d.attackNotes = GetString(attack, "notes");

        var production = GetMap(p, "production");
        d.producesResource = production != null;
        d.productionType = GetString(production, "type");
        d.amountPerCycle = GetInt(production, "amountPerCycle");
        d.coinKind = GetString(production, "coinKind");
        d.productionInitialState = GetString(production, "initialState");

        // Intentionally not assigned: description, extraNotes, imageSelectGUI,
        // plantPrefab, and offsetCreatePlant.
    }

    private static Dictionary<string, object> GetMap(Dictionary<string, object> m, string key)
    { object v; return m != null && m.TryGetValue(key, out v) ? v as Dictionary<string, object> : null; }
    private static List<object> GetList(Dictionary<string, object> m, string key)
    { object v; return m != null && m.TryGetValue(key, out v) ? v as List<object> : null; }
    private static bool HasNonNull(Dictionary<string, object> m, string key)
    { object v; return m != null && m.TryGetValue(key, out v) && v != null; }
    private static string GetString(Dictionary<string, object> m, string key)
    { object v; return m != null && m.TryGetValue(key, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
    private static int GetInt(Dictionary<string, object> m, string key, int fallback = 0)
    { object v; return m != null && m.TryGetValue(key, out v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback; }
    private static float GetFloat(Dictionary<string, object> m, string key)
    { object v; return m != null && m.TryGetValue(key, out v) && v != null ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : 0f; }
    private static string MakeSafeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value;
    }

    /// <summary>Small JSON parser so the importer works without Json.NET packages.</summary>
    private sealed class MiniJson
    {
        private readonly string json;
        private int index;
        private MiniJson(string json) { this.json = json; }
        public static object Parse(string json) { return new MiniJson(json).ReadValue(); }
        private object ReadValue()
        {
            Skip();
            if (index >= json.Length) throw Error("Unexpected end of JSON");
            char c = json[index];
            if (c == '{') return ReadObject();
            if (c == '[') return ReadArray();
            if (c == '"') return ReadString();
            if (c == 't') { ReadLiteral("true"); return true; }
            if (c == 'f') { ReadLiteral("false"); return false; }
            if (c == 'n') { ReadLiteral("null"); return null; }
            return ReadNumber();
        }
        private Dictionary<string, object> ReadObject()
        {
            var result = new Dictionary<string, object>(); index++; Skip();
            if (Take('}')) return result;
            while (true)
            {
                Skip(); if (index >= json.Length || json[index] != '"') throw Error("Expected object key");
                string key = ReadString(); Skip(); Expect(':'); result[key] = ReadValue(); Skip();
                if (Take('}')) return result; Expect(',');
            }
        }
        private List<object> ReadArray()
        {
            var result = new List<object>(); index++; Skip();
            if (Take(']')) return result;
            while (true) { result.Add(ReadValue()); Skip(); if (Take(']')) return result; Expect(','); }
        }
        private string ReadString()
        {
            Expect('"'); var s = new System.Text.StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"') return s.ToString();
                if (c != '\\') { s.Append(c); continue; }
                if (index >= json.Length) throw Error("Bad string escape");
                c = json[index++];
                switch (c)
                {
                    case '"': s.Append('"'); break;
                    case '\\': s.Append('\\'); break;
                    case '/': s.Append('/'); break;
                    case 'b': s.Append('\b'); break;
                    case 'f': s.Append('\f'); break;
                    case 'n': s.Append('\n'); break;
                    case 'r': s.Append('\r'); break;
                    case 't': s.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw Error("Bad unicode escape");
                        s.Append((char)int.Parse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); index += 4; break;
                    default: throw Error("Unknown string escape");
                }
            }
            throw Error("Unterminated string");
        }
        private object ReadNumber()
        {
            int start = index;
            while (index < json.Length && "-+0123456789.eE".IndexOf(json[index]) >= 0) index++;
            if (start == index) throw Error("Expected value");
            string n = json.Substring(start, index - start);
            long whole; if (long.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out whole)) return whole;
            double real; if (double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out real)) return real;
            throw Error("Invalid number");
        }
        private void ReadLiteral(string literal)
        { if (index + literal.Length > json.Length || string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0) throw Error("Invalid literal"); index += literal.Length; }
        private void Skip() { while (index < json.Length && char.IsWhiteSpace(json[index])) index++; }
        private bool Take(char c) { if (index < json.Length && json[index] == c) { index++; return true; } return false; }
        private void Expect(char c) { Skip(); if (!Take(c)) throw Error("Expected '" + c + "'"); }
        private Exception Error(string message) { return new FormatException(message + " at character " + index); }
    }
}
