using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PlantReanimation.Core
{
    public static class PlantReanimLoader
    {
        public static void LoadAtlas(TextAsset atlasJson, PlantReanimData data)
        {
            if (atlasJson == null)
                throw new ArgumentNullException(nameof(atlasJson));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.atlasParts.Clear();
            data.atlasPartList.Clear();

            JObject root = JObject.Parse(atlasJson.text);
            JArray resources = root["resources"] as JArray;

            if (resources == null)
                throw new FormatException("Atlas JSON không có resources[]");

            foreach (JToken item in resources)
            {
                JObject resource = item as JObject;
                JObject attributes =
                    resource == null ? null : resource["attributes"] as JObject;

                string id =
                    attributes == null ? null : attributes.Value<string>("id");

                if (string.IsNullOrWhiteSpace(id) && resource != null)
                {
                    id = resource.Value<string>("id") ??
                         resource.Value<string>("name");
                }

                id = NormalizeId(id);

                JObject rect =
                    resource == null ? null : resource["atlasRect"] as JObject;

                if (rect == null && attributes != null)
                    rect = attributes["atlasRect"] as JObject;

                PlantAtlasPart part = null;

                if (rect != null)
                {
                    float width = Number(rect["width"]);
                    float height = Number(rect["height"]);

                    if (width > 0f && height > 0f)
                    {
                        part = new PlantAtlasPart
                        {
                            id = id,
                            x = Number(rect["x"]),
                            y = Number(rect["y"]),
                            width = width,
                            height = height
                        };
                    }
                }

                // Giữ nguyên index resource kể cả resource không có atlasRect.
                data.atlasPartList.Add(part);

                if (part != null && !string.IsNullOrEmpty(id))
                    data.atlasParts[id] = part;
            }

            if (data.atlasParts.Count == 0)
            {
                throw new FormatException(
                    "Không tìm thấy atlasRect hợp lệ trong Atlas JSON.");
            }
        }

        public static void LoadReanim(TextAsset reanimJson, PlantReanimData data)
        {
            if (reanimJson == null)
                throw new ArgumentNullException(nameof(reanimJson));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            JObject root = JObject.Parse(reanimJson.text);

            data.fps = Number(root["fps"]);
            if (data.fps <= 0f)
                data.fps = 12f;

            JArray tracks = root["tracks"] as JArray;
            if (tracks == null)
                throw new FormatException("Reanim JSON không có tracks[].");

            data.tracks.Clear();

            HashSet<string> usedTrackNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (JToken token in tracks)
            {
                JObject source = token as JObject;
                if (source == null)
                    continue;

                //PlantReanimTrack track = new PlantReanimTrack
                //{
                //    name = source.Value<string>("name")
                //};

                string sourceTrackName = source.Value<string>("name");
                string uniqueTrackName = sourceTrackName;
                int duplicateIndex = 1;

                while (!usedTrackNames.Add(uniqueTrackName))
                {
                    uniqueTrackName =
                        sourceTrackName + "__duplicate_" + duplicateIndex;
                    duplicateIndex++;
                }

                PlantReanimTrack track = new PlantReanimTrack
                {
                    name = uniqueTrackName
                };

                JArray transforms = source["transforms"] as JArray;

                if (transforms != null)
                {
                    foreach (JToken frameToken in transforms)
                    {
                        JObject frame = frameToken as JObject;
                        track.transforms.Add(
                            frame == null ? null : ParseFrame(frame));
                    }
                }

                data.tracks.Add(track);
            }

            data.atlasIndexOffset = FindAtlasIndexOffset(data);
            data.BuildTrackLookup();

            if (data.tracks.Count == 0 || data.FrameCount == 0)
            {
                throw new FormatException(
                    "Reanim JSON không chứa frame/track hợp lệ.");
            }

            Debug.Log(
                "[PlantReanimLoader] Atlas index offset = " +
                data.atlasIndexOffset +
                " (" + reanimJson.name + ")");
        }

        private static int FindAtlasIndexOffset(PlantReanimData data)
        {
            Dictionary<int, int> offsetVotes =
                new Dictionary<int, int>();

            int matchedTracks = 0;

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null ||
                    string.IsNullOrWhiteSpace(track.name) ||
                    track.transforms == null)
                {
                    continue;
                }

                PlantAtlasPart matchingPart =
                    FindAtlasPartForTrack(data, track.name);

                if (matchingPart == null)
                    continue;

                int atlasIndex = data.atlasPartList.IndexOf(matchingPart);
                if (atlasIndex < 0)
                    continue;

                int reanimIndex = -1;

                // Mỗi track chỉ bỏ phiếu một lần, dùng image index đầu tiên có dữ liệu.
                foreach (PlantReanimFrame frame in track.transforms)
                {
                    if (frame != null && frame.i.HasValue)
                    {
                        reanimIndex = frame.i.Value;
                        break;
                    }
                }

                if (reanimIndex < 0)
                    continue;

                int offset = reanimIndex - atlasIndex;

                int votes;
                offsetVotes.TryGetValue(offset, out votes);
                offsetVotes[offset] = votes + 1;
                matchedTracks++;
            }

            if (offsetVotes.Count == 0)
            {
                Debug.LogWarning(
                    "[PlantReanimLoader] Không tìm được track khớp với atlas. " +
                    "Tạm dùng atlas index offset = 0.");
                return 0;
            }

            int bestOffset = 0;
            int bestVotes = -1;

            foreach (KeyValuePair<int, int> vote in offsetVotes)
            {
                if (vote.Value > bestVotes)
                {
                    bestOffset = vote.Key;
                    bestVotes = vote.Value;
                }
            }

            Debug.Log(
                "[PlantReanimLoader] Tính offset từ " +
                bestVotes + "/" + matchedTracks + " track khớp atlas.");

            return bestOffset;
        }

        private static PlantAtlasPart FindAtlasPartForTrack(
            PlantReanimData data,
            string trackName)
        {
            string normalizedTrack = trackName.Trim();

            // Cherrybomb: CherryBomb_leftstem
            // -> IMAGE_REANIM_CHERRYBOMB_LEFTSTEM
            string directId = NormalizeId("IMAGE_REANIM_" + normalizedTrack);

            PlantAtlasPart part;
            if (data.atlasParts.TryGetValue(directId, out part))
                return part;

            // Peashooter: track "backleaf" dùng tên ảnh có prefix PEASHOOTER.
            if (string.Equals(
                    normalizedTrack,
                    "backleaf",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (data.atlasParts.TryGetValue(
                        "IMAGE_REANIM_PEASHOOTER_BACKLEAF",
                        out part))
                {
                    return part;
                }
            }

            string wallNutAtlasId = null;

            switch (normalizedTrack.ToLowerInvariant())
            {
                case "anim_face":
                    wallNutAtlasId = "IMAGE_REANIM_WALLNUT_BODY";
                    break;

                case "anim_blink_twitch":
                    wallNutAtlasId = "IMAGE_REANIM_WALLNUT_TWITCH";
                    break;

                case "anim_blink_twice":
                case "anim_blink_thrice":
                    wallNutAtlasId = "IMAGE_REANIM_WALLNUT_BLINK1";
                    break;
            }

            if (!string.IsNullOrEmpty(wallNutAtlasId) &&
                data.atlasParts.TryGetValue(wallNutAtlasId, out part))
            {
                return part;
            }

            return null;
        }

        private static PlantReanimFrame ParseFrame(JObject frame)
        {
            return new PlantReanimFrame
            {
                x = NullableFloat(frame["x"]),
                y = NullableFloat(frame["y"]),
                kx = NullableFloat(frame["kx"]),
                ky = NullableFloat(frame["ky"]),
                sx = NullableFloat(frame["sx"]),
                sy = NullableFloat(frame["sy"]),
                f = NullableInt(frame["f"]),
                i = NullableInt(frame["i"])
            };
        }

        private static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            int suffixIndex = id.IndexOf('|');

            if (suffixIndex >= 0)
                id = id.Substring(0, suffixIndex);

            return id.Trim().ToUpperInvariant();
        }

        private static float Number(JToken token)
        {
            float value;

            return token != null &&
                   float.TryParse(
                       token.ToString(),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out value)
                ? value
                : 0f;
        }

        private static float? NullableFloat(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            float value;

            return float.TryParse(
                token.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
                ? (float?)value
                : null;
        }

        private static int? NullableInt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            int value;

            return int.TryParse(
                token.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? (int?)value
                : null;
        }
    }
}