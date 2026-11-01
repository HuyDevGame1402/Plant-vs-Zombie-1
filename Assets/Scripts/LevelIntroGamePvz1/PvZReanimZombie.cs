//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using Newtonsoft.Json.Linq;
//using UnityEngine;

///// <summary>
///// Quản lý dữ liệu Reanim dùng chung để tránh parse JSON nhiều lần gây đơ máy.
///// </summary>
//public static class ReanimDataCache
//{
//    public sealed class AtlasPart
//    {
//        public float width;
//        public float height;
//        public Vector2 uvMin;
//        public Vector2 uvMax;
//    }

//    public sealed class ReanimFrame
//    {
//        public float x;
//        public float y;
//        public float kx;
//        public float ky;
//        public float sx = 1f;
//        public float sy = 1f;
//        public bool visible = true;
//        public string imageId;
//    }

//    public sealed class ReanimTrack
//    {
//        public string name;
//        public bool hasImage;
//        public ReanimFrame[] frames;
//    }

//    /// <summary>Một animation (idle, walk, eat, death...) = khoảng frame trong file reanim.</summary>
//    public sealed class ReanimClip
//    {
//        public string name;
//        public int start;
//        public int end;
//        public int Length { get { return end - start; } }
//    }

//    // Cache static
//    public static Dictionary<string, AtlasPart> AtlasParts = new Dictionary<string, AtlasPart>();
//    public static List<ReanimTrack> CachedTracks = new List<ReanimTrack>();
//    public static Dictionary<string, ReanimClip> Clips = new Dictionary<string, ReanimClip>(StringComparer.OrdinalIgnoreCase);
//    public static int IdleFrameCount = 0;
//    public static int Fps = 0;
//    public static int ImageIndexOffset = 0;
//    private static bool isInitialized = false;

//    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
//    private static void ResetStatics()
//    {
//        AtlasParts = new Dictionary<string, AtlasPart>();
//        CachedTracks = new List<ReanimTrack>();
//        Clips = new Dictionary<string, ReanimClip>(StringComparer.OrdinalIgnoreCase);
//        IdleFrameCount = 0;
//        Fps = 0;
//        ImageIndexOffset = 0;
//        isInitialized = false;
//    }

//    public static bool Initialize(TextAsset atlasJsonAsset, TextAsset reanimJsonAsset, HashSet<string> baseZombiePartIds, string shadowImageId)
//    {
//        if (isInitialized) return true;

//        try
//        {
//            // 1. Parse Atlas
//            JObject atlasRoot = JObject.Parse(atlasJsonAsset.text);
//            JArray atlasResources = atlasRoot["resources"] as JArray;
//            if (atlasResources == null) return false;

//            float atlasWidth = 960f; // Mặc định atlas 960
//            float atlasHeight = 960f;
//            foreach (JToken token in atlasResources)
//            {
//                JObject attributes = token["attributes"] as JObject;
//                string rawId = attributes != null ? attributes.Value<string>("id") : null;
//                if (string.IsNullOrEmpty(rawId) || !rawId.StartsWith("ATLASIMAGE_", StringComparison.OrdinalIgnoreCase))
//                    continue;

//                if (TryParseFloat(attributes.Value<string>("aw"), out float w) &&
//                    TryParseFloat(attributes.Value<string>("ah"), out float h) && w > 0f && h > 0f)
//                {
//                    atlasWidth = w;
//                    atlasHeight = h;
//                }
//                break;
//            }

//            Dictionary<int, string> idsByIndex = new Dictionary<int, string>();
//            for (int i = 0; i < atlasResources.Count; i++)
//            {
//                JObject resource = atlasResources[i] as JObject;
//                JObject attributes = resource != null ? resource["attributes"] as JObject : null;
//                JObject atlasRect = resource != null ? resource["atlasRect"] as JObject : null;
//                string rawId = attributes != null ? attributes.Value<string>("id") : null;
//                if (string.IsNullOrEmpty(rawId)) continue;

//                string id = NormalizeImageId(rawId);
//                idsByIndex[i] = id;
//                if ((!baseZombiePartIds.Contains(id) && id != shadowImageId) || atlasRect == null)
//                    continue;

//                float x = atlasRect.Value<float>("x");
//                float y = atlasRect.Value<float>("y");
//                float width = atlasRect.Value<float>("width");
//                float height = atlasRect.Value<float>("height");
//                if (width <= 0f || height <= 0f) continue;

//                AtlasParts[id] = new AtlasPart
//                {
//                    width = width,
//                    height = height,
//                    uvMin = new Vector2(x / atlasWidth, 1f - (y + height) / atlasHeight),
//                    uvMax = new Vector2((x + width) / atlasWidth, 1f - y / atlasHeight)
//                };
//            }

//            // 2. Parse Reanim JSON
//            JObject reanimRoot = JObject.Parse(reanimJsonAsset.text);
//            JArray reanimTracks = reanimRoot["tracks"] as JArray;
//            if (reanimTracks == null) return false;

//            Fps = reanimRoot["fps"] != null ? reanimRoot["fps"].Value<int>() : 30;

//            // Tìm Image Index Offset
//            ImageIndexOffset = GetImageIndexOffset(atlasResources, reanimTracks);

//            // Parse TOÀN BỘ frame của tất cả các track (không chỉ idle nữa)
//            foreach (JObject sourceTrack in reanimTracks)
//            {
//                JArray sourceFrames = sourceTrack["transforms"] as JArray;
//                if (sourceFrames == null || sourceFrames.Count < 2) continue;

//                ReanimTrack track = ParseTrack(sourceTrack, sourceFrames, idsByIndex, ImageIndexOffset);

//                // Track không có ảnh và tên bắt đầu bằng "anim_" chính là track đánh dấu animation (clip)
//                if (!track.hasImage && track.name != null && track.name.StartsWith("anim_", StringComparison.OrdinalIgnoreCase))
//                {
//                    ReanimClip clip = BuildClip(track);
//                    if (clip != null) Clips[clip.name] = clip;
//                }
//                else if (track.hasImage)
//                {
//                    CachedTracks.Add(track);
//                }
//            }

//            ReanimClip idle;
//            if (!Clips.TryGetValue("idle", out idle) || idle.Length < 2) return false;
//            IdleFrameCount = idle.Length + 1;

//            isInitialized = true;
//            return true;
//        }
//        catch (Exception e)
//        {
//            Debug.LogError("Lỗi khởi tạo ReanimCache: " + e.Message);
//            return false;
//        }
//    }

//    /// <summary>Clip = đoạn frame đầu tiên mà track "anim_xxx" hiển thị (f != -1).</summary>
//    private static ReanimClip BuildClip(ReanimTrack track)
//    {
//        int start = -1;
//        int end = -1;
//        for (int i = 0; i < track.frames.Length; i++)
//        {
//            if (track.frames[i].visible)
//            {
//                if (start < 0) start = i;
//                end = i;
//            }
//            else if (start >= 0)
//            {
//                break;
//            }
//        }
//        if (start < 0) return null;
//        return new ReanimClip { name = track.name.Substring(5), start = start, end = end };
//    }

//    private static int GetImageIndexOffset(JArray atlasResources, JArray reanimTracks)
//    {
//        int bodyAtlasIndex = -1;
//        for (int i = 0; i < atlasResources.Count; i++)
//        {
//            string id = atlasResources[i]?["attributes"]?["id"]?.Value<string>();
//            if (NormalizeImageId(id) == "IMAGE_REANIM_ZOMBIE_BODY")
//            {
//                bodyAtlasIndex = i;
//                break;
//            }
//        }

//        JObject bodyTrack = FindTrack(reanimTracks, "Zombie_body");
//        JArray bodyFrames = bodyTrack?["transforms"] as JArray;
//        if (bodyAtlasIndex < 0 || bodyFrames == null) return 0;

//        foreach (JObject frame in bodyFrames)
//        {
//            if (frame["i"] != null)
//                return frame["i"].Value<int>() - bodyAtlasIndex;
//        }
//        return 0;
//    }

//    private static ReanimTrack ParseTrack(JObject sourceTrack, JArray sourceFrames, Dictionary<int, string> idsByIndex, int imageIndexOffset)
//    {
//        int frameCount = sourceFrames.Count;
//        ReanimTrack track = new ReanimTrack
//        {
//            name = sourceTrack.Value<string>("name"),
//            frames = new ReanimFrame[frameCount]
//        };
//        ReanimFrame current = new ReanimFrame();

//        for (int i = 0; i < frameCount; i++)
//        {
//            JObject frame = sourceFrames[i] as JObject;
//            if (frame != null)
//            {
//                UpdateFloat(frame, "x", ref current.x);
//                UpdateFloat(frame, "y", ref current.y);
//                UpdateFloat(frame, "kx", ref current.kx);
//                UpdateFloat(frame, "ky", ref current.ky);
//                UpdateFloat(frame, "sx", ref current.sx);
//                UpdateFloat(frame, "sy", ref current.sy);

//                if (frame["f"] != null)
//                    current.visible = frame["f"].Value<int>() != -1;

//                if (frame["i"] != null)
//                {
//                    track.hasImage = true;
//                    int atlasIndex = frame["i"].Value<int>() - imageIndexOffset;
//                    string imageId;
//                    current.imageId = idsByIndex.TryGetValue(atlasIndex, out imageId) ? imageId : null;
//                }
//            }

//            track.frames[i] = new ReanimFrame
//            {
//                x = current.x, y = current.y,
//                kx = current.kx, ky = current.ky,
//                sx = current.sx, sy = current.sy,
//                visible = current.visible,
//                imageId = current.imageId
//            };
//        }
//        return track;
//    }

//    private static void UpdateFloat(JObject frame, string propertyName, ref float value)
//    {
//        if (frame[propertyName] != null) value = frame[propertyName].Value<float>();
//    }

//    private static bool TryParseFloat(string text, out float value)
//    {
//        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
//    }

//    private static JObject FindTrack(JArray tracks, string trackName)
//    {
//        foreach (JObject track in tracks)
//        {
//            if (track.Value<string>("name") == trackName) return track;
//        }
//        return null;
//    }

//    private static string NormalizeImageId(string imageId)
//    {
//        return string.IsNullOrEmpty(imageId) ? string.Empty : imageId.Split('|')[0].Trim().ToUpperInvariant();
//    }
//}

///// <summary>Mảnh rơi (đầu bị bật ra, tay rụng): bay theo vòng cung, xoay, mờ dần rồi tự huỷ.</summary>
//public class ReanimDebris : MonoBehaviour
//{
//    private Vector3 velocity;
//    private float gravity;
//    private float angularVelocity;
//    private float lifetime;
//    private float fadeTime;
//    private float age;
//    private Mesh[] meshes;
//    private static readonly Color32[] colorBuffer = new Color32[4];

//    public void Init(Vector3 velocity, float gravity, float angularVelocity, float lifetime, float fadeTime, Mesh[] meshes)
//    {
//        this.velocity = velocity;
//        this.gravity = gravity;
//        this.angularVelocity = angularVelocity;
//        this.lifetime = lifetime;
//        this.fadeTime = Mathf.Max(0.01f, fadeTime);
//        this.meshes = meshes;
//    }

//    private void Update()
//    {
//        float dt = Time.deltaTime;
//        age += dt;

//        velocity.y -= gravity * dt;
//        transform.localPosition += velocity * dt;
//        transform.Rotate(0f, 0f, angularVelocity * dt, Space.Self);

//        if (age >= lifetime)
//        {
//            Destroy(gameObject);
//            return;
//        }

//        if (age > lifetime - fadeTime && meshes != null)
//        {
//            byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((lifetime - age) / fadeTime));
//            for (int i = 0; i < 4; i++) colorBuffer[i] = new Color32(255, 255, 255, a);
//            foreach (Mesh mesh in meshes)
//                if (mesh != null) mesh.colors32 = colorBuffer;
//        }
//    }

//    private void OnDestroy()
//    {
//        if (meshes == null) return;
//        foreach (Mesh mesh in meshes)
//            if (mesh != null) Destroy(mesh);
//    }
//}

//public class PvZReanimZombie : MonoBehaviour
//{
//    public enum ZombieState { Idle, Walk, Eat, Dying, Dead }

//    [Header("Cấu hình Hàng (Lane)")]
//    public ZombieLayerManager.ZombieLane currentLane = ZombieLayerManager.ZombieLane.Lane1;

//    [Header("Tài nguyên")]
//    public Texture2D atlasTexture;
//    public TextAsset atlasJsonAsset;
//    public TextAsset reanimJsonAsset;

//    [Header("Hiển thị")]
//    public float pixelPerUnit = 100f;
//    public float animationSpeed = 1f;
//    public Vector2 globalScale = Vector2.one;
//    public Vector2 shadowOffset = Vector2.zero;
//    [Tooltip("Bật: điểm gốc (pivot) của GameObject nằm đúng giữa 2 bàn chân zombie, nên zombie hiện ngay tại vị trí GameObject. Tắt: dùng gốc toạ độ gốc của file reanim (zombie bị lệch).")]
//    public bool anchorToFeet = true;
//    [Tooltip("Tinh chỉnh thêm vị trí hiển thị so với GameObject (đơn vị Unity).")]
//    public Vector2 pivotOffset = Vector2.zero;

//    [Header("Animation")]
//    [Tooltip("Trạng thái lúc bắt đầu (Idle / Walk / Eat).")]
//    public ZombieState initialState = ZombieState.Idle;
//    [Tooltip("Tên clip trong file reanim (bỏ chữ anim_): idle, idle2, walk, walk2, eat, death, death2, superlongdeath...")]
//    public string walkClip = "walk";
//    [Tooltip("Kiểu đi bộ thứ 2 (game gốc cho zombie đi 1 trong 2 kiểu để đỡ giống nhau).")]
//    public string walkClip2 = "walk2";
//    [Tooltip("Bật: mỗi zombie tự chọn ngẫu nhiên 1 trong 2 kiểu đi khi xuất hiện. Tắt: luôn dùng walkClip.")]
//    public bool randomWalkVariant = true;
//    [Tooltip("Tuỳ chọn: clip đi riêng khi đã mất tay. Để trống = giữ nguyên kiểu đi đang dùng (giống game gốc).")]
//    public string walkLostArmClip = "";
//    public string eatClip = "eat";
//    [Tooltip("Chết khi còn đầu (đầu dính theo thân rồi ngã).")]
//    public string deathClip = "death";
//    [Tooltip("Chết khi mất đầu (đầu bật ra, thân ngã không đầu).")]
//    public string deathHeadlessClip = "death2";
//    [Tooltip("Thời gian chuyển mượt giữa 2 animation (giây).")]
//    public float blendTime = 0.12f;
//    [Tooltip("Tự dịch chuyển sang trái khi đang Walk. Tắt nếu script khác của bạn đã lo việc di chuyển.")]
//    public bool moveWhileWalking = false;
//    public float walkSpeed = 0.3f;

//    [Header("Sau khi chết")]
//    public bool fadeAfterDeath = true;
//    public float lingerTime = 1.5f;
//    public float fadeDuration = 1f;
//    public bool destroyAfterFade = true;

//    [Header("Mảnh rơi (đầu / tay)")]
//    public float debrisGravity = 9f;

//    [Header("Máu (tuỳ chọn, dùng qua TakeDamage)")]
//    public float maxHealth = 270f;
//    [Tooltip("Máu còn lại ≤ mức này thì rụng tay.")]
//    public float armLostHealth = 135f;

//    [Header("Test trong Editor (tick khi đang Play)")]
//    public bool testIdle;
//    public bool testWalk;
//    [Tooltip("Ép đi theo kiểu thứ 2 (walkClip2).")]
//    public bool testWalk2;
//    public bool testEat;
//    public bool testLoseArm;
//    public bool testDie;
//    public bool testDieHeadless;
//    public bool testReset;

//    public event Action OnArmLost;
//    public event Action OnDeathStarted;
//    public event Action OnDeathFinished;

//    public ZombieState State { get { return state; } }
//    public bool HasArm { get { return !armLost; } }
//    public bool IsDeadOrDying { get { return state == ZombieState.Dying || state == ZombieState.Dead; } }

//    private const float positionScale = 2f;
//    private const string shadowImageId = "IMAGE_PLANTSHADOW";
//    private const string armUpper2ImageId = "IMAGE_REANIM_ZOMBIE_OUTERARM_UPPER2";

//    private static readonly HashSet<string> baseZombiePartIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
//    {
//        "IMAGE_REANIM_ZOMBIE_NECK", "IMAGE_REANIM_ZOMBIE_BODY", "IMAGE_REANIM_ZOMBIE_HEAD",
//        "IMAGE_REANIM_ZOMBIE_JAW", "IMAGE_REANIM_ZOMBIE_INNERARM_HAND", "IMAGE_REANIM_ZOMBIE_INNERARM_LOWER",
//        "IMAGE_REANIM_ZOMBIE_INNERARM_UPPER", "IMAGE_REANIM_ZOMBIE_INNERLEG_FOOT", "IMAGE_REANIM_ZOMBIE_INNERLEG_LOWER",
//        "IMAGE_REANIM_ZOMBIE_INNERLEG_UPPER", "IMAGE_REANIM_ZOMBIE_OUTERARM_HAND", "IMAGE_REANIM_ZOMBIE_OUTERARM_LOWER",
//        "IMAGE_REANIM_ZOMBIE_OUTERARM_UPPER", "IMAGE_REANIM_ZOMBIE_OUTERLEG_FOOT", "IMAGE_REANIM_ZOMBIE_OUTERLEG_LOWER",
//        "IMAGE_REANIM_ZOMBIE_OUTERLEG_UPPER",
//        // Thêm cho animation ăn (tay cầm kiểu khác) và tay cụt khi mất tay
//        "IMAGE_REANIM_ZOMBIE_OUTERARM_HAND2", "IMAGE_REANIM_ZOMBIE_OUTERARM_UPPER2"
//    };

//    // Các track thuộc phần đầu: sẽ dính theo thân khi chết còn đầu, và biến mất khi đầu bật ra
//    private static readonly HashSet<string> headTrackNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
//    {
//        "anim_head1", "anim_head2", "anim_tongue", "Zombie_mustache", "anim_hair",
//        "zombie_santahat", "anim_cone", "anim_bucket"
//    };

//    private sealed class PartRenderer
//    {
//        public ReanimDataCache.ReanimTrack track;
//        public MeshRenderer renderer;
//        public Mesh mesh;
//        public Vector3[] vertices = new Vector3[4];
//        public Vector2[] uvs = new Vector2[4];
//        public string currentImageId;
//        public int basePartIndex;
//        public int raisedPartIndex;   // thứ tự layer khi được nâng lên trên đầu (chỉ dùng cho cẳng tay + bàn tay lúc ăn)

//        public bool isHead;
//        public bool isArmDropped;   // cẳng tay + bàn tay ngoài (rụng khi mất tay)
//        public bool isArmUpper;     // bắp tay ngoài (đổi thành tay cụt)

//        public Vector2[] corners = new Vector2[4];
//        public Vector2[] blendFrom = new Vector2[4];
//        public bool blendValid;
//        public bool lastVisible;
//        public string lastImageId;
//    }

//    private struct Pose
//    {
//        public float x, y, kx, ky, sx, sy;
//        public bool visible;
//        public string imageId;
//    }

//    private struct Affine
//    {
//        public float m00, m01, m10, m11, tx, ty;

//        public static Affine Identity { get { return new Affine { m00 = 1f, m11 = 1f }; } }

//        public Affine Mul(Affine o)
//        {
//            return new Affine
//            {
//                m00 = m00 * o.m00 + m01 * o.m10,
//                m01 = m00 * o.m01 + m01 * o.m11,
//                m10 = m10 * o.m00 + m11 * o.m10,
//                m11 = m10 * o.m01 + m11 * o.m11,
//                tx = m00 * o.tx + m01 * o.ty + tx,
//                ty = m10 * o.tx + m11 * o.ty + ty
//            };
//        }

//        public Affine Inverse()
//        {
//            float det = m00 * m11 - m01 * m10;
//            if (Mathf.Abs(det) < 1e-6f) return Identity;
//            float i00 = m11 / det;
//            float i01 = -m01 / det;
//            float i10 = -m10 / det;
//            float i11 = m00 / det;
//            return new Affine
//            {
//                m00 = i00, m01 = i01, m10 = i10, m11 = i11,
//                tx = -(i00 * tx + i01 * ty),
//                ty = -(i10 * tx + i11 * ty)
//            };
//        }
//    }

//    private readonly List<PartRenderer> parts = new List<PartRenderer>();
//    private readonly List<PartRenderer> footParts = new List<PartRenderer>();
//    private readonly List<GameObject> debrisObjects = new List<GameObject>();
//    private PartRenderer shadowPart;
//    private PartRenderer shadowPart2;
//    private PartRenderer bodyPart;

//    private Vector2 anchorPixel;   // điểm neo (giữa 2 bàn chân) trong toạ độ reanim, đã nhân globalScale
//    private readonly Vector2[] cornerBuffer = new Vector2[4];
//    private readonly Color32[] colorBuffer = new Color32[4];
//    private static Material sharedPartMaterial;

//    private ZombieState state = ZombieState.Idle;
//    private ReanimDataCache.ReanimClip currentClip;
//    private bool clipLoop = true;
//    private float clipTime;
//    private int refFrame;
//    private float blendTimer;
//    private bool ready;

//    private string chosenWalkClip;
//    private bool armLost;
//    private bool headDetached;
//    private float currentHealth;
//    private float deadTimer;
//    private float currentAlpha = 1f;
//    private bool destroyQueued;

//    private void Start()
//    {
//        if (atlasTexture == null || atlasJsonAsset == null || reanimJsonAsset == null) return;

//        // Khởi tạo Cache tĩnh nếu chưa có
//        if (!ReanimDataCache.Initialize(atlasJsonAsset, reanimJsonAsset, baseZombiePartIds, shadowImageId))
//        {
//            Debug.LogError("Không thể khởi tạo dữ liệu Reanim cache.");
//            return;
//        }

//        // Dùng chung Material để tối ưu hóa Draw Call
//        if (sharedPartMaterial == null || sharedPartMaterial.mainTexture != atlasTexture)
//        {
//            Shader shader = Shader.Find("Sprites/Default");
//            sharedPartMaterial = new Material(shader) { mainTexture = atlasTexture };
//        }

//        refFrame = ReanimDataCache.Clips["idle"].start;
//        chosenWalkClip = (randomWalkVariant && UnityEngine.Random.value < 0.5f) ? walkClip2 : walkClip;
//        currentHealth = maxHealth;

//        BuildParts();

//        switch (initialState)
//        {
//            case ZombieState.Walk: Walk(); break;
//            case ZombieState.Eat: StartEating(); break;
//            default: Idle(); break;
//        }

//        ready = true;
//        RenderCurrentFrame(0f);
//    }

//    private void Update()
//    {
//        if (!ready || parts.Count == 0 || ReanimDataCache.Fps <= 0) return;

//        float rawDt = Time.deltaTime;
//        HandleTestToggles();

//        clipTime += rawDt * animationSpeed;

//        if (state == ZombieState.Dying && currentClip != null && clipTime * ReanimDataCache.Fps >= currentClip.Length)
//        {
//            state = ZombieState.Dead;
//            deadTimer = 0f;
//            if (OnDeathFinished != null) OnDeathFinished();
//        }

//        if (state == ZombieState.Walk && moveWhileWalking)
//            transform.position += Vector3.left * (walkSpeed * animationSpeed * rawDt);

//        RenderCurrentFrame(rawDt);
//        UpdateDeathFade(rawDt);
//        UpdateSortingOrders();
//    }

//    // ------------------------------------------------------------------
//    //  API điều khiển (gọi từ script gameplay của bạn)
//    // ------------------------------------------------------------------

//    /// <summary>Đứng yên.</summary>
//    public void Idle()
//    {
//        if (IsDeadOrDying) return;
//        state = ZombieState.Idle;
//        SetClip("idle", true);
//    }

//    /// <summary>Đi bộ (kiểu đi được chọn ngẫu nhiên lúc xuất hiện, giữ nguyên kể cả khi mất tay).</summary>
//    public void Walk()
//    {
//        if (IsDeadOrDying) return;
//        state = ZombieState.Walk;
//        SetClip(GetWalkClipName(), true);
//    }

//    /// <summary>Chọn kiểu đi: false = walkClip, true = walkClip2.</summary>
//    public void SetWalkVariant(bool second)
//    {
//        chosenWalkClip = second ? walkClip2 : walkClip;
//        if (state == ZombieState.Walk) Walk();
//    }

//    private string GetWalkClipName()
//    {
//        if (armLost && !string.IsNullOrEmpty(walkLostArmClip)) return walkLostArmClip;
//        return chosenWalkClip;
//    }

//    /// <summary>Cúi xuống cắn/nhai cây.</summary>
//    public void StartEating()
//    {
//        if (IsDeadOrDying) return;
//        state = ZombieState.Eat;
//        SetClip(eatClip, true);
//    }

//    /// <summary>Ăn xong (cây chết) thì đi tiếp.</summary>
//    public void StopEating()
//    {
//        if (state == ZombieState.Eat) Walk();
//    }

//    /// <summary>Rụng tay trái: tay rơi ra, tay cụt, và đi bộ chuyển sang clip mất tay.</summary>
//    public void LoseArm()
//    {
//        if (armLost || IsDeadOrDying || !ready) return;
//        armLost = true;

//        List<PartRenderer> armPieces = new List<PartRenderer>();
//        foreach (PartRenderer part in parts)
//            if (part.isArmDropped) armPieces.Add(part);

//        SpawnDebris(armPieces,
//            new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(1.4f, 2.0f), 0f),
//            UnityEngine.Random.Range(-200f, 200f), 1.2f, 0.5f);

//        if (state == ZombieState.Walk) Walk();
//        if (OnArmLost != null) OnArmLost();
//    }

//    /// <summary>Chết khi còn đầu: ngã ra sau, đầu dính theo thân.</summary>
//    public void Die() { BeginDeath(false); }

//    /// <summary>Chết mất đầu: đầu bật ra bay đi, thân ngã không đầu.</summary>
//    public void DieHeadless() { BeginDeath(true); }

//    /// <summary>Trừ máu. Tới mốc armLostHealth thì rụng tay, về 0 thì chết.</summary>
//    public void TakeDamage(float damage, bool removeHead = false)
//    {
//        if (IsDeadOrDying || !ready) return;
//        currentHealth -= damage;

//        if (currentHealth <= 0f)
//        {
//            if (removeHead) DieHeadless(); else Die();
//            return;
//        }
//        if (!armLost && currentHealth <= armLostHealth) LoseArm();
//    }

//    /// <summary>Hồi sinh / đưa zombie về trạng thái ban đầu (dùng để test).</summary>
//    public void ResetZombie()
//    {
//        if (!ready) return;

//        foreach (GameObject go in debrisObjects)
//            if (go != null) Destroy(go);
//        debrisObjects.Clear();

//        armLost = false;
//        headDetached = false;
//        destroyQueued = false;
//        deadTimer = 0f;
//        currentHealth = maxHealth;
//        SetAlpha(1f);

//        state = ZombieState.Idle;
//        SetClip("idle", true);
//    }

//    // ------------------------------------------------------------------
//    //  Nội bộ
//    // ------------------------------------------------------------------

//    private void HandleTestToggles()
//    {
//        if (testReset) { testReset = false; ResetZombie(); }

//        bool wantsAction = testIdle || testWalk || testWalk2 || testEat || testLoseArm || testDie || testDieHeadless;
//        if (!wantsAction) return;

//        if (IsDeadOrDying)
//        {
//            testIdle = testWalk = testWalk2 = testEat = testLoseArm = testDie = testDieHeadless = false;
//            Debug.Log("PvZReanimZombie: zombie đã chết, hãy tick testReset trước.", this);
//            return;
//        }

//        if (testIdle) { testIdle = false; Idle(); }
//        if (testWalk) { testWalk = false; chosenWalkClip = walkClip; Walk(); }
//        if (testWalk2) { testWalk2 = false; chosenWalkClip = walkClip2; Walk(); }
//        if (testEat) { testEat = false; StartEating(); }
//        if (testLoseArm) { testLoseArm = false; LoseArm(); }
//        if (testDie) { testDie = false; Die(); }
//        if (testDieHeadless) { testDieHeadless = false; DieHeadless(); }
//    }

//    private void BeginDeath(bool headless)
//    {
//        if (IsDeadOrDying || !ready) return;

//        if (headless || headDetached)
//        {
//            headless = true;
//            if (!headDetached)
//            {
//                headDetached = true;
//                List<PartRenderer> headPieces = new List<PartRenderer>();
//                foreach (PartRenderer part in parts)
//                    if (part.isHead) headPieces.Add(part);

//                SpawnDebris(headPieces,
//                    new Vector3(-UnityEngine.Random.Range(0.6f, 1.3f), UnityEngine.Random.Range(3.0f, 4.0f), 0f),
//                    UnityEngine.Random.Range(-420f, 420f), 1.4f, 0.5f);
//            }
//        }

//        state = ZombieState.Dying;
//        SetClip(headless ? deathHeadlessClip : deathClip, false);
//        if (OnDeathStarted != null) OnDeathStarted();
//    }

//    private bool SetClip(string clipName, bool loop)
//    {
//        ReanimDataCache.ReanimClip clip;
//        if (!ReanimDataCache.Clips.TryGetValue(clipName, out clip))
//        {
//            Debug.LogWarning("PvZReanimZombie: không tìm thấy clip '" + clipName + "' trong file reanim.", this);
//            return false;
//        }

//        if (clip == currentClip && clipLoop == loop) return true;

//        // Lưu pose hiện tại để chuyển mượt sang animation mới
//        if (currentClip != null && blendTime > 0f)
//        {
//            foreach (PartRenderer part in parts)
//            {
//                part.blendValid = part.lastVisible;
//                if (part.blendValid)
//                    Array.Copy(part.corners, part.blendFrom, 4);
//            }
//            blendTimer = blendTime;
//        }

//        currentClip = clip;
//        clipLoop = loop;
//        clipTime = 0f;
//        return true;
//    }

//    private void BuildParts()
//    {
//        int partIndexCounter = 0;
//        foreach (var sourceTrack in ReanimDataCache.CachedTracks)
//        {
//            bool usesBaseSprite = false;
//            for (int i = 0; i < sourceTrack.frames.Length; i++)
//            {
//                string id = sourceTrack.frames[i].imageId;
//                if (!string.IsNullOrEmpty(id) && ReanimDataCache.AtlasParts.ContainsKey(id))
//                {
//                    usesBaseSprite = true;
//                    break;
//                }
//            }

//            if (!usesBaseSprite) continue;

//            PartRenderer part = CreatePart(sourceTrack, partIndexCounter++);
//            part.isHead = headTrackNames.Contains(sourceTrack.name);
//            part.isArmDropped = string.Equals(sourceTrack.name, "Zombie_outerarm_lower", StringComparison.OrdinalIgnoreCase)
//                             || string.Equals(sourceTrack.name, "Zombie_outerarm_hand", StringComparison.OrdinalIgnoreCase);
//            part.isArmUpper = string.Equals(sourceTrack.name, "Zombie_outerarm_upper", StringComparison.OrdinalIgnoreCase);
//            if (string.Equals(sourceTrack.name, "Zombie_body", StringComparison.OrdinalIgnoreCase))
//                bodyPart = part;

//            parts.Add(part);
//            if (sourceTrack.name.IndexOf("foot", StringComparison.OrdinalIgnoreCase) >= 0)
//                footParts.Add(part);
//        }

//        // Đầu + cằm (và phụ kiện đầu) luôn nằm trên cùng, cao hơn cả hai tay.
//        // Các bộ phận còn lại giữ nguyên thứ tự cũ, bộ phận đầu giữ nguyên thứ tự với nhau.
//        int orderIndex = 0;
//        foreach (PartRenderer part in parts)
//            if (!part.isHead) part.basePartIndex = orderIndex++;
//        foreach (PartRenderer part in parts)
//            if (part.isHead) part.basePartIndex = orderIndex++;
//        // Cẳng tay + bàn tay ngoài: lúc ăn thì nâng lên trên cả đầu, các lúc khác giữ ở dưới đầu
//        foreach (PartRenderer part in parts)
//        {
//            part.raisedPartIndex = part.basePartIndex;
//            if (part.isArmDropped) part.raisedPartIndex = orderIndex++;
//        }

//        shadowPart = CreatePart(new ReanimDataCache.ReanimTrack { name = "Shadow_1", frames = new ReanimDataCache.ReanimFrame[0] }, -1);
//        SetPartUvs(shadowPart, ReanimDataCache.AtlasParts[shadowImageId], shadowImageId);

//        shadowPart2 = CreatePart(new ReanimDataCache.ReanimTrack { name = "Shadow_2", frames = new ReanimDataCache.ReanimFrame[0] }, -1);
//        SetPartUvs(shadowPart2, ReanimDataCache.AtlasParts[shadowImageId], shadowImageId);

//        UpdateSortingOrders();
//    }

//    private PartRenderer CreatePart(ReanimDataCache.ReanimTrack track, int basePartIndex)
//    {
//        GameObject go = new GameObject(track.name);
//        go.transform.SetParent(transform, false);

//        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
//        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
//        meshRenderer.sharedMaterial = sharedPartMaterial;
//        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
//        meshRenderer.receiveShadows = false;

//        Mesh mesh = new Mesh { name = track.name };
//        mesh.MarkDynamic();
//        mesh.vertices = new Vector3[4];
//        mesh.uv = new Vector2[4];
//        mesh.colors32 = new Color32[]
//        {
//            new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
//            new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
//        };
//        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
//        meshFilter.sharedMesh = mesh;

//        return new PartRenderer { track = track, renderer = meshRenderer, mesh = mesh, basePartIndex = basePartIndex };
//    }

//    /// <summary>Tính frame hiện tại của clip rồi vẽ tất cả bộ phận.</summary>
//    private void RenderCurrentFrame(float rawDt)
//    {
//        if (currentClip == null) return;

//        float elapsedFrames = clipTime * ReanimDataCache.Fps;
//        float pos;
//        if (clipLoop)
//            pos = currentClip.start + Mathf.Repeat(elapsedFrames, Mathf.Max(1, currentClip.Length));
//        else
//            pos = currentClip.start + Mathf.Min(elapsedFrames, currentClip.Length);

//        int frameA = Mathf.Clamp(Mathf.FloorToInt(pos), currentClip.start, currentClip.end);
//        int frameB = Mathf.Min(frameA + 1, currentClip.end);
//        float t = frameA == frameB ? 0f : pos - frameA;

//        // Trọng số chuyển mượt giữa animation cũ và mới
//        bool blending = false;
//        float blendWeight = 1f;
//        if (blendTimer > 0f && blendTime > 0f)
//        {
//            blendTimer = Mathf.Max(0f, blendTimer - rawDt);
//            blending = blendTimer > 0f;
//            blendWeight = 1f - blendTimer / blendTime;
//        }

//        // Khi chết còn đầu: đầu dính cứng vào thân (track đầu bị ẩn trong clip chết nên phải tự tính)
//        bool attachHead = IsDeadOrDying && !headDetached && bodyPart != null;
//        Affine headAttach = Affine.Identity;
//        if (attachHead)
//        {
//            Affine bodyNow = ToAffine(Sample(bodyPart.track, frameA, frameB, t));
//            Affine bodyRef = ToAffine(FromFrame(bodyPart.track.frames[refFrame]));
//            headAttach = bodyNow.Mul(bodyRef.Inverse());
//        }

//        UpdateShadow();

//        foreach (PartRenderer part in parts)
//        {
//            Pose pose = Sample(part.track, frameA, frameB, t);
//            bool visible = pose.visible;
//            string imageId = pose.imageId;
//            Affine matrix = ToAffine(pose);

//            if (armLost)
//            {
//                if (part.isArmDropped) visible = false;
//                else if (part.isArmUpper && ReanimDataCache.AtlasParts.ContainsKey(armUpper2ImageId))
//                    imageId = armUpper2ImageId;
//            }

//            if (part.isHead)
//            {
//                if (headDetached)
//                {
//                    visible = false;
//                }
//                else if (attachHead)
//                {
//                    Pose refPose = FromFrame(part.track.frames[refFrame]);
//                    visible = refPose.visible;
//                    imageId = refPose.imageId;
//                    matrix = headAttach.Mul(ToAffine(refPose));
//                }
//            }

//            ReanimDataCache.AtlasPart atlasPart = null;
//            bool hasSprite = visible && !string.IsNullOrEmpty(imageId) && ReanimDataCache.AtlasParts.TryGetValue(imageId, out atlasPart);
//            part.renderer.enabled = hasSprite;
//            part.lastVisible = hasSprite;
//            if (!hasSprite) continue;

//            SetPartUvs(part, atlasPart, imageId);
//            part.lastImageId = imageId;

//            ComputeCorners(matrix, atlasPart, part.corners);
//            if (blending && part.blendValid)
//            {
//                for (int i = 0; i < 4; i++)
//                    part.corners[i] = Vector2.Lerp(part.blendFrom[i], part.corners[i], blendWeight);
//            }

//            for (int i = 0; i < 4; i++)
//                part.vertices[i] = ToUnity(part.corners[i].x, part.corners[i].y);

//            part.mesh.SetVertices(part.vertices);
//            part.mesh.RecalculateBounds();
//        }
//    }

//    private static Pose Sample(ReanimDataCache.ReanimTrack track, int frameA, int frameB, float t)
//    {
//        var a = track.frames[frameA];
//        var b = track.frames[frameB];
//        return new Pose
//        {
//            x = Mathf.Lerp(a.x, b.x, t),
//            y = Mathf.Lerp(a.y, b.y, t),
//            kx = a.kx + Mathf.DeltaAngle(a.kx, b.kx) * t,
//            ky = a.ky + Mathf.DeltaAngle(a.ky, b.ky) * t,
//            sx = Mathf.Lerp(a.sx, b.sx, t),
//            sy = Mathf.Lerp(a.sy, b.sy, t),
//            visible = a.visible,
//            imageId = a.imageId
//        };
//    }

//    private static Pose FromFrame(ReanimDataCache.ReanimFrame f)
//    {
//        return new Pose { x = f.x, y = f.y, kx = f.kx, ky = f.ky, sx = f.sx, sy = f.sy, visible = f.visible, imageId = f.imageId };
//    }

//    private static Affine ToAffine(Pose p)
//    {
//        float kx = p.kx * Mathf.Deg2Rad;
//        float ky = p.ky * Mathf.Deg2Rad;
//        return new Affine
//        {
//            m00 = Mathf.Cos(kx) * p.sx,
//            m01 = -Mathf.Sin(ky) * p.sy,
//            m10 = Mathf.Sin(kx) * p.sx,
//            m11 = Mathf.Cos(ky) * p.sy,
//            tx = p.x * positionScale,
//            ty = p.y * positionScale
//        };
//    }

//    private void ComputeCorners(Affine m, ReanimDataCache.AtlasPart atlasPart, Vector2[] corners)
//    {
//        float w = atlasPart.width;
//        float h = atlasPart.height;
//        float gx = globalScale.x;
//        float gy = globalScale.y;

//        corners[0] = new Vector2(m.tx * gx, m.ty * gy);
//        corners[1] = new Vector2((m.tx + m.m00 * w) * gx, (m.ty + m.m10 * w) * gy);
//        corners[2] = new Vector2((m.tx + m.m00 * w + m.m01 * h) * gx, (m.ty + m.m10 * w + m.m11 * h) * gy);
//        corners[3] = new Vector2((m.tx + m.m01 * h) * gx, (m.ty + m.m11 * h) * gy);
//    }

//    private static void SetPartUvs(PartRenderer part, ReanimDataCache.AtlasPart atlasPart, string imageId)
//    {
//        if (part.currentImageId == imageId) return;

//        part.uvs[0] = new Vector2(atlasPart.uvMin.x, atlasPart.uvMax.y);
//        part.uvs[1] = new Vector2(atlasPart.uvMax.x, atlasPart.uvMax.y);
//        part.uvs[2] = new Vector2(atlasPart.uvMax.x, atlasPart.uvMin.y);
//        part.uvs[3] = new Vector2(atlasPart.uvMin.x, atlasPart.uvMin.y);
//        part.mesh.SetUVs(0, part.uvs);
//        part.currentImageId = imageId;
//    }

//    private void UpdateShadow()
//    {
//        if (shadowPart == null || shadowPart2 == null || footParts.Count == 0) return;

//        float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
//        foreach (PartRenderer foot in footParts)
//        {
//            var frame = foot.track.frames[refFrame];
//            ReanimDataCache.AtlasPart footAtlas;
//            if (!ReanimDataCache.AtlasParts.TryGetValue(frame.imageId ?? string.Empty, out footAtlas)) continue;

//            ComputeCorners(ToAffine(FromFrame(frame)), footAtlas, cornerBuffer);
//            for (int i = 0; i < 4; i++)
//            {
//                minX = Mathf.Min(minX, cornerBuffer[i].x);
//                maxX = Mathf.Max(maxX, cornerBuffer[i].x);
//                maxY = Mathf.Max(maxY, cornerBuffer[i].y);
//            }
//        }

//        if (minX > maxX) return;

//        // Neo gốc toạ độ vào giữa 2 bàn chân để zombie hiện đúng chỗ GameObject đứng
//        anchorPixel = anchorToFeet ? new Vector2((minX + maxX) * 0.5f, maxY) : Vector2.zero;

//        var shadow = ReanimDataCache.AtlasParts[shadowImageId];
//        float halfW = shadow.width * globalScale.x * 0.5f;
//        float halfH = shadow.height * globalScale.y * 0.5f;

//        float cx = (minX + maxX) * 0.5f + (shadowOffset.x * positionScale);
//        float cy = maxY + (shadowOffset.y * positionScale);

//        Vector3 v0 = ToUnity(cx - halfW, cy - halfH);
//        Vector3 v1 = ToUnity(cx + halfW, cy - halfH);
//        Vector3 v2 = ToUnity(cx + halfW, cy + halfH);
//        Vector3 v3 = ToUnity(cx - halfW, cy + halfH);

//        shadowPart.vertices[0] = v0; shadowPart.vertices[1] = v1; shadowPart.vertices[2] = v2; shadowPart.vertices[3] = v3;
//        shadowPart.mesh.SetVertices(shadowPart.vertices);
//        shadowPart.mesh.RecalculateBounds();
//        shadowPart.renderer.enabled = true;

//        shadowPart2.vertices[0] = v0; shadowPart2.vertices[1] = v1; shadowPart2.vertices[2] = v2; shadowPart2.vertices[3] = v3;
//        shadowPart2.mesh.SetVertices(shadowPart2.vertices);
//        shadowPart2.mesh.RecalculateBounds();
//        shadowPart2.renderer.enabled = true;
//    }

//    /// <summary>Tạo mảnh rơi từ các bộ phận đang hiển thị (đầu, tay) tại đúng vị trí hiện tại của chúng.</summary>
//    private void SpawnDebris(List<PartRenderer> sources, Vector3 velocity, float angularVelocity, float lifetime, float fadeTime)
//    {
//        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
//        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
//        bool any = false;

//        foreach (PartRenderer s in sources)
//        {
//            if (!s.lastVisible) continue;
//            for (int i = 0; i < 4; i++)
//            {
//                Vector3 v = ToUnity(s.corners[i].x, s.corners[i].y);
//                min = Vector3.Min(min, v);
//                max = Vector3.Max(max, v);
//                any = true;
//            }
//        }
//        if (!any) return;

//        Vector3 pivot = (min + max) * 0.5f;
//        GameObject root = new GameObject("Debris");
//        root.transform.SetParent(transform, false);
//        root.transform.localPosition = pivot;

//        List<Mesh> meshes = new List<Mesh>();
//        foreach (PartRenderer s in sources)
//        {
//            ReanimDataCache.AtlasPart atlasPart;
//            if (!s.lastVisible || !ReanimDataCache.AtlasParts.TryGetValue(s.lastImageId ?? string.Empty, out atlasPart)) continue;

//            GameObject go = new GameObject(s.track.name + "_piece");
//            go.transform.SetParent(root.transform, false);

//            MeshFilter mf = go.AddComponent<MeshFilter>();
//            MeshRenderer mr = go.AddComponent<MeshRenderer>();
//            mr.sharedMaterial = sharedPartMaterial;
//            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
//            mr.receiveShadows = false;
//            mr.sortingOrder = s.renderer.sortingOrder;

//            Mesh mesh = new Mesh { name = go.name };
//            Vector3[] verts = new Vector3[4];
//            for (int i = 0; i < 4; i++)
//                verts[i] = ToUnity(s.corners[i].x, s.corners[i].y) - pivot;
//            mesh.vertices = verts;
//            mesh.uv = new[]
//            {
//                new Vector2(atlasPart.uvMin.x, atlasPart.uvMax.y), new Vector2(atlasPart.uvMax.x, atlasPart.uvMax.y),
//                new Vector2(atlasPart.uvMax.x, atlasPart.uvMin.y), new Vector2(atlasPart.uvMin.x, atlasPart.uvMin.y)
//            };
//            mesh.colors32 = new Color32[]
//            {
//                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
//                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
//            };
//            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
//            mesh.RecalculateBounds();
//            mf.sharedMesh = mesh;
//            meshes.Add(mesh);
//        }

//        ReanimDebris debris = root.AddComponent<ReanimDebris>();
//        debris.Init(velocity, debrisGravity, angularVelocity, lifetime, fadeTime, meshes.ToArray());
//        debrisObjects.Add(root);
//    }

//    private void UpdateDeathFade(float rawDt)
//    {
//        if (state != ZombieState.Dead || !fadeAfterDeath) return;

//        deadTimer += rawDt;
//        if (deadTimer <= lingerTime) return;

//        float k = fadeDuration <= 0f ? 1f : Mathf.Clamp01((deadTimer - lingerTime) / fadeDuration);
//        SetAlpha(1f - k);

//        if (k >= 1f && destroyAfterFade && !destroyQueued)
//        {
//            destroyQueued = true;
//            Destroy(gameObject);
//        }
//    }

//    private void SetAlpha(float alpha)
//    {
//        if (Mathf.Approximately(alpha, currentAlpha)) return;
//        currentAlpha = alpha;

//        byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha));
//        for (int i = 0; i < 4; i++) colorBuffer[i] = new Color32(255, 255, 255, a);

//        foreach (PartRenderer part in parts)
//            part.mesh.colors32 = colorBuffer;
//        if (shadowPart != null) shadowPart.mesh.colors32 = colorBuffer;
//        if (shadowPart2 != null) shadowPart2.mesh.colors32 = colorBuffer;
//    }

//    private Vector3 ToUnity(float pixelX, float pixelY)
//    {
//        return new Vector3(
//            (pixelX - anchorPixel.x) / pixelPerUnit + pivotOffset.x,
//            -(pixelY - anchorPixel.y) / pixelPerUnit + pivotOffset.y,
//            0f);
//    }

//    private void UpdateSortingOrders()
//    {
//        float currentX = transform.position.x;
//        bool eating = state == ZombieState.Eat;
//        foreach (var part in parts)
//        {
//            if (part.renderer != null)
//            {
//                int index = (eating && part.isArmDropped) ? part.raisedPartIndex : part.basePartIndex;
//                if (ZombieLayerManager.Instance != null)
//                    part.renderer.sortingOrder = ZombieLayerManager.Instance.GetSortingOrder(currentLane, index, currentX);
//                else
//                    part.renderer.sortingOrder = ((int)currentLane * 1000) + index;
//            }
//        }

//        int shadowOrder = ZombieLayerManager.Instance != null ? ZombieLayerManager.Instance.GetShadowSortingOrder(currentLane) : ((int)currentLane * 1000) - 10;
//        if (shadowPart != null && shadowPart.renderer != null) shadowPart.renderer.sortingOrder = shadowOrder;
//        if (shadowPart2 != null && shadowPart2.renderer != null) shadowPart2.renderer.sortingOrder = shadowOrder;
//    }

//    private void OnDestroy()
//    {
//        foreach (PartRenderer part in parts)
//            if (part.mesh != null) Destroy(part.mesh);

//        if (shadowPart != null && shadowPart.mesh != null) Destroy(shadowPart.mesh);
//        if (shadowPart2 != null && shadowPart2.mesh != null) Destroy(shadowPart2.mesh);
//    }
//}