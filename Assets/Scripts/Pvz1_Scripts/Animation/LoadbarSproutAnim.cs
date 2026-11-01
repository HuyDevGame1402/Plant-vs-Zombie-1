using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Chạy animation mầm hoa (loadbar_sprout) từ file reanim JSON + atlas JSON.
/// Quy ước reanim: gốc toạ độ của mỗi sprite là góc TRÊN-TRÁI, trục Y hướng xuống,
/// góc kx/ky tính bằng độ.
/// </summary>
public class LoadBarSproutAnimation : MonoBehaviour
{
    [Header("Dữ liệu Reanim & Atlas")]
    public TextAsset reanimJson;
    public Texture2D atlasTexture;
    public TextAsset atlasJson;

    [Header("Cài đặt hiển thị")]
    public float pixelsPerUnit = 100f;

    [Tooltip("Tỉ lệ đổi toạ độ reanim -> pixel của atlas. " +
             "Atlas 960 có sprout_body cao 64px, trong reanim chỉ ~27 đơn vị => ~2.4 (1536/640). " +
             "Nếu gốc thân vẫn lún/nhô dần, thử chỉnh trong khoảng 2.25 - 2.4.")]
    public float positionScale = 2.4f;

    [Tooltip("Nhân thêm vào scale của sprite. Giữ 1 nếu atlas đúng độ phân giải.")]
    public float spriteScale = 1f;

    public int sortingOrder = 2;

    [Tooltip("Index các track cần vẽ trên cùng (số cuối trong tên GameObject, vd sprout_petal_9 => 9).")]
    public int[] frontTrackIndices = new int[] { 9 };

    [Tooltip("Offset riêng cho các track trong frontTrackIndices (sprout_petal_9), cộng thêm vào offset chung. " +
             "Pixel atlas: X dương = sang phải, Y dương = lên trên.")]
    public Vector2 frontTrackOffsetPx = Vector2.zero;

    [Header("Căn tâm")]
    [Tooltip("Tự dịch thân + 3 lá sao cho tâm thân (ở frame cuối) trùng tâm object này. Đất đá không bị dịch.")]
    public bool centerBodyOnOrigin = true;
    [Tooltip("Chỉnh tay thêm CHỈ cho thân + lá (pixel atlas). X âm = sang trái, X dương = sang phải, Y dương = lên trên.")]
    public Vector2 manualOffsetPx = Vector2.zero;

    [Header("Hiệu ứng Offset Y khi đạt 50% tiến trình")]
    [Tooltip("Bật tính năng thay đổi manualOffsetPx.y từ 0 lên targetOffsetAtHalf khi progress đạt triggerProgress.")]
    public bool enableDynamicOffsetAtHalf = true;
    [Range(0f, 1f)] public float triggerProgress = 0.5f;
    [Tooltip("Giá trị Y cộng thêm khi animation chạy đến mốc triggerProgress.")]
    public float targetOffsetAtHalf = 6f;

    [Header("Che phần thân nằm dưới mặt đất (tuỳ chọn)")]
    [Tooltip("Cắt phần sprout_body nằm dưới đường đất bằng SpriteMask.")]
    public bool clipBelowGround = false;
    [Tooltip("Vị trí đường đất, tính theo đơn vị reanim (trục Y hướng xuống).")]
    public float groundLineY = 25.5f;
    public float maskWidth = 4f;   // đơn vị world
    public float maskHeight = 4f;  // đơn vị world
    [Tooltip("Scale tùy chỉnh riêng cho SpriteMask (ví dụ: thu nhỏ X, Y để tránh tràn viền).")]
    public Vector3 maskScale = new Vector3(0.664f, 0.952f, 1f);

    [Header("Phát animation")]
    public bool autoPlay = false;
    public bool loop = false;
    public float speed = 1f;

    [Tooltip("Tick để test trực tiếp bằng thanh testProgress")]
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
        public string imageKey;               // key sprite trong atlas
        public List<ReanimFrame> frames = new List<ReanimFrame>();
    }

    class TrackNode
    {
        public ReanimTrack track;
        public bool followsStem;   // true: thân + lá (chịu offset), false: đất đá (đứng yên)
        public bool isFront;       // track vẽ trên cùng, có thêm frontTrackOffsetPx
        public Transform transform;
        public SpriteRenderer spriteRenderer;
    }

    // id ảnh trong reanim ("i") -> id trong atlas
    static readonly Dictionary<int, string> ImageById = new Dictionary<int, string>
    {
        { 190, "IMAGE_REANIM_LOAD_POTATOMINE_ROCK1" },
        { 191, "IMAGE_REANIM_LOAD_POTATOMINE_ROCK3" },
        { 193, "IMAGE_REANIM_LOAD_SPROUT_BODY" },
        { 194, "IMAGE_REANIM_LOAD_SPROUT_PETAL" },
    };

    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    readonly List<ReanimTrack> tracks = new List<ReanimTrack>();
    readonly List<TrackNode> nodes = new List<TrackNode>();

    int fps = 12;
    int frameCount = 1;
    float time;
    bool playing;

    public float Duration => frameCount > 1 ? (frameCount - 1) / (float)fps : 0f;

    // Lưu lại manualOffsetPx.y ban đầu để tính toán cộng dồn chính xác
    private float baseManualOffsetY;

    // ---------------------------------------------------------------- lifecycle
    void Awake()
    {
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

    // Xử lý tự động tăng giá trị manual offset Y từ 0 lên targetOffsetAtHalf khi đạt mốc triggerProgress
    void ApplyDynamicOffset(float progress)
    {
        if (!enableDynamicOffsetAtHalf)
        {
            manualOffsetPx.y = baseManualOffsetY;
            return;
        }

        if (progress <= triggerProgress)
        {
            // Từ 0 đến triggerProgress (vd: 0 -> 0.5), offset Y tăng dần từ 0 lên targetOffsetAtHalf
            float t = triggerProgress > 0f ? (progress / triggerProgress) : 1f;
            manualOffsetPx.y = baseManualOffsetY + Mathf.Lerp(0f, targetOffsetAtHalf, t);
        }
        else
        {
            // Sau mốc triggerProgress (vd: 0.5 -> 1), giữ nguyên giá trị targetOffsetAtHalf đã đạt được
            manualOffsetPx.y = baseManualOffsetY + targetOffsetAtHalf;
        }
    }

    // ---------------------------------------------------------------- build
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

            // Atlas tính Y từ trên xuống, Unity tính từ dưới lên
            var uRect = new Rect(x, atlasTexture.height - y - h, w, h);

            // Pivot góc trên-trái để khớp hệ toạ độ reanim
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

            // Frame rỗng {} nghĩa là "giữ nguyên giá trị frame trước"
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

    // Dịch ngang (px) để tâm thân cây ở frame cuối nằm đúng x = 0 của object
    float autoOffsetXPx;

    Vector2 TotalOffsetPx => new Vector2(autoOffsetXPx + manualOffsetPx.x, manualOffsetPx.y);

    void ComputeAutoOffset()
    {
        autoOffsetXPx = 0f;
        if (!centerBodyOnOrigin) return;

        foreach (var track in tracks)
        {
            if (track.name != "sprout" || track.frames.Count == 0) continue;
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
        if (lower.Contains("sprout_petal")) return "IMAGE_REANIM_LOAD_SPROUT_PETAL";
        if (lower.Contains("sprout")) return "IMAGE_REANIM_LOAD_SPROUT_BODY";
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

        // 4x4 px, PPU 4 => đúng 1x1 unit, pivot giữa-đáy
        var sp = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);

        go = new GameObject("GroundMask");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -groundLineY * positionScale / pixelsPerUnit, 0f);
        // Gán thẳng trực tiếp giá trị scale từ Inspector:
        go.transform.localScale = maskScale;
        go.AddComponent<SpriteMask>().sprite = sp;
    }

    void BuildGameObjectHierarchy()
    {
        nodes.Clear();

        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track.name.StartsWith("anim_")) continue;   // track điều khiển, không có sprite

            sprites.TryGetValue(track.imageKey, out var sprite);

            var child = new GameObject($"{track.name}_{i}");
            child.transform.SetParent(transform, false);

            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // Track nằm sau trong file thì vẽ đè lên track trước
            sr.sortingOrder = sortingOrder + i;

            // Track được chỉ định thì vẽ trên cùng, cao hơn mọi track khác
            if (System.Array.IndexOf(frontTrackIndices, i) >= 0)
                sr.sortingOrder = sortingOrder + tracks.Count + 1;

            if (clipBelowGround && maskObj != null && track.name == "sprout")
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            nodes.Add(new TrackNode
            {
                track = track,
                followsStem = track.name.StartsWith("sprout"),   // sprout + sprout_petal
                isFront = System.Array.IndexOf(frontTrackIndices, i) >= 0,
                transform = child.transform,
                spriteRenderer = sr
            });
        }
    }

    // ---------------------------------------------------------------- sample
    public void SampleAnimation(float normalizedTime)
    {
        foreach (var node in nodes)
        {
            ReanimFrame f = SampleTrack(node.track, normalizedTime);

            node.spriteRenderer.enabled = f.visible && node.spriteRenderer.sprite != null;
            if (!f.visible) continue;

            // Toạ độ reanim: Y hướng xuống -> đảo dấu cho Unity
            Vector2 off = node.followsStem ? TotalOffsetPx : Vector2.zero;
            if (node.isFront) off += frontTrackOffsetPx;
            float posX = f.x * positionScale + off.x;
            float posY = -f.y * positionScale + off.y;
            node.transform.localPosition = new Vector3(posX, posY, 0f) / pixelsPerUnit;

            // kx = góc trục X, ky = góc trục Y. Nếu hai trục lệch nhau > 90°
            // (ví dụ kx=135, ky=-45) thì sprite bị lật theo trục Y.
            bool flipY = Mathf.Cos((f.ky - f.kx) * Mathf.Deg2Rad) < 0f;

            node.transform.localScale = new Vector3(
                f.sx * spriteScale,
                (flipY ? -f.sy : f.sy) * spriteScale,
                1f);

            // Phần skew nhỏ (vài độ giữa kx và ky) được bỏ qua vì Transform không hỗ trợ skew
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

        // Frame đang ẩn thì không nội suy sang frame hiện (tránh sprite bay từ giá trị mặc định)
        if (!a.visible) return a;

        // Chuyển từ hiện -> ẩn: giữ nguyên frame a cho tới hết đoạn
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