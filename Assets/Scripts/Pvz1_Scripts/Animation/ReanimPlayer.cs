using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Loading bar chuẩn PvZ 1: 
/// - Thời gian chạy hoàn thành (hoặc demo) là 7 giây.
/// - Logic chuyển động: Khởi đầu chậm ở 1-2s đầu, tăng tốc dần về đích.
/// - Scale giới hạn vừa phải, thu nhỏ dần theo tiến độ và đạt chuẩn khi đến đích.
/// - Nắp cuộn xoay tròn tại chỗ, bám sát mặt đất và tự động ẩn đi khi hoàn thành.
/// - Hỗ trợ kích hoạt GameObject theo các mốc % quãng đường lăn thực tế của nắp cuộn.
/// </summary>
public class PvzLoadingBar : MonoBehaviour
{
    [System.Serializable]
    public struct ProgressMilestone
    {
        [Tooltip("Mốc phần trăm quãng đường lăn (từ 0 đến 100), ví dụ: 20 là khi cuộn cỏ đi được 20% quãng đường")]
        [Range(0f, 100f)] public float percentThreshold;

        [Tooltip("GameObject sẽ được bật (SetActive(true)) khi nắp cuộn đi đến mốc này")]
        public GameObject targetGameObject;
    }

    [Header("Dữ liệu")]
    public Texture2D atlasTexture;          // LoadingBar_Loading_960_00.png (1024x256)
    public TextAsset atlasJson;             // LoadingBar_Loading_960.json
    public TextAsset reanimJson;              // sodroll.reanim.compiled.reanim.json

    [Header("Layout (đơn vị: pixel của ảnh 960, trục y hướng xuống)")]
    public float pixelsPerUnit = 100f;
    [Tooltip("Reanim dùng toạ độ gốc 640, ảnh là 960 => nhân 1.5")]
    public float positionScale = 1.5f;
    public Vector2 dirtOffset = new Vector2(0f, 262f);
    public Vector2 grassOffset = new Vector2(7f, 272f);

    [Header("Cố định vị trí nắp cuộn cỏ (SodRollCap)")]
    [Tooltip("Toạ độ Y của mặt đất để nắp cuộn lăn chuẩn trên đó")]
    public float fixedGroundY = 265f;
    [Tooltip("Tinh chỉnh thêm X, Y cho nắp cuộn cỏ")]
    public Vector2 sodRollOffset = new Vector2(0f, 0f);
    [Tooltip("Giới hạn scale nhỏ nhất để nắp cuộn không bị quá bé (ví dụ: 0.65)")]
    public float minScaleLimit = 0.65f;

    public int sortingOrderBase = 0;

    [Header("Tiến độ & Thời gian")]
    [Range(0f, 1f)] public float progress;
    public float smoothSpeed = 1.5f;
    public bool autoPlayDemo = true;
    [Tooltip("Thời gian chạy demo hoàn thành (giây)")]
    public float demoDuration = 7f;

    [Header("Quản lý mốc tiến độ theo quãng đường lăn")]
    [Tooltip("True: Sử dụng quãng đường thực tế của nắp cuộn để active GameObject. False: Không sử dụng.")]
    public bool useMilestones = true;

    [Tooltip("Danh sách 5 mốc % quãng đường và GameObject tương ứng.")]
    public ProgressMilestone[] progressMilestones = new ProgressMilestone[5];

    public float DisplayedProgress { get; private set; }
    public bool IsFinished => DisplayedProgress >= 0.9999f;

    struct FrameData { public float x, y, sx, sy, kx; }
    class Track { public string name; public FrameData[] frames; }

    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    readonly List<Track> tracks = new List<Track>();
    Transform maskTf;
    float grassW, grassH;
    float demoTime;
    Track sodRollTrack;
    Track sodRollCapTrack;
    float capW, capH;
    Transform capTf;
    Transform spriteHolderTf;

    // Lưu lại vị trí bắt đầu và kết thúc của nắp cuộn dựa trên reanim data để tính % quãng đường chuẩn xác
    private float startRollX = 0f;
    private float endRollX = 0f;

    void Awake()
    {
        BuildSprites();
        BuildStatic();
        BuildTracks();

        // Ban đầu nếu bật useMilestones thì tắt tất cả các GameObject mốc để chờ kích hoạt
        if (useMilestones && progressMilestones != null)
        {
            foreach (var milestone in progressMilestones)
            {
                if (milestone.targetGameObject != null)
                    milestone.targetGameObject.SetActive(false);
            }
        }

        Apply(0f);
    }

    // ---------- 1. Cắt sprite từ atlas ----------
    void BuildSprites()
    {
        var root = JObject.Parse(atlasJson.text);
        foreach (var r in (JArray)root["resources"])
        {
            var rect = r["atlasRect"];
            if (rect == null) continue;
            string id = (string)r["attributes"]["id"];
            id = id.Split('|')[0].ToUpperInvariant();
            float x = (float)rect["x"], y = (float)rect["y"];
            float w = (float)rect["width"], h = (float)rect["height"];
            var uRect = new Rect(x, atlasTexture.height - y - h, w, h);
            sprites[id] = Sprite.Create(atlasTexture, uRect, new Vector2(0f, 1f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    SpriteRenderer MakeSR(string name, Sprite s, Vector2 pos960, int order, Vector2 pivot = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(pos960.x, -pos960.y, 0f) / pixelsPerUnit;
        var sr = go.AddComponent<SpriteRenderer>();

        if (pivot != default && s != null)
        {
            Rect r = s.rect;
            sr.sprite = Sprite.Create(s.texture, r, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
        else
        {
            sr.sprite = s;
        }

        sr.sortingOrder = sortingOrderBase + order;
        return sr;
    }

    // ---------- 2. Nền đất + cỏ có mask ----------
    void BuildStatic()
    {
        MakeSR("Dirt", sprites["IMAGE_LOADBAR_DIRT"], dirtOffset, 0);

        var grass = MakeSR("Grass", sprites["IMAGE_LOADBAR_GRASS"], grassOffset, 1);
        grass.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        grassW = grass.sprite.rect.width;
        grassH = grass.sprite.rect.height;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        var m = new GameObject("GrassMask");
        m.transform.SetParent(transform, false);
        m.transform.localPosition = new Vector3(grassOffset.x, -(grassOffset.y + grassH * 0.5f), 0f) / pixelsPerUnit;
        var mask = m.AddComponent<SpriteMask>();
        mask.sprite = white;
        maskTf = m.transform;
    }

    // ---------- 3. Đọc reanim ----------
    void BuildTracks()
    {
        var root = JObject.Parse(reanimJson.text);
        foreach (JObject t in (JArray)root["tracks"])
        {
            string name = (string)t["name"];
            var arr = (JArray)t["transforms"];
            var frames = new FrameData[arr.Count];
            var cur = new FrameData { sx = 1f, sy = 1f };
            for (int i = 0; i < arr.Count; i++)
            {
                var f = (JObject)arr[i];
                if (f["x"] != null) cur.x = (float)f["x"];
                if (f["y"] != null) cur.y = (float)f["y"];
                if (f["sx"] != null) cur.sx = (float)f["sx"];
                if (f["sy"] != null) cur.sy = (float)f["sy"];
                if (f["kx"] != null) cur.kx = (float)f["kx"];
                frames[i] = cur;
            }

            if (name == "SodRoll")
            {
                sodRollTrack = new Track { name = name, frames = frames };
            }
            else if (name == "SodRollCap")
            {
                sodRollCapTrack = new Track { name = name, frames = frames };
            }
        }

        // Lấy tọa độ bắt đầu và kết thúc từ track của nắp cuộn cỏ để tính khoảng cách di chuyển thực tế
        if (sodRollTrack != null && sodRollTrack.frames.Length > 0)
        {
            startRollX = sodRollTrack.frames[0].x * positionScale;
            endRollX = sodRollTrack.frames[sodRollTrack.frames.Length - 1].x * positionScale;
        }

        var capSprite = sprites["IMAGE_REANIM_LOAD_SODROLLCAP"];
        capW = capSprite.rect.width;
        capH = capSprite.rect.height;

        var parentGo = new GameObject("SodRollCap");
        parentGo.transform.SetParent(transform, false);

        var childGo = new GameObject("SpriteHolder");
        childGo.transform.SetParent(parentGo.transform, false);

        var sr = childGo.AddComponent<SpriteRenderer>();
        Rect r = capSprite.rect;
        sr.sprite = Sprite.Create(capSprite.texture, r, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = sortingOrderBase + 3;

        capTf = parentGo.transform;
        spriteHolderTf = childGo.transform;
    }

    // ---------- 4. Chạy ----------
    void Update()
    {
        if (autoPlayDemo)
        {
            demoTime += Time.deltaTime;
            progress = Mathf.Clamp01(demoTime / demoDuration);
        }
        DisplayedProgress = Mathf.MoveTowards(DisplayedProgress, progress, smoothSpeed * Time.deltaTime);
        Apply(DisplayedProgress);
    }

    // Kiểm tra và bật các GameObject dựa theo % quãng đường thực tế nắp cuộn đã đi được
    void CheckMilestones(float currentRollX)
    {
        if (progressMilestones == null || Mathf.Approximately(startRollX, endRollX)) return;

        // Tính tỷ lệ phần trăm quãng đường nắp cuộn đã đi qua (từ 0 đến 100)
        float distanceTraveledPercent = Mathf.InverseLerp(startRollX, endRollX, currentRollX) * 100f;

        foreach (var milestone in progressMilestones)
        {
            if (milestone.targetGameObject != null)
            {
                if (distanceTraveledPercent >= milestone.percentThreshold)
                {
                    if (!milestone.targetGameObject.activeSelf)
                    {
                        milestone.targetGameObject.SetActive(true);
                    }
                }
            }
        }
    }

    FrameData SampleTrack(Track track, float p)
    {
        if (track == null || track.frames.Length == 0) return default;
        float fi = Mathf.Clamp01(p) * (track.frames.Length - 1);
        int i0 = Mathf.FloorToInt(fi);
        int i1 = Mathf.Min(i0 + 1, track.frames.Length - 1);
        float k = fi - i0;
        FrameData a = track.frames[i0], b = track.frames[i1];
        return new FrameData
        {
            x = Mathf.Lerp(a.x, b.x, k),
            y = Mathf.Lerp(a.y, b.y, k),
            sx = Mathf.Lerp(a.sx, b.sx, k),
            sy = Mathf.Lerp(a.sy, b.sy, k),
            kx = Mathf.Lerp(a.kx, b.kx, k)
        };
    }

    void Apply(float rawProgress)
    {
        // Tạo đường cong gia tốc: 1-2s đầu chạy rất chậm, sau đó tăng tốc dần về cuối (Ease-In)
        float p = Mathf.Pow(rawProgress, 1.8f);

        float currentRollX = startRollX;

        if (capTf != null && spriteHolderTf != null)
        {
            // Kiểm tra nếu đã chạy đến đích thì ẩn nắp cuộn cỏ đi
            if (rawProgress >= 0.9999f)
            {
                if (capTf.gameObject.activeSelf)
                    capTf.gameObject.SetActive(false);

                currentRollX = endRollX;
            }
            else
            {
                if (!capTf.gameObject.activeSelf)
                    capTf.gameObject.SetActive(true);

                FrameData rollFrame = SampleTrack(sodRollTrack, p);
                FrameData capFrame = SampleTrack(sodRollCapTrack, p);

                float targetMinLimit = Mathf.Lerp(1f, minScaleLimit, p);
                float clampedSx = Mathf.Max(capFrame.sx, targetMinLimit);
                float clampedSy = Mathf.Max(capFrame.sy, targetMinLimit);

                // 1. Tọa độ X di chuyển, Y ghim cố định xuống mặt đất
                currentRollX = (rollFrame.x * positionScale);
                float finalX = currentRollX + sodRollOffset.x;
                float finalY = fixedGroundY + sodRollOffset.y;
                capTf.localPosition = new Vector3(finalX, -finalY, 0f) / pixelsPerUnit;

                // 2. Scale nắp cuộn với mức giới hạn mượt mà theo tiến độ
                capTf.localScale = new Vector3(clampedSx, clampedSy, 1f);

                // 3. Bù trừ bán kính tâm để chân đế luôn bám sát mặt đất khi scale
                float currentRadiusY = (capH * 0.5f * clampedSy) / pixelsPerUnit;
                spriteHolderTf.localPosition = new Vector3(0f, currentRadiusY, 0f);

                // 4. Xoay tròn bánh xe tại chỗ đúng chiều
                spriteHolderTf.localRotation = Quaternion.Euler(0f, 0f, -capFrame.kx);
            }
        }

        // Kích hoạt mốc dựa trên quãng đường thực tế nắp cuộn đã đi nếu được bật
        if (useMilestones)
        {
            CheckMilestones(currentRollX);
        }

        // Tính toán phần cỏ lộ ra bám theo vị trí thực tế của nắp cuộn
        float edge;
        if (rawProgress >= 0.9999f || capTf == null) edge = grassW;
        else
        {
            Vector3 centerWorld = capTf.TransformPoint(new Vector3(0f, capH * 0.5f, 0f) / pixelsPerUnit);
            Vector3 centerLocal = transform.InverseTransformPoint(centerWorld);
            edge = centerLocal.x * pixelsPerUnit - grassOffset.x;
        }
        edge = Mathf.Clamp(edge, 0f, grassW);
        maskTf.localScale = new Vector3(edge / pixelsPerUnit, grassH / pixelsPerUnit, 1f);
    }

    public void SetProgress(float p) => progress = Mathf.Clamp01(p);
}