using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chạy animation mặt trời PvZ1 trên UI Canvas (3 Image con, không dùng SpriteRenderer).
/// Bản cập nhật: cache sprite dùng chung giữa các instance (cho object pool) + alpha hit test.
/// Yêu cầu: atlas PNG bật Read/Write nếu dùng alphaHitThreshold > 0.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SunReanimUI : MonoBehaviour
{
    public class ReanimFile { public float fps; public List<ReanimTrack> tracks; }
    public class ReanimTrack { public string name; public List<ReanimKey> transforms; }
    public class ReanimKey { public float? x, y, kx, ky, sx, sy, a; }

    struct Pose
    {
        public float x, y, kx, ky, sx, sy, a;
        public static Pose Lerp(Pose p, Pose q, float t) => new Pose
        {
            x = Mathf.Lerp(p.x, q.x, t),
            y = Mathf.Lerp(p.y, q.y, t),
            kx = Mathf.Lerp(p.kx, q.kx, t),
            ky = Mathf.Lerp(p.ky, q.ky, t),
            sx = Mathf.Lerp(p.sx, q.sx, t),
            sy = Mathf.Lerp(p.sy, q.sy, t),
            a = Mathf.Lerp(p.a, q.a, t)
        };
    }

    [Serializable]
    public class AtlasSprite
    {
        public string trackName;
        public int ax, ay, aw, ah; // toạ độ gốc TRÊN-TRÁI trong file cắt json
    }

    class TrackRuntime
    {
        public RectTransform rt;
        public Image img;
        public Pose[] poses;
    }

    [Header("Assets")]
    public Texture2D atlasTexture;
    public TextAsset reanimJson;

    [Header("Sprite trong atlas")]
    public List<AtlasSprite> sprites = new List<AtlasSprite>
    {
        new AtlasSprite { trackName = "Sun1", ax = 1041, ay = 1646, aw = 39,  ah = 39  },
        new AtlasSprite { trackName = "Sun2", ax = 1557, ay = 799,  aw = 82,  ah = 85  },
        new AtlasSprite { trackName = "Sun3", ax = 1360, ay = 92,   aw = 125, ah = 124 },
    };

    [Header("Playback")]
    public bool playOnAwake = true;
    public bool loop = true;
    public float speed = 1f;
    public bool useUnscaledTime = false;
    public float unitScale = 2f;

    [Header("Click / Hit test")]
    [Tooltip("> 0: chỉ pixel có alpha >= ngưỡng này mới bắt click (cần atlas bật Read/Write). 0 = tắt raycast.")]
    [Range(0f, 1f)] public float alphaHitThreshold = 0.1f;

    static readonly Dictionary<string, Sprite> s_SpriteCache = new Dictionary<string, Sprite>();

    readonly List<TrackRuntime> _tracks = new List<TrackRuntime>();
    float _fps = 12f;
    int _frameCount;
    float _time;
    bool _playing;
    bool _built;

    void Awake()
    {
        Build();
        _playing = playOnAwake;
        Apply(0f);
    }

    public void Play() { _playing = true; }
    public void Stop() { _playing = false; }

    /// <summary>Chạy lại từ đầu (gọi khi lấy từ pool ra).</summary>
    public void Restart()
    {
        if (!_built) Build();
        _time = 0f;
        _playing = true;
        Apply(0f);
    }

    Sprite GetSprite(AtlasSprite info)
    {
        string key = atlasTexture.GetInstanceID() + "_" + info.trackName;
        if (s_SpriteCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var rect = new Rect(info.ax, atlasTexture.height - info.ay - info.ah, info.aw, info.ah);
        var sp = Sprite.Create(atlasTexture, rect, new Vector2(0f, 1f), 100f, 0, SpriteMeshType.FullRect);
        sp.name = info.trackName;
        s_SpriteCache[key] = sp;
        return sp;
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        if (alphaHitThreshold > 0f && !atlasTexture.isReadable)
            Debug.LogWarning("Atlas chưa bật Read/Write -> alpha hit test sẽ không hoạt động đúng. " +
                             "Chọn texture > Inspector > bật 'Read/Write'.", atlasTexture);

        var file = JsonConvert.DeserializeObject<ReanimFile>(reanimJson.text);
        _fps = file.fps > 0 ? file.fps : 12f;
        _frameCount = file.tracks[0].transforms.Count;

        foreach (var track in file.tracks)
        {
            var info = sprites.Find(s => s.trackName == track.name);
            if (info == null) { Debug.LogWarning($"Thiếu sprite cho track {track.name}"); continue; }

            var go = new GameObject(track.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(info.aw, info.ah);

            var img = go.GetComponent<Image>();
            img.sprite = GetSprite(info);
            img.raycastTarget = alphaHitThreshold > 0f;
            if (alphaHitThreshold > 0f) img.alphaHitTestMinimumThreshold = alphaHitThreshold;

            _tracks.Add(new TrackRuntime { rt = rt, img = img, poses = ResolvePoses(track) });
        }
    }

    static Pose[] ResolvePoses(ReanimTrack track)
    {
        var result = new Pose[track.transforms.Count];
        var cur = new Pose { x = 0, y = 0, kx = 0, ky = 0, sx = 1, sy = 1, a = 1 };
        for (int i = 0; i < result.Length; i++)
        {
            var k = track.transforms[i];
            if (k.x.HasValue) cur.x = k.x.Value;
            if (k.y.HasValue) cur.y = k.y.Value;
            if (k.kx.HasValue) cur.kx = k.kx.Value;
            if (k.ky.HasValue) cur.ky = k.ky.Value;
            if (k.sx.HasValue) cur.sx = k.sx.Value;
            if (k.sy.HasValue) cur.sy = k.sy.Value;
            if (k.a.HasValue) cur.a = k.a.Value;
            result[i] = cur;
        }
        return result;
    }

    void Update()
    {
        if (!_playing || _frameCount < 2) return;
        _time += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * speed;
        Apply(_time);
    }

    void Apply(float time)
    {
        float span = _frameCount - 1; // frame cuối trùng frame đầu => chu kỳ kín
        float f = time * _fps;
        if (loop) f = Mathf.Repeat(f, span);
        else if (f >= span) { f = span; _playing = false; }

        int i0 = Mathf.Min(Mathf.FloorToInt(f), _frameCount - 1);
        int i1 = Mathf.Min(i0 + 1, _frameCount - 1);
        float t = f - i0;

        foreach (var tr in _tracks)
        {
            var p = Pose.Lerp(tr.poses[i0], tr.poses[i1], t);
            tr.rt.anchoredPosition = new Vector2(p.x, -p.y) * unitScale;
            tr.rt.localRotation = Quaternion.Euler(0f, 0f, -p.kx);
            tr.rt.localScale = new Vector3(p.sx, p.sy, 1f);
            var c = tr.img.color; c.a = p.a; tr.img.color = c;
        }
    }
}