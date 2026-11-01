using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Cache dùng chung cho xe cắt cỏ: parse atlas JSON + reanim JSON đúng 1 lần.
/// Khác với zombie: file reanim của xe có NHIỀU animation nằm chung 1 timeline
/// (anim_normal = frame 0..16, anim_tricked = frame 17..33), nên ở đây ta tự dò
/// "clip" (start..end) dựa vào cờ f (-1 = ẩn, 0 = hiện) của các track anim_*.
/// </summary>
public static class LawnMowerReanimCache
{
    public sealed class AtlasPart
    {
        public float width;
        public float height;
        public Vector2 uvMin;
        public Vector2 uvMax;
    }

    public sealed class Frame
    {
        public float x, y, kx, ky;
        public float sx = 1f, sy = 1f;
        public bool visible = true;
        public string imageId;
    }

    public sealed class Track
    {
        public string name;
        public Frame[] frames;
    }

    /// <summary>Đoạn frame của 1 animation, start và end đều là frame hợp lệ (inclusive).</summary>
    public sealed class Clip
    {
        public string name;
        public int start;
        public int end;
    }

    public const string ShadowImageId = "IMAGE_PLANTSHADOW";
    private const string BodyImageId = "IMAGE_REANIM_LAWNMOWER_BODY";
    private const string BodyTrackName = "LawnMower_body";

    public static readonly Dictionary<string, AtlasPart> AtlasParts = new Dictionary<string, AtlasPart>();
    public static readonly List<Track> Tracks = new List<Track>();
    public static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>(StringComparer.OrdinalIgnoreCase);
    public static int Fps = 20;
    public static int ImageIndexOffset;
    public static bool IsInitialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AtlasParts.Clear();
        Tracks.Clear();
        Clips.Clear();
        IsInitialized = false;
    }

    public static bool Initialize(TextAsset atlasJsonAsset, TextAsset reanimJsonAsset)
    {
        if (IsInitialized) return true;

        try
        {
            // ---------- 1. Atlas ----------
            JObject atlasRoot = JObject.Parse(atlasJsonAsset.text);
            JArray atlasResources = atlasRoot["resources"] as JArray;
            if (atlasResources == null) return false;

            float atlasWidth = 2048f;
            float atlasHeight = 2048f;
            foreach (JToken token in atlasResources)
            {
                JObject attributes = token["attributes"] as JObject;
                string rawId = attributes != null ? attributes.Value<string>("id") : null;
                if (string.IsNullOrEmpty(rawId) || !rawId.StartsWith("ATLASIMAGE_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryParseFloat(attributes.Value<string>("aw"), out float w) &&
                    TryParseFloat(attributes.Value<string>("ah"), out float h) && w > 0f && h > 0f)
                {
                    atlasWidth = w;
                    atlasHeight = h;
                }
                break;
            }

            // index trong mảng resources -> id ảnh (dùng để đổi "i" của reanim ra ảnh)
            Dictionary<int, string> idsByIndex = new Dictionary<int, string>();
            int bodyAtlasIndex = -1;

            for (int i = 0; i < atlasResources.Count; i++)
            {
                JObject resource = atlasResources[i] as JObject;
                JObject attributes = resource != null ? resource["attributes"] as JObject : null;
                JObject atlasRect = resource != null ? resource["atlasRect"] as JObject : null;
                string rawId = attributes != null ? attributes.Value<string>("id") : null;
                if (string.IsNullOrEmpty(rawId)) continue;

                string id = NormalizeImageId(rawId);
                idsByIndex[i] = id;
                if (id == BodyImageId) bodyAtlasIndex = i;

                bool wanted = id.StartsWith("IMAGE_REANIM_LAWNMOWER", StringComparison.Ordinal) || id == ShadowImageId;
                if (!wanted || atlasRect == null) continue;

                float x = atlasRect.Value<float>("x");
                float y = atlasRect.Value<float>("y");
                float width = atlasRect.Value<float>("width");
                float height = atlasRect.Value<float>("height");
                if (width <= 0f || height <= 0f) continue;

                AtlasParts[id] = new AtlasPart
                {
                    width = width,
                    height = height,
                    uvMin = new Vector2(x / atlasWidth, 1f - (y + height) / atlasHeight),
                    uvMax = new Vector2((x + width) / atlasWidth, 1f - y / atlasHeight)
                };
            }

            // ---------- 2. Reanim ----------
            JObject reanimRoot = JObject.Parse(reanimJsonAsset.text);
            JArray reanimTracks = reanimRoot["tracks"] as JArray;
            if (reanimTracks == null) return false;

            Fps = reanimRoot["fps"] != null ? reanimRoot["fps"].Value<int>() : 20;

            // "i" trong reanim lệch so với index trong atlas JSON -> tính offset nhờ track thân xe
            ImageIndexOffset = 0;
            JObject bodyTrack = FindTrack(reanimTracks, BodyTrackName);
            JArray bodyFrames = bodyTrack != null ? bodyTrack["transforms"] as JArray : null;
            if (bodyAtlasIndex >= 0 && bodyFrames != null)
            {
                foreach (JObject f in bodyFrames)
                {
                    if (f["i"] != null)
                    {
                        ImageIndexOffset = f["i"].Value<int>() - bodyAtlasIndex;
                        break;
                    }
                }
            }

            // ---------- 3. Parse track + clip ----------
            foreach (JObject sourceTrack in reanimTracks)
            {
                string name = sourceTrack.Value<string>("name") ?? string.Empty;
                JArray sourceFrames = sourceTrack["transforms"] as JArray;
                if (sourceFrames == null || sourceFrames.Count == 0) continue;

                Track track = ParseTrack(name, sourceFrames, idsByIndex, ImageIndexOffset);

                if (name.StartsWith("anim_", StringComparison.OrdinalIgnoreCase))
                {
                    Clip clip = BuildClip(name, track);
                    if (clip != null) Clips[name.Substring(5)] = clip; // "anim_normal" -> "normal"
                }
                else
                {
                    Tracks.Add(track);
                }
            }

            if (Clips.Count == 0) return false;

            IsInitialized = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi khởi tạo LawnMowerReanimCache: " + e.Message);
            return false;
        }
    }

    private static Clip BuildClip(string trackName, Track animTrack)
    {
        int count = animTrack.frames.Length;
        int start = -1;
        for (int i = 0; i < count; i++)
        {
            if (animTrack.frames[i].visible) { start = i; break; }
        }
        if (start < 0) return null;

        int end = start;
        while (end + 1 < count && animTrack.frames[end + 1].visible) end++;

        return new Clip { name = trackName, start = start, end = end };
    }

    private static Track ParseTrack(string name, JArray sourceFrames, Dictionary<int, string> idsByIndex, int imageIndexOffset)
    {
        Track track = new Track { name = name, frames = new Frame[sourceFrames.Count] };
        Frame current = new Frame();

        for (int i = 0; i < sourceFrames.Count; i++)
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
                    current.visible = frame["f"].Value<int>() != -1;

                if (frame["i"] != null)
                {
                    int atlasIndex = frame["i"].Value<int>() - imageIndexOffset;
                    current.imageId = idsByIndex.TryGetValue(atlasIndex, out string imageId) ? imageId : null;
                }
            }

            track.frames[i] = new Frame
            {
                x = current.x, y = current.y,
                kx = current.kx, ky = current.ky,
                sx = current.sx, sy = current.sy,
                visible = current.visible,
                imageId = current.imageId
            };
        }
        return track;
    }

    private static void UpdateFloat(JObject frame, string propertyName, ref float value)
    {
        if (frame[propertyName] != null) value = frame[propertyName].Value<float>();
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static JObject FindTrack(JArray tracks, string trackName)
    {
        foreach (JObject track in tracks)
        {
            if (string.Equals(track.Value<string>("name"), trackName, StringComparison.OrdinalIgnoreCase))
                return track;
        }
        return null;
    }

    private static string NormalizeImageId(string imageId)
    {
        return string.IsNullOrEmpty(imageId) ? string.Empty : imageId.Split('|')[0].Trim().ToUpperInvariant();
    }
}

public enum LawnMowerAnimation
{
    Normal,   // anim_normal  : xe cắt cỏ thường (frame 0..16)
    Tricked   // anim_tricked : xe bản "tricked" (frame 17..33)
}

/// <summary>
/// Cắt các bộ phận xe cắt cỏ từ atlas rồi ghép lại + chạy animation theo reanim.
/// Gắn script này vào 1 GameObject rỗng, kéo thả 3 tài nguyên là chạy.
/// </summary>
public class PvZReanimLawnMower : MonoBehaviour
{
    [Header("Tài nguyên")]
    public Texture2D atlasTexture;          // LOADINGIMAGES_PLANTSZOMBIES_1_960_00.PNG
    public TextAsset atlasJsonAsset;        // LoadingImages_PlantsZombies_1_960.json
    public TextAsset reanimJsonAsset;       // lawnmower_reanim_compiled_reanim.json

    [Header("Animation")]
    public LawnMowerAnimation animation = LawnMowerAnimation.Normal;
    public bool playing = true;
    [Tooltip("Bật: xe đứng im ở frame đầu khi mới tạo, gọi Run() để chạy.")]
    public bool startIdle = true;
    public float animationSpeed = 1f;

    [Header("Hiển thị")]
    public float pixelPerUnit = 100f;
    public Vector2 globalScale = Vector2.one;
    [Tooltip("Scale riêng cho toàn bộ các bộ phận xe cắt cỏ (Vector2).")]
    public Vector2 partsScale = Vector2.one;
    [Tooltip("Tọa độ trong reanim nhân hệ số này (giống code zombie = 2).")]
    public float positionScale = 2f;

    [Header("Sorting")]
    public string sortingLayerName = "";
    public int sortingOrderBase = 0;

    [Header("Bóng")]
    public bool showShadow = true;
    public Vector2 shadowOffset = Vector2.zero;
    public float shadowScale = 1f;

    [Header("Cấu hình di chuyển (Trái sang Phải)")]
    [Tooltip("Tốc độ di chuyển ngang (units/giây trong thế giới Unity)")]
    public float moveSpeed = 3f;
    [Tooltip("Khoảng cách chạy ngắn lúc đầu trận (từ nhà ra line)")]
    public float startRunDistance = 1.5f;
    [Tooltip("Khoảng cách chạy xa khi cán dọn sạch màn hình")]
    public float longRunDistance = 10f;

    // Trạng thái di chuyển nội bộ
    private enum MovementState { None, MovingShort, MovingLong }
    private MovementState currentMovementState = MovementState.None;
    private float targetMoveDistance = 0f;
    private float traveledDistance = 0f;
    private Action onMovementComplete;

    private sealed class PartRenderer
    {
        public LawnMowerReanimCache.Track track;
        public MeshRenderer renderer;
        public Mesh mesh;
        public Vector3[] vertices = new Vector3[4];
        public Vector2[] uvs = new Vector2[4];
        public string currentImageId;
        public int index;
    }

    private readonly List<PartRenderer> parts = new List<PartRenderer>();
    private PartRenderer shadowPart;
    private readonly Vector2[] cornerBuffer = new Vector2[4];
    private static Material sharedMaterial;
    private static Texture sharedMaterialTexture;

    private LawnMowerReanimCache.Clip clip;
    private float elapsedTime;
    private LawnMowerAnimation builtAnimation;

    private void Start()
    {
        if (atlasTexture == null || atlasJsonAsset == null || reanimJsonAsset == null)
        {
            Debug.LogError("PvZReanimLawnMower: thiếu atlasTexture / atlasJsonAsset / reanimJsonAsset.");
            return;
        }

        if (!LawnMowerReanimCache.Initialize(atlasJsonAsset, reanimJsonAsset))
        {
            Debug.LogError("PvZReanimLawnMower: không thể khởi tạo dữ liệu reanim.");
            return;
        }

        if (sharedMaterial == null || sharedMaterialTexture != atlasTexture)
        {
            sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = atlasTexture };
            sharedMaterialTexture = atlasTexture;
        }

        BuildParts();
        SetAnimation(animation, true);
        if (startIdle) Idle();

        // Ví dụ gọi hàm 1 khi bắt đầu màn chơi:
        StartRunThenIdle();
    }

    private void Update()
    {
        if (parts.Count == 0 || clip == null) return;

        // Xử lý di chuyển ngang (Trái sang Phải, giữ nguyên Y)
        if (currentMovementState != MovementState.None)
        {
            float step = moveSpeed * Time.deltaTime;
            transform.Translate(Vector3.right * step, Space.World);
            traveledDistance += step;

            if (traveledDistance >= targetMoveDistance)
            {
                currentMovementState = MovementState.None;
                
                // Thực hiện callback khi chạy xong
                Action callback = onMovementComplete;
                onMovementComplete = null;
                callback?.Invoke();
            }
        }

        // Đổi animation từ Inspector khi đang chạy
        if (animation != builtAnimation) SetAnimation(animation, true);

        if (playing) elapsedTime += Time.deltaTime * animationSpeed;
        ApplyFrame(GetFramePosition());
    }

    // ------------------------------------------------------------------
    // CÁC HÀM DI CHUYỂN
    // ------------------------------------------------------------------
    public void StartRunThenIdle(Action onComplete = null)
    {
        traveledDistance = 0f;
        targetMoveDistance = startRunDistance;
        currentMovementState = MovementState.MovingShort;

        SetAnimation(LawnMowerAnimation.Normal, true);
        Play();

        onMovementComplete = () =>
        {
            Idle();
            onComplete?.Invoke();
        };
    }

    public void RunAcrossAndHide(Action onComplete = null)
    {
        traveledDistance = 0f;
        targetMoveDistance = longRunDistance;
        currentMovementState = MovementState.MovingLong;

        SetAnimation(LawnMowerAnimation.Tricked, true);
        Play();

        onMovementComplete = () =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        };
    }

    // ------------------------------------------------------------------
    // API công khai
    // ------------------------------------------------------------------

    /// <summary>Đổi animation (Normal / Tricked). restart = true thì chạy lại từ đầu.</summary>
    public void SetAnimation(LawnMowerAnimation newAnimation, bool restart = true)
    {
        string key = newAnimation.ToString();
        if (!LawnMowerReanimCache.Clips.TryGetValue(key, out LawnMowerReanimCache.Clip found))
        {
            Debug.LogWarning("PvZReanimLawnMower: không có animation '" + key + "' trong file reanim.");
            return;
        }

        animation = newAnimation;
        builtAnimation = newAnimation;
        clip = found;
        if (restart) elapsedTime = 0f;

        RefreshShadow();
        ApplyFrame(GetFramePosition());
    }

    public void Play() { playing = true; }
    public void Pause() { playing = false; }

    /// <summary>Trạng thái đứng im: dừng ở frame đầu của animation hiện tại (xe chưa chạy).</summary>
    public void Idle()
    {
        playing = false;
        elapsedTime = 0f;
        if (clip != null) ApplyFrame(clip.start);
    }

    /// <summary>Trạng thái chạy: chạy animation Normal từ đầu (bánh quay, thân rung).</summary>
    public void Run()
    {
        animation = LawnMowerAnimation.Normal;
        SetAnimation(LawnMowerAnimation.Normal, true);
        playing = true;
    }

    // ------------------------------------------------------------------
    // Dựng các bộ phận
    // ------------------------------------------------------------------

    private void BuildParts()
    {
        int index = 0;
        foreach (LawnMowerReanimCache.Track track in LawnMowerReanimCache.Tracks)
        {
            bool hasSprite = false;
            foreach (LawnMowerReanimCache.Frame f in track.frames)
            {
                if (!string.IsNullOrEmpty(f.imageId) && LawnMowerReanimCache.AtlasParts.ContainsKey(f.imageId))
                {
                    hasSprite = true;
                    break;
                }
            }

            index++;
            if (!hasSprite) continue;

            parts.Add(CreatePart(track, index));
        }

        if (showShadow && LawnMowerReanimCache.AtlasParts.ContainsKey(LawnMowerReanimCache.ShadowImageId))
        {
            shadowPart = CreatePart(new LawnMowerReanimCache.Track { name = "Shadow", frames = new LawnMowerReanimCache.Frame[0] }, 0);
            SetPartUvs(shadowPart, LawnMowerReanimCache.AtlasParts[LawnMowerReanimCache.ShadowImageId], LawnMowerReanimCache.ShadowImageId);
        }

        ApplySortingOrders();
    }

    private PartRenderer CreatePart(LawnMowerReanimCache.Track track, int index)
    {
        GameObject go = new GameObject(track.name);
        go.transform.SetParent(transform, false);

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Mesh mesh = new Mesh { name = track.name };
        mesh.MarkDynamic();
        mesh.vertices = new Vector3[4];
        mesh.uv = new Vector2[4];
        mesh.colors32 = new Color32[]
        {
            new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        meshFilter.sharedMesh = mesh;

        return new PartRenderer { track = track, renderer = meshRenderer, mesh = mesh, index = index };
    }

    private void ApplySortingOrders()
    {
        foreach (PartRenderer part in parts)
        {
            if (!string.IsNullOrEmpty(sortingLayerName)) part.renderer.sortingLayerName = sortingLayerName;
            part.renderer.sortingOrder = sortingOrderBase + part.index;
        }

        if (shadowPart != null)
        {
            if (!string.IsNullOrEmpty(sortingLayerName)) shadowPart.renderer.sortingLayerName = sortingLayerName;
            shadowPart.renderer.sortingOrder = sortingOrderBase - 1;
        }
    }

    // ------------------------------------------------------------------
    // Chạy animation
    // ------------------------------------------------------------------

    private float GetFramePosition()
    {
        int length = clip.end - clip.start;
        if (length <= 0) return clip.start;
        float local = Mathf.Repeat(elapsedTime * LawnMowerReanimCache.Fps, length);
        return clip.start + local;
    }

    private void ApplyFrame(float framePosition)
    {
        int frameA = Mathf.Clamp(Mathf.FloorToInt(framePosition), clip.start, clip.end);
        int frameB = Mathf.Min(frameA + 1, clip.end);
        float t = framePosition - Mathf.Floor(framePosition);

        foreach (PartRenderer part in parts)
        {
            LawnMowerReanimCache.Frame a = part.track.frames[frameA];
            LawnMowerReanimCache.Frame b = part.track.frames[frameB];

            LawnMowerReanimCache.AtlasPart atlasPart = null;
            bool hasSprite = a.visible && !string.IsNullOrEmpty(a.imageId) &&
                             LawnMowerReanimCache.AtlasParts.TryGetValue(a.imageId, out atlasPart);
            part.renderer.enabled = hasSprite;
            if (!hasSprite) continue;

            SetPartUvs(part, atlasPart, a.imageId);
            ComputeCorners(a, b, t, atlasPart, cornerBuffer);
            for (int i = 0; i < 4; i++)
                part.vertices[i] = ToUnity(cornerBuffer[i].x, cornerBuffer[i].y);

            part.mesh.SetVertices(part.vertices);
            part.mesh.RecalculateBounds();
        }
    }

    private static void SetPartUvs(PartRenderer part, LawnMowerReanimCache.AtlasPart atlasPart, string imageId)
    {
        if (part.currentImageId == imageId) return;

        part.uvs[0] = new Vector2(atlasPart.uvMin.x, atlasPart.uvMax.y);
        part.uvs[1] = new Vector2(atlasPart.uvMax.x, atlasPart.uvMax.y);
        part.uvs[2] = new Vector2(atlasPart.uvMax.x, atlasPart.uvMin.y);
        part.uvs[3] = new Vector2(atlasPart.uvMin.x, atlasPart.uvMin.y);
        part.mesh.SetUVs(0, part.uvs);
        part.currentImageId = imageId;
    }

    private void ComputeCorners(LawnMowerReanimCache.Frame a, LawnMowerReanimCache.Frame b, float t,
        LawnMowerReanimCache.AtlasPart atlasPart, Vector2[] corners)
    {
        float x = Mathf.Lerp(a.x, b.x, t);
        float y = Mathf.Lerp(a.y, b.y, t);
        float kx = Mathf.Lerp(a.kx, b.kx, t) * Mathf.Deg2Rad;
        float ky = Mathf.Lerp(a.ky, b.ky, t) * Mathf.Deg2Rad;
        
        // Kết hợp scale từ reanim, globalScale và partsScale mới thêm
        float sx = Mathf.Lerp(a.sx, b.sx, t) * globalScale.x * partsScale.x;
        float sy = Mathf.Lerp(a.sy, b.sy, t) * globalScale.y * partsScale.y;

        float m00 = Mathf.Cos(kx) * sx;
        float m01 = -Mathf.Sin(ky) * sy;
        float m10 = Mathf.Sin(kx) * sx;
        float m11 = Mathf.Cos(ky) * sy;

        float tx = x * positionScale * globalScale.x;
        float ty = y * positionScale * globalScale.y;
        float w = atlasPart.width;
        float h = atlasPart.height;

        corners[0] = new Vector2(tx, ty);
        corners[1] = new Vector2(tx + m00 * w, ty + m10 * w);
        corners[2] = new Vector2(tx + m00 * w + m01 * h, ty + m10 * w + m11 * h);
        corners[3] = new Vector2(tx + m01 * h, ty + m11 * h);
    }

    private Vector3 ToUnity(float pixelX, float pixelY)
    {
        return new Vector3(pixelX / pixelPerUnit, -pixelY / pixelPerUnit, 0f);
    }

    // ------------------------------------------------------------------
    // Bóng
    // ------------------------------------------------------------------

    private void RefreshShadow()
    {
        if (shadowPart == null || clip == null) return;

        float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (PartRenderer part in parts)
        {
            LawnMowerReanimCache.Frame f = part.track.frames[clip.start];
            LawnMowerReanimCache.AtlasPart atlasPart;
            if (!f.visible || string.IsNullOrEmpty(f.imageId) ||
                !LawnMowerReanimCache.AtlasParts.TryGetValue(f.imageId, out atlasPart))
                continue;

            ComputeCorners(f, f, 0f, atlasPart, cornerBuffer);
            for (int i = 0; i < 4; i++)
            {
                minX = Mathf.Min(minX, cornerBuffer[i].x);
                maxX = Mathf.Max(maxX, cornerBuffer[i].x);
                maxY = Mathf.Max(maxY, cornerBuffer[i].y);
            }
        }

        if (minX > maxX) { shadowPart.renderer.enabled = false; return; }

        LawnMowerReanimCache.AtlasPart shadow = LawnMowerReanimCache.AtlasParts[LawnMowerReanimCache.ShadowImageId];
        float halfW = shadow.width * globalScale.x * partsScale.x * shadowScale * 0.5f;
        float halfH = shadow.height * globalScale.y * partsScale.y * shadowScale * 0.5f;
        float cx = (minX + maxX) * 0.5f + shadowOffset.x * positionScale;
        float cy = maxY + shadowOffset.y * positionScale;

        shadowPart.vertices[0] = ToUnity(cx - halfW, cy - halfH);
        shadowPart.vertices[1] = ToUnity(cx + halfW, cy - halfH);
        shadowPart.vertices[2] = ToUnity(cx + halfW, cy + halfH);
        shadowPart.vertices[3] = ToUnity(cx - halfW, cy + halfH);
        shadowPart.mesh.SetVertices(shadowPart.vertices);
        shadowPart.mesh.RecalculateBounds();
        shadowPart.renderer.enabled = true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || parts.Count == 0) return;
        ApplySortingOrders();
        RefreshShadow();
    }

    private void OnDestroy()
    {
        foreach (PartRenderer part in parts)
            if (part.mesh != null) Destroy(part.mesh);
        if (shadowPart != null && shadowPart.mesh != null) Destroy(shadowPart.mesh);
    }
}