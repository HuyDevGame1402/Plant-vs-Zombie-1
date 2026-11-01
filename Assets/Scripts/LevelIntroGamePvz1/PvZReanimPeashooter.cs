using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Cache dùng chung cho cây Peashooter: parse atlas JSON + reanim JSON đúng 1 lần.
///
/// File reanim của Peashooter có 4 clip nằm chung 1 timeline (12 fps, mỗi clip 25 frame):
///   anim_idle      = frame  4..28  : chỉ phần THÂN (lá, cuống)
///   anim_head_idle = frame 29..53  : chỉ phần ĐẦU (đầu, miệng, lá đầu, lông mày)
///   anim_shooting  = frame 54..78  : chỉ phần ĐẦU, animation bắn
///   anim_full_idle = frame 79..103 : thân + đầu đã "bake" sẵn vào nhau (idle hoàn chỉnh)
/// Frame cuối của mỗi clip giống hệt frame đầu nên khi lặp không nội suy từ cuối về đầu.
/// </summary>
public static class PeashooterReanimCache
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
        public bool hasImage;
        public Frame[] frames;
    }

    /// <summary>Đoạn frame của 1 animation, start và end đều là frame hợp lệ (inclusive).</summary>
    public sealed class Clip
    {
        public string name;
        public int start;
        public int end;
        public int Length { get { return end - start; } }
    }

    public const string ShadowImageId = "IMAGE_PLANTSHADOW";
    private const string RefImageId = "IMAGE_REANIM_PEASHOOTER_BACKLEAF";
    private const string RefTrackName = "backleaf";
    public const string FaceTrackName = "anim_face";
    public const string BlinkTrackName = "anim_blink";

    public static readonly Dictionary<string, AtlasPart> AtlasParts = new Dictionary<string, AtlasPart>();
    public static readonly List<Track> Tracks = new List<Track>();                 // chỉ các track vẽ được (đúng thứ tự vẽ)
    public static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>(StringComparer.OrdinalIgnoreCase);
    public static Clip BlinkClip;
    public static float[] AttachOffsetX;   // độ lệch đầu do thân lắc: full_idle - head_idle (theo từng frame)
    public static float[] AttachOffsetY;
    public static int Fps = 12;
    public static int ImageIndexOffset;
    public static bool IsInitialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AtlasParts.Clear();
        Tracks.Clear();
        Clips.Clear();
        BlinkClip = null;
        AttachOffsetX = null;
        AttachOffsetY = null;
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

            Dictionary<int, string> idsByIndex = new Dictionary<int, string>();
            int refAtlasIndex = -1;

            for (int i = 0; i < atlasResources.Count; i++)
            {
                JObject resource = atlasResources[i] as JObject;
                JObject attributes = resource != null ? resource["attributes"] as JObject : null;
                JObject atlasRect = resource != null ? resource["atlasRect"] as JObject : null;
                string rawId = attributes != null ? attributes.Value<string>("id") : null;
                if (string.IsNullOrEmpty(rawId)) continue;

                string id = NormalizeImageId(rawId);
                idsByIndex[i] = id;
                if (id == RefImageId) refAtlasIndex = i;

                bool wanted = id.StartsWith("IMAGE_REANIM_PEASHOOTER", StringComparison.Ordinal) || id == ShadowImageId;
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

            Fps = reanimRoot["fps"] != null ? reanimRoot["fps"].Value<int>() : 12;

            // "i" trong reanim lệch so với index trong atlas JSON -> tính offset nhờ track backleaf
            ImageIndexOffset = 0;
            JObject refTrack = FindTrack(reanimTracks, RefTrackName);
            JArray refFrames = refTrack != null ? refTrack["transforms"] as JArray : null;
            if (refAtlasIndex >= 0 && refFrames != null)
            {
                foreach (JObject f in refFrames)
                {
                    if (f["i"] != null)
                    {
                        ImageIndexOffset = f["i"].Value<int>() - refAtlasIndex;
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

                bool isClipTrack = !track.hasImage &&
                                   name.StartsWith("anim_", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("anim_stem", StringComparison.OrdinalIgnoreCase);
                if (isClipTrack)
                {
                    Clip clip = BuildClip(name, track);
                    if (clip != null) Clips[name.Substring(5)] = clip; // "anim_full_idle" -> "full_idle"
                }
                else if (track.hasImage)
                {
                    Tracks.Add(track);
                    if (name.Equals(BlinkTrackName, StringComparison.OrdinalIgnoreCase))
                        BlinkClip = BuildClip(name, track);
                }
            }

            if (!Clips.ContainsKey("full_idle") || !Clips.ContainsKey("shooting")) return false;

            BuildAttachOffsets();

            IsInitialized = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi khởi tạo PeashooterReanimCache: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// Khi bắn, đầu chạy clip "shooting" nhưng thân vẫn lắc theo full_idle.
    /// Clip shooting không chứa độ lệch do thân lắc, nên ta đo độ lệch đó từ track anim_face:
    /// offset[k] = vị trí mặt ở full_idle - vị trí mặt ở head_idle (cùng frame k).
    /// </summary>
    private static void BuildAttachOffsets()
    {
        AttachOffsetX = null;
        AttachOffsetY = null;

        Clip full, head;
        if (!Clips.TryGetValue("full_idle", out full) || !Clips.TryGetValue("head_idle", out head)) return;
        if (full.Length != head.Length) return;

        Track face = null;
        foreach (Track t in Tracks)
        {
            if (t.name.Equals(FaceTrackName, StringComparison.OrdinalIgnoreCase)) { face = t; break; }
        }
        if (face == null) return;

        int count = full.Length + 1;
        AttachOffsetX = new float[count];
        AttachOffsetY = new float[count];
        for (int k = 0; k < count; k++)
        {
            Frame f = face.frames[full.start + k];
            Frame h = face.frames[head.start + k];
            AttachOffsetX[k] = f.x - h.x;
            AttachOffsetY[k] = f.y - h.y;
        }
    }

    private static Clip BuildClip(string trackName, Track track)
    {
        int count = track.frames.Length;
        int start = -1;
        for (int i = 0; i < count; i++)
        {
            if (track.frames[i].visible) { start = i; break; }
        }
        if (start < 0) return null;

        int end = start;
        while (end + 1 < count && track.frames[end + 1].visible) end++;

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
                    track.hasImage = true;
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

/// <summary>
/// Cây Peashooter: cắt các bộ phận từ atlas rồi ghép + chạy animation idle, bắn và chớp mắt.
/// Gắn script vào 1 GameObject rỗng, kéo 3 tài nguyên vào. Gọi Shoot() để cây bắn.
/// </summary>
public class PvZReanimPeashooter : MonoBehaviour
{
    [Header("Tài nguyên")]
    public Texture2D atlasTexture;          // LOADINGIMAGES_PLANTSZOMBIES_1_960_00.PNG
    public TextAsset atlasJsonAsset;        // LoadingImages_PlantsZombies_1_960.json
    public TextAsset reanimJsonAsset;       // peashooter_reanim_compiled_reanim.json

    [Header("Animation")]
    [Tooltip("Tốc độ chạy idle (và thân khi đang bắn).")]
    public float animationSpeed = 1f;
    [Tooltip("Nhân thêm tốc độ cho animation bắn. 12fps x 3 = ~36fps (bắn xong trong ~0.7 giây).")]
    public float shootSpeed = 3f;
    [Tooltip("Thời gian chuyển mượt giữa idle <-> bắn (giây). 0 = không chuyển mượt.")]
    public float blendTime = 0.1f;
    [Tooltip("Frame của animation bắn (tính từ 0) mà sự kiện OnFire được gọi - lúc đó bạn tạo viên đạn. Chỉnh cho khớp.")]
    public int fireFrame = 12;

    [Header("Chớp mắt")]
    public bool autoBlink = true;
    public float blinkIntervalMin = 3f;
    public float blinkIntervalMax = 6f;

    [Header("Hiển thị")]
    public float pixelPerUnit = 100f;
    public Vector2 globalScale = Vector2.one;
    [Tooltip("Scale riêng của các bộ phận cây (không ảnh hưởng bóng). Mặc định (1,1).")]
    public Vector2 plantScale = Vector2.one;
    [Tooltip("Tọa độ trong reanim nhân hệ số này (giống code zombie / xe = 2).")]
    public float positionScale = 2f;

    [Header("Sorting")]
    public string sortingLayerName = "";
    public int sortingOrderBase = 0;

    [Header("Bóng")]
    public bool showShadow = true;
    [Tooltip("Dịch vị trí bóng (đơn vị reanim, nhân positionScale). +x sang phải, +y xuống dưới. Cả 2 lớp bóng dùng chung offset này.")]
    public Vector2 shadowOffset = Vector2.zero;
    public float shadowScale = 1f;

    [Header("Test trong Editor")]
    public bool autoShootTest = false;
    public float autoShootInterval = 2f;
    [Tooltip("Đang Play: tick vào ô này để cây bắn 1 phát (tự bỏ tick sau khi bắn).")]
    public bool shootNow = false;
    [Tooltip("In log ra Console mỗi khi sự kiện OnFire (frame tạo đạn) được gọi.")]
    public bool logFireEvent = false;

    /// <summary>Gọi đúng 1 lần mỗi phát bắn, tại fireFrame. Nơi bạn tạo viên đạn.</summary>
    public event Action OnFire;
    /// <summary>Gọi khi animation bắn kết thúc và cây trở về idle.</summary>
    public event Action OnShootFinished;

    public bool IsShooting { get { return shooting; } }

    private enum Layer { Body, Head, Blink }

    private struct Pose
    {
        public float x, y, kx, ky, sx, sy;
        public bool visible;
        public string imageId;
    }

    private sealed class PartRenderer
    {
        public PeashooterReanimCache.Track track;
        public Layer layer;
        public MeshRenderer renderer;
        public Mesh mesh;
        public Vector3[] vertices = new Vector3[4];
        public Vector2[] uvs = new Vector2[4];
        public string currentImageId;
        public int index;
        public Pose lastPose;
        public Pose blendFrom;
        public bool hasLastPose;
    }

    private readonly List<PartRenderer> parts = new List<PartRenderer>();
    private PartRenderer shadowPart;
    private PartRenderer shadowPart2;   // lớp bóng thứ 2, chồng lên lớp 1 cho đậm màu
    private Vector2 lastPlantScale = Vector2.one;
    private PartRenderer facePart;
    private readonly Vector2[] cornerBuffer = new Vector2[4];
    private static Material sharedMaterial;
    private static Texture sharedMaterialTexture;

    private PeashooterReanimCache.Clip fullClip;
    private PeashooterReanimCache.Clip shootClip;
    private PeashooterReanimCache.Clip blinkClip;

    private bool ready;
    private float bodyTime;

    private bool shooting;
    private float shootTime;
    private bool firedThisShot;

    private float blendTimer;

    private bool blinking;
    private float blinkTime;
    private float blinkCountdown;

    private float autoShootCountdown;

    private void Start()
    {
        if (atlasTexture == null || atlasJsonAsset == null || reanimJsonAsset == null)
        {
            Debug.LogError("PvZReanimPeashooter: thiếu atlasTexture / atlasJsonAsset / reanimJsonAsset.");
            return;
        }

        if (!PeashooterReanimCache.Initialize(atlasJsonAsset, reanimJsonAsset))
        {
            Debug.LogError("PvZReanimPeashooter: không thể khởi tạo dữ liệu reanim.");
            return;
        }

        fullClip = PeashooterReanimCache.Clips["full_idle"];
        shootClip = PeashooterReanimCache.Clips["shooting"];
        blinkClip = PeashooterReanimCache.BlinkClip;

        if (sharedMaterial == null || sharedMaterialTexture != atlasTexture)
        {
            sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = atlasTexture };
            sharedMaterialTexture = atlasTexture;
        }

        BuildParts();
        ResetBlinkCountdown();
        autoShootCountdown = autoShootInterval;
        ready = true;

        UpdateAnimation(0f);
        RefreshShadow();
    }

    private void Update()
    {
        if (!ready) return;

        float dt = Time.deltaTime;

        if (plantScale != lastPlantScale)
        {
            ApplyPlantScale();
            RefreshShadow();
        }

        if (autoShootTest)
        {
            autoShootCountdown -= dt;
            if (autoShootCountdown <= 0f)
            {
                Shoot();
                autoShootCountdown = autoShootInterval;
            }
        }

        UpdateAnimation(dt);
    }

    // ------------------------------------------------------------------
    // API công khai
    // ------------------------------------------------------------------

    /// <summary>Cho cây bắn 1 phát. Trả về false nếu đang bắn dở.</summary>
    [ContextMenu("Shoot")]
    public bool Shoot()
    {
        if (!ready || shooting) return false;

        shooting = true;
        shootTime = 0f;
        firedThisShot = false;
        blinking = false;
        StartBlend();
        return true;
    }

    // ------------------------------------------------------------------
    // Dựng các bộ phận
    // ------------------------------------------------------------------

    private void BuildParts()
    {
        int index = 0;
        foreach (PeashooterReanimCache.Track track in PeashooterReanimCache.Tracks)
        {
            // thứ tự track trong JSON = thứ tự vẽ (track sau vẽ đè lên track trước)
            index++;

            bool hasSprite = false;
            foreach (PeashooterReanimCache.Frame f in track.frames)
            {
                if (!string.IsNullOrEmpty(f.imageId) && PeashooterReanimCache.AtlasParts.ContainsKey(f.imageId))
                {
                    hasSprite = true;
                    break;
                }
            }
            if (!hasSprite) continue;

            PartRenderer part = CreatePart(track, index);

            if (track.name.Equals(PeashooterReanimCache.BlinkTrackName, StringComparison.OrdinalIgnoreCase))
            {
                part.layer = Layer.Blink;
            }
            else
            {
                // Track thuộc THÂN nếu hiện ở frame đầu của full_idle VÀ cũng hiện ở anim_idle (frame 4..28);
                // phần còn lại là ĐẦU. Dùng clip "idle" để phân biệt: thân hiện ở đó, đầu thì không.
                PeashooterReanimCache.Clip idleClip;
                bool isBody = PeashooterReanimCache.Clips.TryGetValue("idle", out idleClip) &&
                              track.frames[idleClip.start].visible;
                part.layer = isBody ? Layer.Body : Layer.Head;
            }

            if (track.name.Equals(PeashooterReanimCache.FaceTrackName, StringComparison.OrdinalIgnoreCase))
                facePart = part;

            parts.Add(part);
        }

        if (showShadow && PeashooterReanimCache.AtlasParts.ContainsKey(PeashooterReanimCache.ShadowImageId))
        {
            shadowPart = CreatePart(new PeashooterReanimCache.Track { name = "Shadow", frames = new PeashooterReanimCache.Frame[0] }, 0);
            SetPartUvs(shadowPart, PeashooterReanimCache.AtlasParts[PeashooterReanimCache.ShadowImageId], PeashooterReanimCache.ShadowImageId);

            shadowPart2 = CreatePart(new PeashooterReanimCache.Track { name = "Shadow2", frames = new PeashooterReanimCache.Frame[0] }, 0);
            SetPartUvs(shadowPart2, PeashooterReanimCache.AtlasParts[PeashooterReanimCache.ShadowImageId], PeashooterReanimCache.ShadowImageId);
        }

        ApplyPlantScale();
        ApplySortingOrders();
    }

    private PartRenderer CreatePart(PeashooterReanimCache.Track track, int index)
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

        if (shadowPart2 != null)
        {
            if (!string.IsNullOrEmpty(sortingLayerName)) shadowPart2.renderer.sortingLayerName = sortingLayerName;
            shadowPart2.renderer.sortingOrder = sortingOrderBase - 1;
        }
    }

    /// <summary>Scale các bộ phận của cây quanh gốc (pivot) của object. Bóng không bị scale.</summary>
    private void ApplyPlantScale()
    {
        Vector3 scale = new Vector3(plantScale.x, plantScale.y, 1f);
        foreach (PartRenderer part in parts)
            part.renderer.transform.localScale = scale;
        lastPlantScale = plantScale;
    }

    // ------------------------------------------------------------------
    // Chạy animation
    // ------------------------------------------------------------------

    private void UpdateAnimation(float dt)
    {
        int fps = PeashooterReanimCache.Fps;

        // ----- thân (và đầu khi idle) luôn chạy vòng lặp full_idle -----
        bodyTime += dt * animationSpeed;
        float bodyPos = LoopPosition(fullClip, bodyTime);

        // ----- trạng thái bắn -----
        float shootPos = shootClip.start;
        float attachX = 0f, attachY = 0f;
        if (shooting)
        {
            shootTime += dt * animationSpeed * shootSpeed;
            float shootFrames = shootTime * fps;

            if (!firedThisShot && shootFrames >= fireFrame)
            {
                firedThisShot = true;
                if (logFireEvent) Debug.Log("PvZReanimPeashooter: OnFire (frame " + fireFrame + ")", this);
                if (OnFire != null) OnFire();
            }

            if (shootFrames >= shootClip.Length)
            {
                // bắn xong: đầu quay về idle (sẽ được chuyển mượt)
                shooting = false;
                StartBlend();
                ResetBlinkCountdown();
                if (OnShootFinished != null) OnShootFinished();
            }
            else
            {
                shootPos = shootClip.start + shootFrames;
                SampleAttachOffset(bodyPos, out attachX, out attachY);
            }
        }

        blendTimer = Mathf.Max(0f, blendTimer - dt);
        float blendWeight = blendTime > 0f ? 1f - blendTimer / blendTime : 1f;
        bool blendActive = blendTimer > 0f && blendTime > 0f;

        int ba, bb; float bt;
        FrameIndices(fullClip, bodyPos, out ba, out bb, out bt);
        int sa = 0, sb = 0; float st = 0f;
        if (shooting) FrameIndices(shootClip, shootPos, out sa, out sb, out st);

        // ----- thân + đầu -----
        foreach (PartRenderer part in parts)
        {
            if (part.layer == Layer.Blink) continue;

            Pose pose;
            if (part.layer == Layer.Head && shooting)
            {
                pose = Sample(part.track, sa, sb, st);
                if (pose.visible) { pose.x += attachX; pose.y += attachY; }
            }
            else
            {
                pose = Sample(part.track, ba, bb, bt);
            }

            if (part.layer == Layer.Head && blendActive && part.hasLastPose)
                pose = BlendPose(part.blendFrom, pose, blendWeight);

            part.lastPose = pose;
            part.hasLastPose = true;
            Render(part, pose);
        }

        // ----- chớp mắt -----
        UpdateBlink(dt);
    }

    private void UpdateBlink(float dt)
    {
        PartRenderer blinkPart = null;
        foreach (PartRenderer p in parts)
        {
            if (p.layer == Layer.Blink) { blinkPart = p; break; }
        }
        if (blinkPart == null) return;

        if (!shooting && autoBlink && blinkClip != null)
        {
            if (!blinking)
            {
                blinkCountdown -= dt;
                if (blinkCountdown <= 0f) { blinking = true; blinkTime = 0f; }
            }
        }

        if (blinking && blinkClip != null)
        {
            blinkTime += dt * animationSpeed;
            float pos = blinkClip.start + blinkTime * PeashooterReanimCache.Fps;
            if (pos >= blinkClip.end)
            {
                blinking = false;
                ResetBlinkCountdown();
            }
            else
            {
                int a, b; float t;
                FrameIndices(blinkClip, pos, out a, out b, out t);
                Pose pose = Sample(blinkPart.track, a, b, t);

                // Mắt chớp đứng yên trong file, nhưng đầu thì lắc theo thân -> cho mắt đi theo mặt
                if (facePart != null && facePart.hasLastPose)
                {
                    PeashooterReanimCache.Frame rest = facePart.track.frames[fullClip.start];
                    pose.x += facePart.lastPose.x - rest.x;
                    pose.y += facePart.lastPose.y - rest.y;
                }

                Render(blinkPart, pose);
                return;
            }
        }

        blinkPart.renderer.enabled = false;
    }

    private void ResetBlinkCountdown()
    {
        blinkCountdown = UnityEngine.Random.Range(blinkIntervalMin, Mathf.Max(blinkIntervalMin, blinkIntervalMax));
    }

    private void StartBlend()
    {
        blendTimer = blendTime;
        foreach (PartRenderer part in parts)
        {
            if (part.layer == Layer.Head && part.hasLastPose)
                part.blendFrom = part.lastPose;
        }
    }

    /// <summary>Độ lệch của đầu theo nhịp lắc của thân, nội suy theo vị trí frame của full_idle.</summary>
    private void SampleAttachOffset(float bodyPos, out float ox, out float oy)
    {
        ox = 0f; oy = 0f;
        float[] ax = PeashooterReanimCache.AttachOffsetX;
        float[] ay = PeashooterReanimCache.AttachOffsetY;
        if (ax == null || ay == null) return;

        float k = bodyPos - fullClip.start;
        int i0 = Mathf.Clamp(Mathf.FloorToInt(k), 0, ax.Length - 1);
        int i1 = Mathf.Min(i0 + 1, ax.Length - 1);
        float f = k - Mathf.Floor(k);
        ox = Mathf.Lerp(ax[i0], ax[i1], f);
        oy = Mathf.Lerp(ay[i0], ay[i1], f);
    }

    /// <summary>Vị trí frame trong [start, end). Frame cuối = frame đầu nên không nội suy cuối -> đầu.</summary>
    private static float LoopPosition(PeashooterReanimCache.Clip clip, float time)
    {
        int length = clip.Length;
        if (length <= 0) return clip.start;
        return clip.start + Mathf.Repeat(time * PeashooterReanimCache.Fps, length);
    }

    private static void FrameIndices(PeashooterReanimCache.Clip clip, float position, out int a, out int b, out float t)
    {
        a = Mathf.Clamp(Mathf.FloorToInt(position), clip.start, clip.end);
        b = Mathf.Min(a + 1, clip.end);
        t = position - Mathf.Floor(position);
    }

    private static Pose Sample(PeashooterReanimCache.Track track, int frameA, int frameB, float t)
    {
        PeashooterReanimCache.Frame a = track.frames[frameA];
        PeashooterReanimCache.Frame b = track.frames[frameB];
        return new Pose
        {
            x = Mathf.Lerp(a.x, b.x, t),
            y = Mathf.Lerp(a.y, b.y, t),
            kx = Mathf.Lerp(a.kx, b.kx, t),
            ky = Mathf.Lerp(a.ky, b.ky, t),
            sx = Mathf.Lerp(a.sx, b.sx, t),
            sy = Mathf.Lerp(a.sy, b.sy, t),
            visible = a.visible,
            imageId = a.imageId
        };
    }

    private static Pose BlendPose(Pose from, Pose to, float w)
    {
        if (!from.visible || !to.visible || from.imageId != to.imageId) return to;
        return new Pose
        {
            x = Mathf.Lerp(from.x, to.x, w),
            y = Mathf.Lerp(from.y, to.y, w),
            kx = Mathf.Lerp(from.kx, to.kx, w),
            ky = Mathf.Lerp(from.ky, to.ky, w),
            sx = Mathf.Lerp(from.sx, to.sx, w),
            sy = Mathf.Lerp(from.sy, to.sy, w),
            visible = true,
            imageId = to.imageId
        };
    }

    // ------------------------------------------------------------------
    // Vẽ
    // ------------------------------------------------------------------

    private void Render(PartRenderer part, Pose pose)
    {
        PeashooterReanimCache.AtlasPart atlasPart = null;
        bool hasSprite = pose.visible && !string.IsNullOrEmpty(pose.imageId) &&
                         PeashooterReanimCache.AtlasParts.TryGetValue(pose.imageId, out atlasPart);
        part.renderer.enabled = hasSprite;
        if (!hasSprite) return;

        SetPartUvs(part, atlasPart, pose.imageId);
        ComputeCorners(pose, atlasPart, cornerBuffer);
        for (int i = 0; i < 4; i++)
            part.vertices[i] = ToUnity(cornerBuffer[i].x, cornerBuffer[i].y);

        part.mesh.SetVertices(part.vertices);
        part.mesh.RecalculateBounds();
    }

    private static void SetPartUvs(PartRenderer part, PeashooterReanimCache.AtlasPart atlasPart, string imageId)
    {
        if (part.currentImageId == imageId) return;

        part.uvs[0] = new Vector2(atlasPart.uvMin.x, atlasPart.uvMax.y);
        part.uvs[1] = new Vector2(atlasPart.uvMax.x, atlasPart.uvMax.y);
        part.uvs[2] = new Vector2(atlasPart.uvMax.x, atlasPart.uvMin.y);
        part.uvs[3] = new Vector2(atlasPart.uvMin.x, atlasPart.uvMin.y);
        part.mesh.SetUVs(0, part.uvs);
        part.currentImageId = imageId;
    }

    /// <summary>Cùng công thức ma trận với code zombie: (x,y) + kx/ky (độ) + sx/sy.</summary>
    private void ComputeCorners(Pose pose, PeashooterReanimCache.AtlasPart atlasPart, Vector2[] corners)
    {
        float kx = pose.kx * Mathf.Deg2Rad;
        float ky = pose.ky * Mathf.Deg2Rad;

        float m00 = Mathf.Cos(kx) * pose.sx * globalScale.x;
        float m01 = -Mathf.Sin(ky) * pose.sy * globalScale.x;
        float m10 = Mathf.Sin(kx) * pose.sx * globalScale.y;
        float m11 = Mathf.Cos(ky) * pose.sy * globalScale.y;

        float tx = pose.x * positionScale * globalScale.x;
        float ty = pose.y * positionScale * globalScale.y;
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
    // Bóng: đặt dưới đáy cây ở tư thế nghỉ (frame đầu của full_idle)
    // ------------------------------------------------------------------

    private void RefreshShadow()
    {
        if (shadowPart == null) return;

        float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (PartRenderer part in parts)
        {
            if (part.layer == Layer.Blink) continue;

            Pose pose = Sample(part.track, fullClip.start, fullClip.start, 0f);
            PeashooterReanimCache.AtlasPart atlasPart = null;
            if (!pose.visible || string.IsNullOrEmpty(pose.imageId) ||
                !PeashooterReanimCache.AtlasParts.TryGetValue(pose.imageId, out atlasPart))
                continue;

            ComputeCorners(pose, atlasPart, cornerBuffer);
            for (int i = 0; i < 4; i++)
            {
                minX = Mathf.Min(minX, cornerBuffer[i].x);
                maxX = Mathf.Max(maxX, cornerBuffer[i].x);
                maxY = Mathf.Max(maxY, cornerBuffer[i].y);
            }
        }

        if (minX > maxX)
        {
            shadowPart.renderer.enabled = false;
            if (shadowPart2 != null) shadowPart2.renderer.enabled = false;
            return;
        }

        PeashooterReanimCache.AtlasPart shadow = PeashooterReanimCache.AtlasParts[PeashooterReanimCache.ShadowImageId];
        float halfW = shadow.width * globalScale.x * shadowScale * 0.5f;
        float halfH = shadow.height * globalScale.y * shadowScale * 0.5f;
        // đáy/giữa cây đã nhân plantScale để bóng luôn nằm dưới chân cây; offset cộng sau cùng
        float cx = (minX + maxX) * 0.5f * plantScale.x + shadowOffset.x * positionScale;
        float cy = maxY * plantScale.y + shadowOffset.y * positionScale;

        SetShadowQuad(shadowPart, cx, cy, halfW, halfH);
        if (shadowPart2 != null) SetShadowQuad(shadowPart2, cx, cy, halfW, halfH);
    }

    private void SetShadowQuad(PartRenderer shadow, float cx, float cy, float halfW, float halfH)
    {
        shadow.vertices[0] = ToUnity(cx - halfW, cy - halfH);
        shadow.vertices[1] = ToUnity(cx + halfW, cy - halfH);
        shadow.vertices[2] = ToUnity(cx + halfW, cy + halfH);
        shadow.vertices[3] = ToUnity(cx - halfW, cy + halfH);
        shadow.mesh.SetVertices(shadow.vertices);
        shadow.mesh.RecalculateBounds();
        shadow.renderer.enabled = true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || !ready) return;
        ApplyPlantScale();
        ApplySortingOrders();
        RefreshShadow();

        if (shootNow)
        {
            shootNow = false;
            Shoot();
        }
    }

    private void OnDestroy()
    {
        foreach (PartRenderer part in parts)
            if (part.mesh != null) Destroy(part.mesh);
        if (shadowPart != null && shadowPart.mesh != null) Destroy(shadowPart.mesh);
        if (shadowPart2 != null && shadowPart2.mesh != null) Destroy(shadowPart2.mesh);
    }
}