using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Đọc Atlas JSON và Reanim JSON, sau đó tạo ra một ReanimData độc lập.
/// Class này chỉ chịu trách nhiệm parse dữ liệu, không quản lý cache.
/// </summary>
public static class ReanimDataParser
{
    private const float DefaultAtlasWidth = 960f;
    private const float DefaultAtlasHeight = 960f;
    private const int DefaultFps = 30;

    /// <summary>
    /// Tên clip tự tạo cho file Reanim không có track anim_xxx
    /// (ví dụ file lá cờ): clip này chạy toàn bộ các frame.
    /// </summary>
    public const string DefaultClipName = "default";

    /// <summary>
    /// Parse một bộ Atlas + Reanim thành ReanimData.
    /// Trả về false nếu dữ liệu không hợp lệ.
    /// </summary>
    public static bool TryParse(
        TextAsset atlasJsonAsset,
        TextAsset reanimJsonAsset,
        out ReanimData data)
    {
        data = null;

        if (atlasJsonAsset == null || reanimJsonAsset == null)
        {
            Debug.LogError("ReanimDataParser: Atlas JSON hoặc Reanim JSON đang bị null.");
            return false;
        }

        try
        {
            ReanimData result = new ReanimData();

            // --------------------------------------------------
            // 1. Parse Atlas JSON
            // --------------------------------------------------

            JObject atlasRoot = JObject.Parse(atlasJsonAsset.text);
            JArray atlasResources = atlasRoot["resources"] as JArray;

            if (atlasResources == null)
            {
                Debug.LogError(
                    "ReanimDataParser: Không tìm thấy resources trong Atlas: "
                    + atlasJsonAsset.name);

                return false;
            }

            float atlasWidth = DefaultAtlasWidth;
            float atlasHeight = DefaultAtlasHeight;

            // Tìm kích thước Atlas từ resource ATLASIMAGE_.
            foreach (JToken token in atlasResources)
            {
                JObject attributes = token["attributes"] as JObject;

                string rawId = attributes != null
                    ? attributes.Value<string>("id")
                    : null;

                if (string.IsNullOrEmpty(rawId))
                    continue;

                if (!rawId.StartsWith(
                    "ATLASIMAGE_",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryParseFloat(attributes.Value<string>("aw"), out float width)
                    && TryParseFloat(attributes.Value<string>("ah"), out float height)
                    && width > 0f
                    && height > 0f)
                {
                    atlasWidth = width;
                    atlasHeight = height;
                }

                break;
            }

            // Lưu ID theo đúng index của resource trong Atlas.
            // Reanim sử dụng index này để tham chiếu đến hình ảnh.
            Dictionary<int, string> idsByIndex =
                new Dictionary<int, string>();

            for (int i = 0; i < atlasResources.Count; i++)
            {
                JObject resource = atlasResources[i] as JObject;

                if (resource == null)
                    continue;

                JObject attributes = resource["attributes"] as JObject;
                JObject atlasRect = resource["atlasRect"] as JObject;

                string rawId = attributes != null
                    ? attributes.Value<string>("id")
                    : null;

                if (string.IsNullOrEmpty(rawId))
                    continue;

                string imageId = NormalizeImageId(rawId);

                idsByIndex[i] = imageId;

                // Không có atlasRect thì không thể tạo UV.
                if (atlasRect == null)
                    continue;

                float x = atlasRect.Value<float>("x");
                float y = atlasRect.Value<float>("y");
                float width = atlasRect.Value<float>("width");
                float height = atlasRect.Value<float>("height");

                if (width <= 0f || height <= 0f)
                    continue;

                result.AtlasParts[imageId] = new ReanimData.AtlasPart
                {
                    width = width,
                    height = height,

                    uvMin = new Vector2(
                        x / atlasWidth,
                        1f - (y + height) / atlasHeight),

                    uvMax = new Vector2(
                        (x + width) / atlasWidth,
                        1f - y / atlasHeight)
                };
            }

            // --------------------------------------------------
            // 2. Parse Reanim JSON
            // --------------------------------------------------

            JObject reanimRoot = JObject.Parse(reanimJsonAsset.text);
            JArray reanimTracks = reanimRoot["tracks"] as JArray;

            if (reanimTracks == null)
            {
                Debug.LogError(
                    "ReanimDataParser: Không tìm thấy tracks trong Reanim: "
                    + reanimJsonAsset.name);

                return false;
            }

            result.Fps = reanimRoot["fps"] != null
                ? reanimRoot["fps"].Value<int>()
                : DefaultFps;

            if (result.Fps <= 0)
                result.Fps = DefaultFps;

            // --------------------------------------------------
            // 3. Tính Image Index Offset
            // --------------------------------------------------

            result.ImageIndexOffset = GetImageIndexOffset(
                atlasResources,
                reanimTracks);

            // --------------------------------------------------
            // 4. Parse toàn bộ track
            // --------------------------------------------------

            foreach (JToken token in reanimTracks)
            {
                JObject sourceTrack = token as JObject;

                if (sourceTrack == null)
                    continue;

                JArray sourceFrames =
                    sourceTrack["transforms"] as JArray;

                if (sourceFrames == null || sourceFrames.Count < 2)
                    continue;

                ReanimData.ReanimTrack track = ParseTrack(
                    sourceTrack,
                    sourceFrames,
                    idsByIndex,
                    result.ImageIndexOffset);

                // Track anim_xxx không chứa ảnh:
                // Đây là track đánh dấu khoảng animation.
                if (!track.hasImage
                    && !string.IsNullOrEmpty(track.name)
                    && track.name.StartsWith(
                        "anim_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ReanimData.ReanimClip clip = BuildClip(track);

                    if (clip != null)
                        result.Clips[clip.name] = clip;
                }
                // Root motion must be available to gameplay, but must not be
                // sent to the part renderer as a drawable sprite.
                else if (!track.hasImage
                    && string.Equals(
                        track.name,
                        "_ground",
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.GroundTrack = track;
                }
                // Track có chứa hình ảnh: lưu để Renderer sử dụng.
                else if (track.hasImage)
                {
                    result.Tracks.Add(track);
                }
            }

            // --------------------------------------------------
            // 4b. File không có clip nào (ví dụ file lá cờ):
            //     tự tạo clip "default" chạy hết toàn bộ frame.
            // --------------------------------------------------

            if (result.Clips.Count == 0 && result.Tracks.Count > 0)
            {
                int maxFrames = 0;

                foreach (ReanimData.ReanimTrack track in result.Tracks)
                    maxFrames = Mathf.Max(maxFrames, track.frames.Length);

                if (maxFrames >= 2)
                {
                    result.Clips[DefaultClipName] = new ReanimData.ReanimClip
                    {
                        name = DefaultClipName,
                        start = 0,
                        end = maxFrames - 1
                    };
                }
            }

            // --------------------------------------------------
            // 5. Xử lý thông tin Idle nếu có
            // --------------------------------------------------

            ReanimData.ReanimClip idleClip;

            if (result.Clips.TryGetValue("idle", out idleClip))
            {
                result.IdleFrameCount = idleClip.Length + 1;
            }
            else
            {
                // Không bắt buộc mọi bộ Reanim phải có idle.
                result.IdleFrameCount = 0;
            }

            // --------------------------------------------------
            // 6. Kiểm tra kết quả
            // --------------------------------------------------

            result.IsValid =
                result.Fps > 0
                && result.AtlasParts.Count > 0
                && (result.Tracks.Count > 0 || result.Clips.Count > 0);

            if (!result.IsValid)
            {
                Debug.LogError(
                    "ReanimDataParser: Dữ liệu parse không hợp lệ. "
                    + "Atlas: " + atlasJsonAsset.name
                    + ", Reanim: " + reanimJsonAsset.name);

                return false;
            }

            data = result;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "ReanimDataParser: Lỗi parse dữ liệu. "
                + "Atlas: " + atlasJsonAsset.name
                + ", Reanim: " + reanimJsonAsset.name
                + "\n" + e);

            data = null;
            return false;
        }
    }

    // ==========================================================
    // Tạo clip từ track anim_xxx
    // ==========================================================

    /// <summary>
    /// Clip là đoạn frame hiển thị liên tiếp đầu tiên
    /// trong track anim_xxx.
    /// </summary>
    private static ReanimData.ReanimClip BuildClip(
        ReanimData.ReanimTrack track)
    {
        int start = -1;
        int end = -1;

        for (int i = 0; i < track.frames.Length; i++)
        {
            if (track.frames[i].visible)
            {
                if (start < 0)
                    start = i;

                end = i;
            }
            else if (start >= 0)
            {
                break;
            }
        }

        if (start < 0)
            return null;

        return new ReanimData.ReanimClip
        {
            name = track.name.Substring(5),
            start = start,
            end = end
        };
    }

    // ==========================================================
    // Tính Image Index Offset
    // ==========================================================

    /// <summary>
    /// Index ảnh trong file Reanim lệch so với index trong Atlas một khoảng cố định.
    /// Cách 1: dựa vào track Zombie_body (file reanim zombie).
    /// Cách 2: nếu không có Zombie_body (ví dụ file lá cờ), thử ghép tên track
    ///         với ID ảnh: Zombie_flagpole -> IMAGE_REANIM_ZOMBIE_FLAGPOLE.
    /// </summary>
    private static int GetImageIndexOffset(
        JArray atlasResources,
        JArray reanimTracks)
    {
        // ---------- Cách 1: Zombie_body ----------
        int bodyAtlasIndex = -1;

        for (int i = 0; i < atlasResources.Count; i++)
        {
            string id = atlasResources[i]?["attributes"]?["id"]
                ?.Value<string>();

            if (NormalizeImageId(id) == "IMAGE_REANIM_ZOMBIE_BODY")
            {
                bodyAtlasIndex = i;
                break;
            }
        }

        JObject bodyTrack = FindTrack(
            reanimTracks,
            "Zombie_body");

        JArray bodyFrames =
            bodyTrack?["transforms"] as JArray;

        if (bodyAtlasIndex >= 0 && bodyFrames != null)
        {
            foreach (JToken token in bodyFrames)
            {
                JObject frame = token as JObject;

                if (frame != null && frame["i"] != null)
                {
                    return frame["i"].Value<int>() - bodyAtlasIndex;
                }
            }
        }

        // ---------- Cách 2: ghép tên track với ID ảnh ----------
        foreach (JToken trackToken in reanimTracks)
        {
            JObject track = trackToken as JObject;

            if (track == null)
                continue;

            string trackName = track.Value<string>("name");
            JArray frames = track["transforms"] as JArray;

            if (string.IsNullOrEmpty(trackName) || frames == null)
                continue;

            int fileIndex = -1;

            foreach (JToken frameToken in frames)
            {
                JObject frame = frameToken as JObject;

                if (frame != null && frame["i"] != null)
                {
                    fileIndex = frame["i"].Value<int>();
                    break;
                }
            }

            if (fileIndex < 0)
                continue;

            string wantedId = "IMAGE_REANIM_" + trackName.ToUpperInvariant();

            for (int i = 0; i < atlasResources.Count; i++)
            {
                string id = atlasResources[i]?["attributes"]?["id"]
                    ?.Value<string>();

                if (NormalizeImageId(id) == wantedId)
                    return fileIndex - i;
            }
        }

        return 0;
    }

    // ==========================================================
    // Parse một track
    // ==========================================================

    private static ReanimData.ReanimTrack ParseTrack(
        JObject sourceTrack,
        JArray sourceFrames,
        Dictionary<int, string> idsByIndex,
        int imageIndexOffset)
    {
        int frameCount = sourceFrames.Count;

        ReanimData.ReanimTrack track =
            new ReanimData.ReanimTrack
            {
                name = sourceTrack.Value<string>("name"),
                frames = new ReanimData.ReanimFrame[frameCount]
            };

        // Giữ transform của frame trước nếu frame hiện tại
        // không khai báo lại property tương ứng.
        ReanimData.ReanimFrame current =
            new ReanimData.ReanimFrame();

        for (int i = 0; i < frameCount; i++)
        {
            JObject frame = sourceFrames[i] as JObject;

            if (frame != null)
            {
                UpdateFloat(frame, "x", ref current.x);
                UpdateFloat(frame, "y", ref current.y);
                UpdateFloat(frame, "kx", ref current.kx);
                UpdateFloat(frame, "ky", ref current.ky);
                UpdateFloat(frame, "sx", ref current.sx);
                UpdateFloat(frame, "sy", ref current.sy);

                if (frame["f"] != null)
                {
                    current.visible =
                        frame["f"].Value<int>() != -1;
                }

                if (frame["i"] != null)
                {
                    track.hasImage = true;

                    int atlasIndex =
                        frame["i"].Value<int>() - imageIndexOffset;

                    string imageId;

                    current.imageId =
                        idsByIndex.TryGetValue(atlasIndex, out imageId)
                            ? imageId
                            : null;
                }
            }

            // Tạo bản sao frame để tránh mọi frame
            // cùng tham chiếu đến một object current.
            track.frames[i] = new ReanimData.ReanimFrame
            {
                x = current.x,
                y = current.y,
                kx = current.kx,
                ky = current.ky,
                sx = current.sx,
                sy = current.sy,
                visible = current.visible,
                imageId = current.imageId
            };
        }

        return track;
    }

    // ==========================================================
    // Các hàm hỗ trợ
    // ==========================================================

    private static void UpdateFloat(
        JObject frame,
        string propertyName,
        ref float value)
    {
        if (frame[propertyName] != null)
            value = frame[propertyName].Value<float>();
    }

    private static bool TryParseFloat(
        string text,
        out float value)
    {
        return float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static JObject FindTrack(
        JArray tracks,
        string trackName)
    {
        foreach (JToken token in tracks)
        {
            JObject track = token as JObject;

            if (track != null
                && string.Equals(
                    track.Value<string>("name"),
                    trackName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return track;
            }
        }

        return null;
    }

    private static string NormalizeImageId(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
            return string.Empty;

        return imageId
            .Split('|')[0]
            .Trim()
            .ToUpperInvariant();
    }
}
