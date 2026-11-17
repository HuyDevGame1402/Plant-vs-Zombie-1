using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng cuộn cỏ PvZ1 level 2, làm đúng theo logic của game gốc (Board::DrawBackdrop):
///   1. Vẽ ảnh SOD1ROW (lane giữa) ĐỦ, luôn luôn.
///   2. Vẽ đè ảnh SOD3ROW nhưng chỉ phần source rect [0, width) tính từ mép TRÁI,
///      width là SỐ NGUYÊN pixel chạy tuyến tính từ 0 đến bề rộng ảnh.
///   3. SOD3ROW lệch (-4, -116) pixel so với SOD1ROW (game: 235,149 và 239,265).
///
/// Input: PNG atlas + JSON cắt atlas. Script tự cắt ra 2 sprite lúc chạy.
/// Không dùng SpriteMask nên mép cắt luôn tròn pixel, không bị lệch nửa pixel.
///
/// Vị trí của GameObject gắn script = góc TRÁI-TRÊN của ảnh lane giữa (SOD1ROW).
/// </summary>
[ExecuteAlways]
public class GrassRevealTester : MonoBehaviour
{
    // ───────────── Dữ liệu JSON (chỉ khai báo phần cần dùng) ─────────────

    [Serializable] private class AtlasJson { public ResourceEntry[] resources; }
    [Serializable] private class ResourceEntry { public ResourceAttributes attributes; public AtlasRect atlasRect; }
    [Serializable] private class ResourceAttributes { public string id; }
    [Serializable] private class AtlasRect { public int x, y, width, height; }

    // ───────────── Inspector ─────────────

    [Header("Atlas")]
    [Tooltip("File PNG atlas (DelayLoad_BackgroundUnsodded_SodRows_960_00.png)")]
    public Texture2D atlasTexture;

    [Tooltip("File JSON cắt atlas (DelayLoad_BackgroundUnsodded_SodRows_960.json)")]
    public TextAsset atlasJson;

    [Tooltip("id (không gồm phần '|960') của ảnh lane giữa trong JSON")]
    public string row1Id = "IMAGE_SOD1ROW";

    [Tooltip("id (không gồm phần '|960') của ảnh 3 lane trong JSON")]
    public string row3Id = "IMAGE_SOD3ROW";

    [Tooltip("Pixels Per Unit dùng khi cắt sprite")]
    [Min(1f)] public float pixelsPerUnit = 100f;

    [Header("Scale (chỉnh sau)")]
    [Tooltip("Scale chung cho cả 2 ảnh, mặc định 1 1")]
    public Vector2 scale = Vector2.one;

    [Header("Reveal")]
    [Range(0f, 1f)] public float progress = 0f;

    [Header("Layout (pixel của atlas, theo game gốc)")]
    [Tooltip("Vị trí SOD3ROW so với SOD1ROW, tính bằng pixel atlas (Unity trục Y hướng lên). " +
             "Đã đo trực tiếp trên ảnh atlas: lane giữa của 2 ảnh trùng khít pixel khi SOD3ROW lệch (-4, +124).")]
    public Vector2 row3Offset = new Vector2(-4f, 124f);

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0; // SOD1ROW = order, SOD3ROW = order + 1 (vẽ đè lên)

    [Header("Animation Test")]
    public bool autoPlay = false;
    [Min(0.01f)] public float duration = 2f; // game gốc: 6000 -> 8000 ms = 2 giây, tuyến tính

    // ───────────── Public ─────────────

    /// Vị trí world X của mép đang mở (dùng để đặt cuộn đất / đất văng sau này).
    public float EdgeWorldX { get; private set; }

    /// Số pixel hiện đang hiển thị của ảnh 3 lane.
    public int RevealedPixels { get; private set; }

    // ───────────── Nội bộ ─────────────

    private const string ContainerName = "GrassContainer";

    private Transform container;
    private SpriteRenderer row1Renderer;
    private SpriteRenderer row3Renderer;

    private RectInt row1Rect;   // toạ độ atlas, gốc ở góc TRÊN-TRÁI (như JSON)
    private RectInt row3Rect;

    private readonly Dictionary<int, Sprite> row3Cache = new Dictionary<int, Sprite>();
    private Sprite row1Sprite;

    private int lastWidth = -1;
    private float elapsed;
    private bool built;
    private bool dirty;

    // ───────────── Lifecycle ─────────────

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        DestroyAll();
    }

    private void OnValidate()
    {
        // Không tạo / huỷ object trong OnValidate, chỉ đánh dấu rồi xử lý ở Update.
        dirty = true;
    }

    private void Update()
    {
        if (dirty)
        {
            dirty = false;
            Rebuild();
        }

        if (!built) return;

        if (autoPlay && Application.isPlaying)
        {
            elapsed += Time.deltaTime;
            progress = Mathf.Clamp01(elapsed / duration);
        }

        Apply();
    }

    // ───────────── Build ─────────────

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        DestroyAll();

        if (atlasTexture == null || atlasJson == null)
            return;

        AtlasJson data;
        try
        {
            data = JsonUtility.FromJson<AtlasJson>(atlasJson.text);
        }
        catch (Exception e)
        {
            Debug.LogError("[GrassRevealTester] Không đọc được JSON: " + e.Message, this);
            return;
        }

        if (data == null || data.resources == null ||
            !TryFindRect(data, row1Id, out row1Rect) ||
            !TryFindRect(data, row3Id, out row3Rect))
        {
            Debug.LogError("[GrassRevealTester] Không tìm thấy '" + row1Id + "' hoặc '" + row3Id + "' trong JSON.", this);
            return;
        }

        // Container chứa 2 ảnh, scale áp dụng ở đây.
        var cGo = new GameObject(ContainerName) { hideFlags = HideFlags.DontSave };
        container = cGo.transform;
        container.SetParent(transform, false);

        row1Renderer = CreateRenderer("SodRow1", sortingOrder);
        row3Renderer = CreateRenderer("SodRow3", sortingOrder + 1);

        // Lane giữa: sprite đầy đủ, vẽ ở gốc (góc trái-trên).
        row1Sprite = MakeSprite(row1Rect, row1Rect.width);
        row1Renderer.sprite = row1Sprite;
        row1Renderer.transform.localPosition = Vector3.zero;

        // 3 lane: lệch theo offset của game gốc. Pivot trái-trên nên đổi width không làm xê dịch.
        row3Renderer.transform.localPosition = new Vector3(
            row3Offset.x / pixelsPerUnit,
            row3Offset.y / pixelsPerUnit,
            0f);

        lastWidth = -1;
        built = true;
        Apply();
    }

    private SpriteRenderer CreateRenderer(string objName, int order)
    {
        var go = new GameObject(objName) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(container, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;
        return sr;
    }

    private static bool TryFindRect(AtlasJson data, string id, out RectInt rect)
    {
        foreach (var res in data.resources)
        {
            if (res == null || res.attributes == null || string.IsNullOrEmpty(res.attributes.id))
                continue;

            // "IMAGE_SOD1ROW|960" -> "IMAGE_SOD1ROW"
            string baseId = res.attributes.id.Split('|')[0];
            if (baseId != id) continue;

            if (res.atlasRect == null || res.atlasRect.width <= 0 || res.atlasRect.height <= 0)
                continue;

            rect = new RectInt(res.atlasRect.x, res.atlasRect.y, res.atlasRect.width, res.atlasRect.height);
            return true;
        }

        rect = default;
        return false;
    }

    /// Cắt 1 sprite từ atlas: lấy phần [0, widthPx) tính từ mép trái của rect.
    /// JSON có gốc Y ở TRÊN, Texture2D của Unity có gốc Y ở DƯỚI nên phải lật.
    private Sprite MakeSprite(RectInt jsonRect, int widthPx)
    {
        float unityY = atlasTexture.height - jsonRect.y - jsonRect.height;

        var sprite = Sprite.Create(
            atlasTexture,
            new Rect(jsonRect.x, unityY, widthPx, jsonRect.height),
            new Vector2(0f, 1f),          // pivot góc trái-trên
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private Sprite GetRow3Sprite(int widthPx)
    {
        if (!row3Cache.TryGetValue(widthPx, out Sprite s))
        {
            s = MakeSprite(row3Rect, widthPx);
            row3Cache[widthPx] = s;
        }
        return s;
    }

    // ───────────── Reveal ─────────────

    private void Apply()
    {
        if (!built || container == null || row3Renderer == null) return;

        progress = Mathf.Clamp01(progress);

        container.localScale = new Vector3(scale.x, scale.y, 1f);

        // Giống game: aWidth là số nguyên pixel, tuyến tính 0 -> bề rộng ảnh 3 lane.
        int fullW = row3Rect.width;
        int widthPx = (progress >= 1f) ? fullW : Mathf.Clamp(Mathf.FloorToInt(progress * fullW), 0, fullW);

        if (widthPx != lastWidth)
        {
            lastWidth = widthPx;

            if (widthPx > 0)
            {
                row3Renderer.sprite = GetRow3Sprite(widthPx);
                row3Renderer.enabled = true;
            }
            else
            {
                row3Renderer.sprite = null;
                row3Renderer.enabled = false;
            }
        }

        RevealedPixels = widthPx;

        // Mép mở trong world space: mép trái ảnh 3 lane + số pixel đã mở.
        float edgeLocalX = (row3Offset.x + widthPx) / pixelsPerUnit;
        EdgeWorldX = container.TransformPoint(new Vector3(edgeLocalX, 0f, 0f)).x;
    }

    // ───────────── Cleanup ─────────────

    private void DestroyAll()
    {
        built = false;

        if (container != null)
            Kill(container.gameObject);

        foreach (var kv in row3Cache)
            if (kv.Value != null) Kill(kv.Value);
        row3Cache.Clear();

        if (row1Sprite != null) Kill(row1Sprite);

        // Dọn container cũ sót lại (sau recompile trong Editor)
        var old = transform.Find(ContainerName);
        if (old != null) Kill(old.gameObject);

        container = null;
        row1Renderer = null;
        row3Renderer = null;
        row1Sprite = null;
        lastWidth = -1;
    }

    private static void Kill(UnityEngine.Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // ───────────── Public API ─────────────

    public void SetProgress(float v)
    {
        progress = Mathf.Clamp01(v);
        Apply();
    }

    public void ResetProgress()
    {
        elapsed = 0f;
        SetProgress(0f);
    }
}