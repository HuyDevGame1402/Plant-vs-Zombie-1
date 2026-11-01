using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PlantReanimation.Core
{
    public static class PlantReanimLoader
    {
        public static void LoadAtlas(TextAsset atlasJson, PlantReanimData data)
        {
            if (atlasJson == null) throw new ArgumentNullException(nameof(atlasJson));
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.atlasParts.Clear(); data.atlasPartList.Clear();
            JObject root = JObject.Parse(atlasJson.text);
            JArray resources = root["resources"] as JArray;
            if (resources == null) throw new FormatException("Atlas JSON không có resources[]");
            foreach (JToken item in resources)
            {
                JObject resource = item as JObject;
                JObject attrs = resource == null ? null : resource["attributes"] as JObject;
                string id = attrs == null ? null : attrs.Value<string>("id");
                if (string.IsNullOrWhiteSpace(id) && resource != null) id = resource.Value<string>("id") ?? resource.Value<string>("name");
                id = NormalizeId(id);
                JObject rect = resource == null ? null : resource["atlasRect"] as JObject;
                if (rect == null && attrs != null) rect = attrs["atlasRect"] as JObject;
                PlantAtlasPart part = null;
                if (rect != null)
                {
                    float w = Number(rect["width"]), h = Number(rect["height"]);
                    if (w > 0f && h > 0f) part = new PlantAtlasPart { id = id, x = Number(rect["x"]), y = Number(rect["y"]), width = w, height = h };
                }
                data.atlasPartList.Add(part); // giữ nguyên resource index của atlas
                if (part != null && !string.IsNullOrEmpty(id)) data.atlasParts[id] = part;
            }
            if (data.atlasParts.Count == 0) throw new FormatException("Không tìm thấy atlasRect hợp lệ trong Atlas JSON.");
        }

        public static void LoadReanim(TextAsset reanimJson, PlantReanimData data)
        {
            if (reanimJson == null) throw new ArgumentNullException(nameof(reanimJson));
            if (data == null) throw new ArgumentNullException(nameof(data));
            JObject root = JObject.Parse(reanimJson.text);
            data.fps = Number(root["fps"]); if (data.fps <= 0f) data.fps = 12f;
            JArray tracks = root["tracks"] as JArray;
            if (tracks == null) throw new FormatException("Reanim JSON không có tracks[].");
            data.tracks.Clear();
            int backleafAtlasIndex = -1, backleafReanimIndex = int.MinValue;
            for (int ti = 0; ti < tracks.Count; ti++)
            {
                JObject source = tracks[ti] as JObject; if (source == null) continue;
                PlantReanimTrack track = new PlantReanimTrack { name = source.Value<string>("name") };
                JArray transforms = source["transforms"] as JArray;
                if (transforms != null)
                {
                    for (int fi = 0; fi < transforms.Count; fi++)
                    {
                        JObject f = transforms[fi] as JObject;
                        track.transforms.Add(f == null ? null : ParseFrame(f));
                        if (backleafReanimIndex == int.MinValue && string.Equals(track.name, "backleaf", StringComparison.OrdinalIgnoreCase) && f != null && f["i"] != null)
                        {
                            backleafReanimIndex = (int)f["i"];
                            for (int ai = 0; ai < data.atlasPartList.Count; ai++)
                                if (data.atlasPartList[ai] != null && string.Equals(data.atlasPartList[ai].id, "IMAGE_REANIM_PEASHOOTER_BACKLEAF", StringComparison.OrdinalIgnoreCase)) { backleafAtlasIndex = ai; break; }
                        }
                    }
                }
                data.tracks.Add(track);
            }
            data.atlasIndexOffset = backleafAtlasIndex >= 0 && backleafReanimIndex != int.MinValue ? backleafReanimIndex - backleafAtlasIndex : 0;
            data.BuildTrackLookup();
            if (data.tracks.Count == 0 || data.FrameCount == 0) throw new FormatException("Reanim JSON không chứa frame/track hợp lệ.");
        }

        private static PlantReanimFrame ParseFrame(JObject f)
        {
            return new PlantReanimFrame { x = NullableFloat(f["x"]), y = NullableFloat(f["y"]), kx = NullableFloat(f["kx"]), ky = NullableFloat(f["ky"]), sx = NullableFloat(f["sx"]), sy = NullableFloat(f["sy"]), f = NullableInt(f["f"]), i = NullableInt(f["i"]) };
        }
        private static string NormalizeId(string id) { if (string.IsNullOrWhiteSpace(id)) return null; int n = id.IndexOf('|'); return (n >= 0 ? id.Substring(0, n) : id).Trim().ToUpperInvariant(); }
        private static float Number(JToken t) { float n; return t != null && float.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? n : 0f; }
        private static float? NullableFloat(JToken t) { if (t == null || t.Type == JTokenType.Null) return null; float n; return float.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? (float?)n : null; }
        private static int? NullableInt(JToken t) { if (t == null || t.Type == JTokenType.Null) return null; int n; return int.TryParse(t.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? (int?)n : null; }
    }
}
