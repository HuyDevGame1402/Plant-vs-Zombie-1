using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Chạy animation đầu zombie (loadbar_zombiehead) từ file reanim JSON + atlas JSON.
/// Quy ước reanim: gốc toạ độ của mỗi sprite là góc TRÊN-TRÁI, trục Y hướng xuống,
/// góc kx/ky tính bằng độ.
/// </summary>
public class LoadBarZombieHeadAnimation : MonoBehaviour
{
    [Header("Dữ liệu Reanim & Atlas")]
    public TextAsset reanimJson;
    public Texture2D atlasTexture;
    public TextAsset atlasJson;

    [Header("Cài đặt hiển thị")]
    public float pixelsPerUnit = 100f;
    public float positionScale = 2.4f;
    public float spriteScale = 1f;
    public int sortingOrder = 2;

    [Tooltip("Index các track cần vẽ trên cùng (số cuối trong tên GameObject).")]
    public int[] frontTrackIndices = new int[] { };

    [Tooltip("Offset riêng cho các track trong frontTrackIndices.")]
    public Vector2 frontTrackOffsetPx = Vector2.zero;

    [Header("Căn tâm")]
    [Tooltip("Tự dịch đầu zombie sao cho tâm ở frame cuối trùng tâm object này.")]
    public bool centerBodyOnOrigin = true;
    public Vector2 manualOffsetPx = Vector2.zero;

    [Header("Hiệu ứng Offset khi đạt 75% tiến trình (Tăng X từ 9->18)")]
    public bool enableDynamicOffsetAtHalf = true;
    [Range(0f, 1f)] public float triggerProgress = 0.75f;
    [Tooltip("Giá trị X mục tiêu đạt được ở cuối animation (tăng dần từ 9 lên 18).")]
    public float targetOffsetXAtEnd = 18f;
    [Tooltip("Giá trị Y cộng thêm khi animation chạy đến mốc triggerProgress.")]
    public float targetOffsetAtHalf = 6f;

    [Header("Che phần thân nằm dưới mặt đất (tuỳ chọn)")]
    public bool clipBelowGround = false;
    public float groundLineY = 25.5f;
    public float maskWidth = 4f;
    public float maskHeight = 4f;
    [Tooltip("Scale tùy chỉnh riêng cho SpriteMask của đầu zombie.")]
    public Vector3 maskScale = new Vector3(0.913089156f, 1.13680005f, 1f);

    [Header("Phát animation")]
    public bool autoPlay = false;
    public bool loop = false;
    public float speed = 1f;

    public bool testPlay = false;
    [Range(0f, 1f)] public float testProgress = 0f;

    // ---------------------------------------------------------------- data
    public struct ReanimFrame
    {
        public float x, y, sx, sy, kx, ky;
        public bool visible;
    }

    public class ReanimTrack
    {
        public string name;
        public string imageKey;
        public List<ReanimFrame> frames = new List<ReanimFrame>();
    }

    class TrackNode
    {
        public ReanimTrack track;
        public bool followsStem;
        public bool isFront;
        public Transform transform;
        public SpriteRenderer spriteRenderer;
    }

    // Mapping ID ảnh trong reanim của đầu zombie sang atlas key
    static readonly Dictionary<int, string> ImageById = new Dictionary<int, string>
    {
        { 190, "IMAGE_REANIM_LOAD_POTATOMINE_ROCK1" },
        { 191, "IMAGE_REANIM_LOAD_POTATOMINE_ROCK3" },
        { 195, "IMAGE_REANIM_LOAD_ZOMBIE_HAIR" },
        { 196, "IMAGE_REANIM_LOAD_ZOMBIE_HEAD" },
        { 197, "IMAGE_REANIM_LOAD_ZOMBIE_JAW" },
    };

    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    readonly List<ReanimTrack> tracks = new List<ReanimTrack>();
    readonly List<TrackNode> nodes = new List<TrackNode>();

    int fps = 12;
    int frameCount = 1;
    float time;
    bool playing;

    public float Duration => frameCount > 1 ? (frameCount - 1) / (float)fps : 0f;
    private float baseManualOffsetX;
    private float baseManualOffsetY;

    void Awake()
    {
        baseManualOffsetX = manualOffsetPx.x;
        baseManualOffsetY = manualOffsetPx.y;

        if (atlasTexture != null && atlasJson != null) BuildSprites();

        if (reanimJson != null)
        {
            ParseReanimData();
            ComputeAutoOffset();
            if (clipBelowGround) BuildGroundMask(out maskObj);
            BuildGameObjectHierarchy();

            float currentProg = testPlay ? testProgress : 0f;
            ApplyDynamicOffset(currentProg);
            SampleAnimation(currentProg);
        }
    }

    void Start()
    {
        if (autoPlay) Play();
    }

    void Update()
    {
        if (nodes.Count == 0) return;

        if (testPlay)
        {
            ApplyDynamicOffset(testProgress);
            SampleAnimation(testProgress);
            return;
        }

        if (!playing || Duration <= 0f) return;

        time += Time.deltaTime * speed;
        if (time >= Duration)
        {
            if (loop)
            {
                time %= Duration;
            }
            else
            {
                time = Duration;
                playing = false;
            }
        }

        float normalizedProgress = time / Duration;
        ApplyDynamicOffset(normalizedProgress);
        SampleAnimation(normalizedProgress);
    }

    public void Play()
    {
        time = 0f;
        playing = true;
    }

    // Xử lý tự động tăng X từ 9 lên 18 trong khoảng từ triggerProgress (0.75) đến 1.0
    void ApplyDynamicOffset(float progress)
    {
        if (!enableDynamicOffsetAtHalf)
        {
            manualOffsetPx.x = baseManualOffsetX;
            manualOffsetPx.y = baseManualOffsetY;
            return;
        }

        // Xử lý trục Y (nếu bạn vẫn muốn giữ logic cũ)
        if (progress <= triggerProgress)
        {
            float tY = triggerProgress > 0f ? (progress / triggerProgress) : 1f;
            manualOffsetPx.y = baseManualOffsetY + Mathf.Lerp(0f, targetOffsetAtHalf, tY);
        }
        else
        {
            manualOffsetPx.y = baseManualOffsetY + targetOffsetAtHalf;
        }

        // Xử lý trục X tăng dần từ 9 lên 18 trong 25% thời gian cuối (từ triggerProgress đến 1.0)
        if (progress < triggerProgress)
        {
            manualOffsetPx.x = baseManualOffsetX; // Giữ nguyên giá trị gốc (vd: 9) trước mốc 75%
        }
        else
        {
            float tX = (1f - triggerProgress) > 0f ? (progress - triggerProgress) / (1f - triggerProgress) : 1f;
            manualOffsetPx.x = Mathf.Lerp(baseManualOffsetX, targetOffsetXAtEnd, Mathf.Clamp01(tX));
        }
    }

    void BuildSprites()
    {
        atlasTexture.wrapMode = TextureWrapMode.Clamp;

        var root = JObject.Parse(atlasJson.text);
        foreach (var r in (JArray)root["resources"])
        {
            var rect = r["atlasRect"];
            if (rect == null) continue;

            string id = ((string)r["attributes"]["id"]).Split('|')[0].ToUpperInvariant();
            float x = (float)rect["x"], y = (float)rect["y"];
            float w = (float)rect["width"], h = (float)rect["height"];

            var uRect = new Rect(x, atlasTexture.height - y - h, w, h);

            sprites[id] = Sprite.Create(atlasTexture, uRect, new Vector2(0f, 1f),
                                        pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    void ParseReanimData()
    {
        tracks.Clear();
        var root = JObject.Parse(reanimJson.text);
        fps = (int)(root["fps"] ?? 12);
        frameCount = 1;

        foreach (JObject t in (JArray)root["tracks"])
        {
            var track = new ReanimTrack { name = (string)t["name"] };

            var cur = new ReanimFrame { sx = 1f, sy = 1f, visible = true };
            bool first = true;

            foreach (JObject f in (JArray)t["transforms"])
            {
                if (f["x"] != null) cur.x = (float)f["x"];
                if (f["y"] != null) cur.y = (float)f["y"];
                if (f["sx"] != null) cur.sx = (float)f["sx"];
                if (f["sy"] != null) cur.sy = (float)f["sy"];
                if (f["kx"] != null) cur.kx = (float)f["kx"];
                if (f["ky"] != null) cur.ky = (float)f["ky"];

                if (f["f"] != null) cur.visible = (int)f["f"] != -1;
                else if (first) cur.visible = true;
                first = false;

                if (f["i"] != null && track.imageKey == null)
                {
                    int imgId = (int)f["i"];
                    if (ImageById.TryGetValue(imgId, out var key)) track.imageKey = key;
                }

                track.frames.Add(cur);
            }

            if (track.imageKey == null) track.imageKey = FallbackKeyByName(track.name);
            frameCount = Mathf.Max(frameCount, track.frames.Count);
            tracks.Add(track);
        }
    }

    float autoOffsetXPx;
    Vector2 TotalOffsetPx => new Vector2(autoOffsetXPx + manualOffsetPx.x, manualOffsetPx.y);

    void ComputeAutoOffset()
    {
        autoOffsetXPx = 0f;
        if (!centerBodyOnOrigin) return;

        foreach (var track in tracks)
        {
            if (track.name != "Zombie_head" || track.frames.Count == 0) continue;
            if (!sprites.TryGetValue(track.imageKey, out var sprite)) return;

            ReanimFrame last = track.frames[track.frames.Count - 1];
            float widthPx = sprite.rect.width * last.sx * spriteScale;
            float centerPx = last.x * positionScale + widthPx * 0.5f;
            autoOffsetXPx = -centerPx;
            return;
        }
    }

    static string FallbackKeyByName(string trackName)
    {
        string lower = trackName.ToLowerInvariant();
        if (lower.Contains("zombie_head")) return "IMAGE_REANIM_LOAD_ZOMBIE_HEAD";
        if (lower.Contains("zombie_hair")) return "IMAGE_REANIM_LOAD_ZOMBIE_HAIR";
        if (lower.Contains("zombie_jaw")) return "IMAGE_REANIM_LOAD_ZOMBIE_JAW";
        if (lower.Contains("rock")) return "IMAGE_REANIM_LOAD_POTATOMINE_ROCK3";
        return "";
    }

    GameObject maskObj;

    void BuildGroundMask(out GameObject go)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();

        var sp = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);

        go = new GameObject("GroundMask");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -groundLineY * positionScale / pixelsPerUnit, 0f);
        // Gán trực tiếp giá trị maskScale từ Inspector
        go.transform.localScale = maskScale;
        go.AddComponent<SpriteMask>().sprite = sp;
    }

    void BuildGameObjectHierarchy()
    {
        nodes.Clear();

        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track.name.StartsWith("anim_")) continue;

            sprites.TryGetValue(track.imageKey, out var sprite);

            var child = new GameObject($"{track.name}_{i}");
            child.transform.SetParent(transform, false);

            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder + i;

            if (System.Array.IndexOf(frontTrackIndices, i) >= 0)
                sr.sortingOrder = sortingOrder + tracks.Count + 1;

            if (clipBelowGround && maskObj != null && track.name.StartsWith("Zombie"))
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            nodes.Add(new TrackNode
            {
                track = track,
                followsStem = track.name.StartsWith("Zombie"),
                isFront = System.Array.IndexOf(frontTrackIndices, i) >= 0,
                transform = child.transform,
                spriteRenderer = sr
            });
        }
    }

    public void SampleAnimation(float normalizedTime)
    {
        foreach (var node in nodes)
        {
            ReanimFrame f = SampleTrack(node.track, normalizedTime);

            node.spriteRenderer.enabled = f.visible && node.spriteRenderer.sprite != null;
            if (!f.visible) continue;

            Vector2 off = node.followsStem ? TotalOffsetPx : Vector2.zero;
            if (node.isFront) off += frontTrackOffsetPx;
            float posX = f.x * positionScale + off.x;
            float posY = -f.y * positionScale + off.y;
            node.transform.localPosition = new Vector3(posX, posY, 0f) / pixelsPerUnit;

            bool flipY = Mathf.Cos((f.ky - f.kx) * Mathf.Deg2Rad) < 0f;

            node.transform.localScale = new Vector3(
                f.sx * spriteScale,
                (flipY ? -f.sy : f.sy) * spriteScale,
                1f);

            node.transform.localRotation = Quaternion.Euler(0f, 0f, -f.kx);
        }
    }

    ReanimFrame SampleTrack(ReanimTrack track, float normalizedTime)
    {
        if (track == null || track.frames.Count == 0) return default;

        float fi = Mathf.Clamp01(normalizedTime) * (track.frames.Count - 1);
        int i0 = Mathf.FloorToInt(fi);
        int i1 = Mathf.Min(i0 + 1, track.frames.Count - 1);
        float k = fi - i0;

        ReanimFrame a = track.frames[i0], b = track.frames[i1];

        if (!a.visible) return a;
        if (!b.visible) return a;

        return new ReanimFrame
        {
            x = Mathf.Lerp(a.x, b.x, k),
            y = Mathf.Lerp(a.y, b.y, k),
            sx = Mathf.Lerp(a.sx, b.sx, k),
            sy = Mathf.Lerp(a.sy, b.sy, k),
            kx = Mathf.Lerp(a.kx, b.kx, k),
            ky = Mathf.Lerp(a.ky, b.ky, k),
            visible = true
        };
    }
}