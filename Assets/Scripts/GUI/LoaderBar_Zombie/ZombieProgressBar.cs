using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh tiến độ zombie (Flag Meter) kiểu PvZ1 – dựng hoàn toàn bằng UGUI.
/// Chỉ cần gán 3 file: atlas PNG + atlas JSON (cắt ảnh) + reanim JSON.
///
/// Tiến độ:
///  - Nếu gán CampaignWaveSpawner (followSpawner = true): thanh bám theo spawner thật
///    (0 lúc wave 1 spawn -> 1 lúc wave cuối spawn, không tính countdown đầu level).
///    Với pvzStyleSmoothing = true, thanh hiển thị tiến dần về tiến độ thật với tốc độ giới hạn (như PvZ1),
///    nên wave đến sớm không làm thanh "vụt" lên.
///  - Nếu không: dùng Play(seconds) / SetProgress(p) như cũ.
///
/// Cờ: đặt đúng tại tiến độ của wave cờ (lấy từ spawner), hoặc k/N nếu không có spawner.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ZombieProgressBar : MonoBehaviour
{
    // ───────────────────────── INPUT (chỉ cần 3 file này) ─────────────────────────
    [Header("3 file đầu vào")]
    public Texture2D atlasTexture;   // LOADINGIMAGES_PLANTSZOMBIES_3_960_00.PNG
    public TextAsset atlasJson;      // LoadingImages_PlantsZombies_3_960.json
    public TextAsset reanimJson;     // loadbar_zombiehead_reanim_compiled_reanim.json

    // ───────────────────────── NGUỒN TIẾN ĐỘ ─────────────────────────
    [Header("Bám theo spawner")]
    [SerializeField] private CampaignWaveSpawner campaignWaveSpawner;
    [Tooltip("Bật: tiến độ lấy từ spawner. Tắt: dùng Play()/SetProgress() thủ công.")]
    public bool followSpawner = true;
    [Tooltip("Độ mượt khi tiến độ nhảy (chỉ dùng khi tắt pvzStyleSmoothing). 0 = không làm mượt.")]
    [Min(0f)] public float smoothTime = 0.25f;

    [Header("Tốc độ thanh (port từ PvZ1)")]
    [Tooltip("Bật: thanh hiển thị tiến dần về tiến độ thật với tốc độ giới hạn như PvZ1 (không nhảy/vụt). Tắt: dùng smoothTime.")]
    public bool pvzStyleSmoothing = true;
    [Tooltip("px/giây khi thanh tụt sau mục tiêu (PvZ1: 1px mỗi 20 tick = 5px/s).")]
    [Min(0.1f)] public float slowPixelsPerSecond = 5f;
    [Tooltip("px/giây khi tụt xa hơn 1 đoạn wave (PvZ1: 1px mỗi 5 tick = 20px/s).")]
    [Min(0.1f)] public float fastPixelsPerSecond = 20f;
    [Tooltip("Tổng chiều dài thanh theo PvZ1 (150px), dùng để quy đổi tốc độ sang tỉ lệ 0..1.")]
    [Min(1f)] public float meterPixels = 150f;

    // ───────────────────────── SETTING ─────────────────────────
    [Header("Tiến độ")]
    [Range(0f, 1f)] public float progress = 0f;
    [Min(0)] public int flagCount = 1;   // 0 = không có cờ (VD: level đầu)

    [Header("Tự chạy theo thời gian (chỉ khi KHÔNG bám spawner)")]
    public bool autoPlayOnStart = false;
    [Min(0.01f)] public float duration = 10f;
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
    public bool loop = false;

    [Header("Hiển thị")]
    public float scale = 1f;
    public bool playHeadIntro = false;     // đầu zombie "trồi lên" lúc bắt đầu theo reanim
    public float introMoveScale = 1f;

    [Header("Tinh chỉnh (px theo ảnh gốc, ít khi cần sửa)")]
    public float fillLeft = 14f;           // mép trái phần ruột thanh
    public float fillRight = 304f;         // mép phải phần ruột thanh
    public float poleBaseY = -10f;         // chân cột cờ so với tâm thanh

    public event Action OnCompleted;

    public float Progress => progress;
    public bool IsPlaying => _playing;

    // ───────────────────────── nội bộ ─────────────────────────
    const string KEY_BAR = "IMAGE_FLAGMETER";
    const string KEY_PARTS = "IMAGE_FLAGMETERPARTS";

    float _w, _h;                          // kích thước 1 thanh (px)
    RectTransform _root, _mask, _head;
    readonly List<RectTransform> _flags = new List<RectTransform>();
    readonly List<float> _flagFrac = new List<float>();
    List<float> _customFlagFrac;           // vị trí cờ lấy từ spawner (null = dùng k/N)
    Sprite _sprEmpty, _sprFull, _sprHead, _sprPole, _sprFlag;

    bool _playing;
    float _elapsed;
    float _smoothVelocity;
    bool _completedFired;

    float _visual;                         // tiến độ hiển thị (đuổi theo target với tốc độ giới hạn)

    // reanim intro
    struct RFrame { public float x, y, kx, sx, sy; }
    RFrame[] _intro;
    float _introFps = 12f;
    float _introTime;
    bool _introDone = true;
    Vector2 _headBase;

    bool FollowingSpawner => followSpawner && campaignWaveSpawner != null;

    // ───────────────────────── vòng đời ─────────────────────────
    void Awake()
    {
        EnsureCanvas();
        Build();
    }

    void Start()
    {
        SetupFlagsFromSpawner();
        BuildFlags();
        if (!FollowingSpawner && autoPlayOnStart) Play(duration);
    }

    /// <summary>Lấy số cờ, vị trí từng cờ và tốc độ danh nghĩa từ spawner.</summary>
    private void SetupFlagsFromSpawner()
    {
        if (!FollowingSpawner) return;

        _customFlagFrac = campaignWaveSpawner.GetFlagProgressFractions();
        flagCount = _customFlagFrac.Count;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (FollowingSpawner)
        {
            float target = Mathf.Clamp01(campaignWaveSpawner.LevelProgress01);

            if (pvzStyleSmoothing)
            {
                // Port PvZ1: thanh hiển thị chỉ tiến dần về target với tốc độ giới hạn.
                //  - bình thường: 1px / 20 tick; tụt xa hơn 1 đoạn wave: 1px / 5 tick.
                // Dùng Time.deltaTime để thanh dừng cùng spawner khi pause.
                float delta = target - _visual;
                if (delta > 0f)
                {
                    int waves = campaignWaveSpawner.TotalWaves;
                    float segLen = waves > 1 ? 1f / (waves - 1) : 1f;
                    float pxPerSec = delta > segLen ? fastPixelsPerSecond : slowPixelsPerSecond;
                    _visual = Mathf.Min(_visual + pxPerSec / meterPixels * Time.deltaTime, target);
                }
                progress = _visual;
            }
            else
            {
                progress = smoothTime > 0f
                    ? Mathf.SmoothDamp(progress, target, ref _smoothVelocity, smoothTime, Mathf.Infinity, dt)
                    : target;
            }

            if (!_completedFired && target >= 1f && progress >= 0.999f)
            {
                progress = 1f;
                _completedFired = true;
                OnCompleted?.Invoke();
            }
        }
        else if (_playing)
        {
            _elapsed += dt;
            float t = Mathf.Clamp01(_elapsed / duration);
            progress = Mathf.Clamp01(curve.Evaluate(t));
            if (t >= 1f)
            {
                progress = 1f;
                _playing = false;
                OnCompleted?.Invoke();
                if (loop) Play(duration);
            }
        }

        Apply(dt);
    }

    // ───────────────────────── API ─────────────────────────
    /// <summary>Đặt tiến độ trực tiếp 0..1 (chỉ có tác dụng khi không bám spawner).</summary>
    public void SetProgress(float p)
    {
        _playing = false;
        progress = Mathf.Clamp01(p);
    }

    /// <summary>Chạy tự động từ 0 -> 1 trong "seconds" giây (chế độ thủ công).</summary>
    public void Play(float seconds, Action onComplete = null)
    {
        duration = Mathf.Max(0.01f, seconds);
        _elapsed = 0f;
        progress = 0f;
        _playing = true;
        if (onComplete != null) OnCompleted += onComplete;
    }

    public void Stop() { _playing = false; }

    public void ResetBar()
    {
        _playing = false;
        _elapsed = 0f;
        progress = 0f;
        _visual = 0f;
        _smoothVelocity = 0f;
        _completedFired = false;
        StartIntro();
    }

    /// <summary>Đổi số cờ lúc runtime (cờ chia đều k/N).</summary>
    public void SetFlagCount(int n)
    {
        flagCount = Mathf.Max(0, n);
        _customFlagFrac = null;
        BuildFlags();
    }

    /// <summary>Đặt cờ tại các vị trí tiến độ cụ thể (0..1, 1 = ngoài cùng bên trái).</summary>
    public void SetFlagFractions(IList<float> fractions)
    {
        _customFlagFrac = fractions != null ? new List<float>(fractions) : null;
        flagCount = _customFlagFrac != null ? _customFlagFrac.Count : 0;
        BuildFlags();
    }

    // ───────────────────────── dựng UI ─────────────────────────
    void EnsureCanvas()
    {
        if (GetComponentInParent<Canvas>() != null) return;

        var cgo = new GameObject("ZombieBarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        transform.SetParent(cgo.transform, false);
    }

    void Build()
    {
        if (atlasTexture == null || atlasJson == null)
        {
            Debug.LogError("[ZombieProgressBar] Thiếu atlasTexture hoặc atlasJson.");
            enabled = false;
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

        var rects = ParseAtlas(atlasJson.text);
        if (!rects.TryGetValue(KEY_BAR, out var rBar) || !rects.TryGetValue(KEY_PARTS, out var rParts))
        {
            Debug.LogError("[ZombieProgressBar] Atlas JSON không có IMAGE_FLAGMETER / IMAGE_FLAGMETERPARTS.");
            enabled = false;
            return;
        }

        // IMAGE_FLAGMETER (316x108): nửa trên = thanh rỗng (bg), nửa dưới = thanh xanh đầy.
        int halfH = rBar.height / 2;
        _w = rBar.width;
        _h = halfH;
        _sprEmpty = Cut(new RectInt(rBar.x, rBar.y, rBar.width, halfH), new Vector2(0.5f, 0.5f));
        _sprFull = Cut(new RectInt(rBar.x, rBar.y + halfH, rBar.width, rBar.height - halfH), new Vector2(0.5f, 0.5f));

        // IMAGE_FLAGMETERPARTS (150x50): đầu zombie | cột cờ | lá cờ
        _sprHead = Cut(new RectInt(rParts.x + 4, rParts.y + 2, 45, 46), new Vector2(0.5f, 0.5f));
        _sprPole = Cut(new RectInt(rParts.x + 56, rParts.y + 10, 6, 36), new Vector2(0.5f, 0f));
        _sprFlag = Cut(new RectInt(rParts.x + 106, rParts.y + 2, 40, 36), new Vector2(0f, 1f));

        _root = (RectTransform)transform;
        _root.sizeDelta = new Vector2(_w, _h);
        _root.localScale = Vector3.one * scale;

        // 1) BG (thanh rỗng)
        var bg = NewImage("BG", _root, _sprEmpty);
        Stretch(bg.rectTransform);

        // 2) Thanh xanh: RectMask2D cắt theo progress, neo mép phải
        var maskGo = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
        _mask = (RectTransform)maskGo.transform;
        _mask.SetParent(_root, false);
        _mask.anchorMin = _mask.anchorMax = new Vector2(0.5f, 0.5f);
        _mask.pivot = new Vector2(1f, 0.5f);
        _mask.anchoredPosition = new Vector2(X(fillRight), 0f);
        _mask.sizeDelta = new Vector2(0f, _h);

        var green = NewImage("Fill", _mask, _sprFull);
        var gr = green.rectTransform;
        gr.anchorMin = gr.anchorMax = new Vector2(1f, 0.5f);
        gr.pivot = new Vector2(1f, 0.5f);
        gr.sizeDelta = new Vector2(_w, _h);
        gr.anchoredPosition = new Vector2(_w - fillRight, 0f);

        // 3) Cờ
        var flagsRoot = new GameObject("Flags", typeof(RectTransform));
        flagsRoot.transform.SetParent(_root, false);
        var fr = (RectTransform)flagsRoot.transform;
        Stretch(fr);
        BuildFlags();

        // 4) Đầu zombie (trên cùng)
        var headImg = NewImage("ZombieHead", _root, _sprHead);
        _head = headImg.rectTransform;
        _head.anchorMin = _head.anchorMax = new Vector2(0.5f, 0.5f);
        _head.sizeDelta = new Vector2(45, 46);

        PrepareIntro();
        StartIntro();
        Apply(0f);
    }

    void BuildFlags()
    {
        var flagsRoot = _root != null ? _root.Find("Flags") : null;
        if (flagsRoot == null) return;

        for (int i = flagsRoot.childCount - 1; i >= 0; i--) Destroy(flagsRoot.GetChild(i).gameObject);
        _flags.Clear(); _flagFrac.Clear();

        bool custom = _customFlagFrac != null;
        int n = custom ? _customFlagFrac.Count : Mathf.Max(0, flagCount);

        for (int k = 0; k < n; k++)
        {
            // custom: vị trí thật của wave cờ; mặc định: cờ thứ k nằm ở mốc k/N (cờ cuối ngoài cùng bên trái)
            float frac = custom ? Mathf.Clamp01(_customFlagFrac[k]) : (float)(k + 1) / n;
            float px = fillRight - frac * (fillRight - fillLeft);

            var pole = NewImage("Pole" + (k + 1), (RectTransform)flagsRoot, _sprPole);
            var pr = pole.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.sizeDelta = new Vector2(6, 36);
            pr.anchoredPosition = new Vector2(X(px), poleBaseY);

            var flag = NewImage("Flag" + (k + 1), (RectTransform)flagsRoot, _sprFlag);
            var fr = flag.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
            fr.pivot = new Vector2(0f, 1f);
            fr.sizeDelta = new Vector2(40, 36);
            fr.anchoredPosition = new Vector2(X(px) - 3f, poleBaseY + 36f);

            _flags.Add(fr);
            _flagFrac.Add(frac);
        }
    }

    // ───────────────────────── cập nhật mỗi frame ─────────────────────────
    void Apply(float dt)
    {
        if (_mask == null) return;

        _root.localScale = Vector3.one * scale;

        float fillW = fillRight - fillLeft;
        float p = Mathf.Clamp01(progress);

        // thanh xanh: lớn dần từ phải sang trái
        _mask.sizeDelta = new Vector2(p * fillW, _h);

        // đầu zombie ở mép trái của thanh xanh
        float headX = X(fillRight - p * fillW);
        _headBase = new Vector2(headX, 1f);

        Vector2 pos = _headBase;
        float rot = 0f;
        Vector3 sc = Vector3.one;

        if (!_introDone && _intro != null)
        {
            _introTime += dt;
            float f = _introTime * _introFps;
            int last = _intro.Length - 1;
            if (f >= last) { f = last; _introDone = true; }
            int a = Mathf.Clamp(Mathf.FloorToInt(f), 0, last);
            int b = Mathf.Min(a + 1, last);
            float t = f - a;
            RFrame F = _intro[last];
            float ix = Mathf.Lerp(_intro[a].x, _intro[b].x, t) - F.x;
            float iy = Mathf.Lerp(_intro[a].y, _intro[b].y, t) - F.y;
            float ikx = Mathf.Lerp(_intro[a].kx, _intro[b].kx, t) - F.kx;
            float isx = Mathf.Lerp(_intro[a].sx, _intro[b].sx, t) / Mathf.Max(F.sx, 0.0001f);
            float isy = Mathf.Lerp(_intro[a].sy, _intro[b].sy, t) / Mathf.Max(F.sy, 0.0001f);
            pos += new Vector2(ix, -iy) * introMoveScale;
            rot = -ikx;
            sc = new Vector3(isx, isy, 1f);
        }

        _head.anchoredPosition = pos;
        _head.localRotation = Quaternion.Euler(0, 0, rot);
        _head.localScale = sc;
    }

    // ───────────────────────── reanim (đầu zombie trồi lên) ─────────────────────────
    void PrepareIntro()
    {
        _intro = null;
        _introDone = true;
        if (reanimJson == null) return;

        try
        {
            var root = MiniJson.Parse(reanimJson.text) as Dictionary<string, object>;
            if (root == null) return;
            if (root.TryGetValue("fps", out var fpsObj)) _introFps = Mathf.Max(1f, Convert.ToSingle(fpsObj, CultureInfo.InvariantCulture));

            var tracks = root["tracks"] as List<object>;
            Dictionary<string, object> head = null;
            foreach (var t in tracks)
            {
                var d = t as Dictionary<string, object>;
                if (d != null && (string)d["name"] == "Zombie_head") { head = d; break; }
            }
            if (head == null) return;

            var tf = head["transforms"] as List<object>;
            float x = 0, y = 0, kx = 0, sx = 1, sy = 1;
            bool visible = false;
            var list = new List<RFrame>();
            foreach (var o in tf)
            {
                var d = o as Dictionary<string, object>;
                if (d.TryGetValue("x", out var v)) x = Num(v);
                if (d.TryGetValue("y", out v)) y = Num(v);
                if (d.TryGetValue("kx", out v)) kx = Num(v);
                if (d.TryGetValue("sx", out v)) sx = Num(v);
                if (d.TryGetValue("sy", out v)) sy = Num(v);
                if (d.TryGetValue("f", out v)) visible = Num(v) >= 0f;
                if (visible) list.Add(new RFrame { x = x, y = y, kx = kx, sx = sx, sy = sy });
            }
            if (list.Count >= 2) _intro = list.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ZombieProgressBar] Không đọc được reanim, bỏ qua hiệu ứng intro: " + e.Message);
        }
    }

    void StartIntro()
    {
        _introTime = 0f;
        _introDone = !(playHeadIntro && _intro != null);
    }

    static float Num(object o) { return Convert.ToSingle(o, CultureInfo.InvariantCulture); }

    // ───────────────────────── helpers ─────────────────────────
    float X(float spritePx) { return spritePx - _w * 0.5f; }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(string name, RectTransform parent, Sprite spr)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = spr;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Cắt sprite từ atlas. Toạ độ r theo JSON (gốc trên-trái), Unity cần gốc dưới-trái.</summary>
    Sprite Cut(RectInt r, Vector2 pivot)
    {
        var rect = new Rect(r.x, atlasTexture.height - r.y - r.height, r.width, r.height);
        return Sprite.Create(atlasTexture, rect, pivot, 100f, 0, SpriteMeshType.FullRect);
    }

    /// <summary>Đọc atlas JSON: id (bỏ đuôi "|960") -> atlasRect.</summary>
    static Dictionary<string, RectInt> ParseAtlas(string json)
    {
        var map = new Dictionary<string, RectInt>();
        var root = MiniJson.Parse(json) as Dictionary<string, object>;
        var list = root["resources"] as List<object>;
        foreach (var o in list)
        {
            var res = o as Dictionary<string, object>;
            if (res == null || !res.ContainsKey("atlasRect")) continue;
            var attr = res["attributes"] as Dictionary<string, object>;
            var ar = res["atlasRect"] as Dictionary<string, object>;
            string id = (string)attr["id"];
            int bar = id.IndexOf('|');
            if (bar >= 0) id = id.Substring(0, bar);
            map[id] = new RectInt((int)Num(ar["x"]), (int)Num(ar["y"]), (int)Num(ar["width"]), (int)Num(ar["height"]));
        }
        return map;
    }
}

/// <summary>Parser JSON nhỏ gọn (tránh JsonUtility vì cần biết field nào "có mặt" trong reanim).</summary>
static class MiniJson
{
    public static object Parse(string text)
    {
        int i = 0;
        text = text.TrimStart('\uFEFF');
        return Value(text, ref i);
    }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{') return Obj(s, ref i);
        if (c == '[') return Arr(s, ref i);
        if (c == '"') return Str(s, ref i);
        if (c == 't') { i += 4; return true; }
        if (c == 'f') { i += 5; return false; }
        if (c == 'n') { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
    }

    static Dictionary<string, object> Obj(string s, ref int i)
    {
        var d = new Dictionary<string, object>();
        i++; Ws(s, ref i);
        if (s[i] == '}') { i++; return d; }
        while (true)
        {
            Ws(s, ref i);
            string key = Str(s, ref i);
            Ws(s, ref i); i++;                       // ':'
            d[key] = Value(s, ref i);
            Ws(s, ref i);
            if (s[i] == ',') { i++; continue; }
            i++; break;                              // '}'
        }
        return d;
    }

    static List<object> Arr(string s, ref int i)
    {
        var l = new List<object>();
        i++; Ws(s, ref i);
        if (s[i] == ']') { i++; return l; }
        while (true)
        {
            l.Add(Value(s, ref i));
            Ws(s, ref i);
            if (s[i] == ',') { i++; continue; }
            i++; break;                              // ']'
        }
        return l;
    }

    static string Str(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++;                                         // mở "
        while (s[i] != '"')
        {
            if (s[i] == '\\')
            {
                i++;
                switch (s[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                        i += 4; break;
                    default: sb.Append(s[i]); break;
                }
            }
            else sb.Append(s[i]);
            i++;
        }
        i++;                                         // đóng "
        return sb.ToString();
    }
}