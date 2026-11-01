using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Hiệu ứng trải thảm cỏ trong Level PvZ 1 (Đã bổ sung tùy chỉnh Scale và Mask Offset Y):
/// - Hỗ trợ tùy chọn loại thảm cỏ: 3 đường (IMAGE_SOD3ROW) hoặc 1 đường (IMAGE_SOD1ROW).
/// - Tùy chỉnh Custom Width Scale & Custom Height Scale để thảm cỏ phủ kín sân.
/// - Tùy chỉnh maskOffsetY để dịch chuyển vị trí Mask theo chiều dọc.
/// </summary>
public class LevelSodRollAnimation : MonoBehaviour
{
    public enum GrassType
    {
        Sod3Row, // Ảnh lớn phía trên (3 đường)
        Sod1Row  // Ảnh nhỏ phía dưới (1 đường)
    }

    [Header("Cấu hình Thảm Cỏ")]
    [Tooltip("Chọn loại thảm cỏ muốn sử dụng từ Atlas")]
    public GrassType grassType = GrassType.Sod3Row;

    [Header("Tùy chỉnh Kích thước & Co giãn (Scale)")]
    [Tooltip("Hệ số phóng to chiều ngang của thảm cỏ (giúp dải cỏ dài ra cho vừa sân)")]
    public float customWidthScale = 1.5f;
    [Tooltip("Hệ số phóng to chiều cao của thảm cỏ (giúp dải cỏ cao/dày hơn)")]
    public float customHeightScale = 1.2f;

    [Header("Dữ liệu")]
    public Texture2D atlasTexture;          // DelayLoad_BackgroundUnsodded_SodRows_960_00.png
    public TextAsset atlasJson;             // DelayLoad_BackgroundUnsodded_SodRows_960.json

    [Header("Layout (đơn vị: pixel của ảnh 960, trục y hướng xuống)")]
    public float pixelsPerUnit = 100f;
    public Vector2 grassOffset = new Vector2(0f, 0f);

    [Tooltip("Độ dịch chuyển theo chiều dọc của Mask (pixel). Số dương dịch xuống dưới, số âm dịch lên trên.")]
    public float maskOffsetY = 0f;

    public int sortingOrderBase = 0;

    [Header("Tiến độ & Thời gian")]
    [Range(0f, 1f)] public float progress;
    public float smoothSpeed = 1.5f;
    public bool autoPlayDemo = true;
    [Tooltip("Thời gian chạy hoàn thành (giây)")]
    public float demoDuration = 7f;

    public float DisplayedProgress { get; private set; }
    public bool IsFinished => DisplayedProgress >= 0.9999f;

    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    Transform maskTf;
    float grassW, grassH;
    float demoTime;
    SpriteRenderer grassSr;

    void Awake()
    {
        BuildSprites();
        BuildStatic();
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

    // ---------- 2. Tạo Thảm Cỏ và Mask che hiển thị ----------
    void BuildStatic()
    {
        string grassKey = (grassType == GrassType.Sod3Row) ? "IMAGE_SOD3ROW" : "IMAGE_SOD1ROW";

        if (!sprites.ContainsKey(grassKey))
        {
            Debug.LogError($"Không tìm thấy key {grassKey} trong Atlas JSON!");
            return;
        }

        grassSr = MakeSR("Grass", sprites[grassKey], grassOffset, 1);
        grassSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        grassW = grassSr.sprite.rect.width;
        grassH = grassSr.sprite.rect.height;

        // Áp dụng hệ số scale tùy chỉnh ngay khi khởi tạo cho thảm cỏ
        grassSr.transform.localScale = new Vector3(customWidthScale, customHeightScale, 1f);

        // Tạo Mask trắng để che/mở thảm cỏ theo tiến độ
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        var m = new GameObject("GrassMask");
        m.transform.SetParent(transform, false);

        // Cập nhật vị trí ban đầu của Mask, cộng thêm maskOffsetY (chú ý trục Y trong Unity hướng lên, còn hệ tọa độ 960 hướng xuống nên trừ dấu)
        float basePosY = -(grassOffset.y + grassH * 0.5f + maskOffsetY);
        m.transform.localPosition = new Vector3(grassOffset.x, basePosY / pixelsPerUnit, 0f);

        var mask = m.AddComponent<SpriteMask>();
        mask.sprite = white;
        maskTf = m.transform;
    }

    // ---------- 3. Chạy cập nhật animation ----------
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

    void Apply(float rawProgress)
    {
        float p = Mathf.Pow(rawProgress, 1.8f);

        if (maskTf != null)
        {
            // Cập nhật lại vị trí Y của mask theo sát maskOffsetY (phòng khi bạn thay đổi giá trị này trực tiếp trên Inspector khi game đang chạy)
            float basePosY = -(grassOffset.y + grassH * 0.5f + maskOffsetY);
            maskTf.localPosition = new Vector3(grassOffset.x, basePosY / pixelsPerUnit, 0f);

            // Tính toán chiều rộng mask dựa theo tiến độ, nhân thêm customWidthScale và customHeightScale
            float currentWidth = Mathf.Lerp(0f, grassW * customWidthScale, p);
            maskTf.localScale = new Vector3(currentWidth / pixelsPerUnit, (grassH * customHeightScale) / pixelsPerUnit, 1f);
        }
    }

    public void SetProgress(float p) => progress = Mathf.Clamp01(p);
}