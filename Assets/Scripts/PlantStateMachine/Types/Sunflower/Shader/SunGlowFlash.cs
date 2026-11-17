using System.Collections;
using UnityEngine;
using PlantReanimation.Controllers.Sunflower;

/// <summary>
/// Vầng sáng vàng bám theo mặt cây. Vị trí = vị trí cố định của object mặt
/// + độ đung đưa lấy từ dữ liệu animation (không phụ thuộc Transform có chuyển động hay không).
/// </summary>
[DefaultExecutionOrder(1000)]
public class SunGlowFlash : MonoBehaviour
{
    [Header("Bám theo mặt cây")]
    [Tooltip("Object chứa các sprite của cây. Trống = object này.")]
    [SerializeField] private Transform searchRoot;

    [Tooltip("Tên (hoặc một phần tên) của object mặt, dùng làm gốc vị trí.")]
    [SerializeField] private string targetNameContains = "anim_idle";

    [Tooltip("Offset so với gốc (đơn vị world). X âm = lệch trái, Y dương = lên cao.")]
    [SerializeField] private Vector2 offset = new Vector2(-0.11f, 0.92f);

    [SerializeField] private bool offsetFollowsRotation = false;

    [Header("Đung đưa theo dữ liệu animation")]
    [Tooltip("Controller của cây. Trống = tự tìm trong searchRoot.")]
    [SerializeField] private SunflowerController controller;

    [Tooltip("Bật: cộng độ đung đưa của đầu lấy từ animation. " +
             "Tắt nếu object mặt đã tự di chuyển (tránh cộng đôi).")]
    [SerializeField] private bool useAnimationData = true;

    [Tooltip("Đổi 1 đơn vị Reanim ra bao nhiêu đơn vị world. " +
             "Vầng sáng đung đưa quá ít thì tăng, quá nhiều thì giảm.")]
    [SerializeField, Min(0f)] private float reanimToWorld = 0.01f;

    [Tooltip("Reanim có trục Y hướng xuống, Unity hướng lên. " +
             "Nếu vầng sáng đung đưa ngược chiều dọc thì bỏ tick.")]
    [SerializeField] private bool flipY = true;

    [SerializeField] private Vector2 headMoveDebug;

    [Header("Hình dạng")]
    [SerializeField, Min(0.01f)] private float size = 1.2f;
    [SerializeField, Min(1f)] private float peakScaleMultiplier = 1.25f;

    [Header("Màu")]
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.35f, 1f);
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.9f;

    [Header("Sorting (phải cao hơn cây)")]
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = 100;

    [Header("Test")]
    [Range(0f, 1f)] public float intensityTest = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmo = true;

    private SpriteRenderer glowRenderer;
    private Transform glowTransform;
    private Texture2D glowTexture;
    private Sprite glowSprite;
    private Material glowMaterial;

    private Transform target;
    private float nextFindTime;
    private bool warnedNotFound;
    private float intensity;

    /// <summary>Vị trí world hiện tại của điểm neo (đã cộng offset + đung đưa). Dùng làm điểm sinh sun.</summary>
    public Vector3 AnchorWorldPosition { get; private set; }

    private void Awake()
    {
        if (searchRoot == null)
            searchRoot = transform;

        if (controller == null)
            controller = searchRoot.GetComponentInChildren<SunflowerController>(true);

        AnchorWorldPosition = transform.position;

        Build();
        Apply(0f);
    }

    private void OnDestroy()
    {
        if (glowSprite != null) Destroy(glowSprite);
        if (glowTexture != null) Destroy(glowTexture);
        if (glowMaterial != null) Destroy(glowMaterial);
    }

    private void Build()
    {
        GameObject go = new GameObject("SunGlow");
        glowTransform = go.transform;
        glowTransform.SetParent(transform, false);

        const int res = 64;
        glowTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        glowTexture.wrapMode = TextureWrapMode.Clamp;

        float half = (res - 1) * 0.5f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 2f);
                glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        glowTexture.Apply();

        glowSprite = Sprite.Create(
            glowTexture,
            new Rect(0, 0, res, res),
            new Vector2(0.5f, 0.5f),
            res);

        glowRenderer = go.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = glowSprite;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        glowMaterial = new Material(shader) { name = "SunGlow_Runtime" };
        glowRenderer.sharedMaterial = glowMaterial;

        if (!string.IsNullOrEmpty(sortingLayerName))
            glowRenderer.sortingLayerName = sortingLayerName;
        glowRenderer.sortingOrder = sortingOrder;
    }

    // =========================================================
    // TÌM OBJECT GỐC
    // =========================================================

    private void TryFindTarget()
    {
        if (target != null || Time.time < nextFindTime)
            return;

        nextFindTime = Time.time + 0.5f;

        if (string.IsNullOrEmpty(targetNameContains))
            return;

        Transform[] all = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];

            if (t == searchRoot || t == glowTransform)
                continue;

            if (t.name.IndexOf(targetNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = t;
                warnedNotFound = false;
                return;
            }
        }

        if (!warnedNotFound)
        {
            warnedNotFound = true;
            Debug.LogWarning(
                "[SunGlowFlash] Chưa tìm thấy object chứa tên '" + targetNameContains +
                "'. Đang dùng vị trí của " + searchRoot.name + ".",
                this);
        }
    }

    // =========================================================
    // CẬP NHẬT VỊ TRÍ MỖI FRAME
    // =========================================================

    private void LateUpdate()
    {
        TryFindTarget();

        Vector3 basePos;
        Quaternion baseRot;

        if (target != null)
        {
            basePos = target.position;
            baseRot = target.rotation;
        }
        else
        {
            basePos = searchRoot.position;
            baseRot = searchRoot.rotation;
        }

        // Cộng độ đung đưa của đầu lấy từ dữ liệu animation.
        if (useAnimationData && controller != null)
        {
            Vector2 move = controller.GetHeadMovement();
            headMoveDebug = move;

            float s = reanimToWorld * Mathf.Abs(controller.transform.lossyScale.x);
            basePos += new Vector3(move.x * s, (flipY ? -move.y : move.y) * s, 0f);
        }

        Vector3 off = new Vector3(offset.x, offset.y, 0f);
        if (offsetFollowsRotation)
            off = baseRot * off;

        AnchorWorldPosition = basePos + off;

        if (glowTransform != null)
            glowTransform.position = AnchorWorldPosition;

        Apply(intensityTest > 0f ? intensityTest : intensity);
    }

    // =========================================================
    // ĐIỀU KHIỂN ĐỘ SÁNG
    // =========================================================

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
    }

    private void Apply(float v)
    {
        if (glowRenderer == null)
            return;

        Color c = glowColor;
        c.a = v * maxAlpha;
        glowRenderer.color = c;

        float worldScale = size * Mathf.Lerp(0.8f, peakScaleMultiplier, v);
        Vector3 parentScale = transform.lossyScale;
        float px = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
        float py = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;

        glowTransform.localScale = new Vector3(worldScale / px, worldScale / py, 1f);

        glowRenderer.enabled = v > 0.001f;
    }

    // =========================================================
    // TEST & DEBUG
    // =========================================================

    [ContextMenu("Test Flash (không cần chờ)")]
    private void TestFlash()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            SetIntensity(t / 1.2f);
            yield return null;
        }

        t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            SetIntensity(1f - t / 0.35f);
            yield return null;
        }

        SetIntensity(0f);
    }

    [ContextMenu("List Child Transforms")]
    private void ListChildTransforms()
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            Debug.Log(
                "[SunGlowFlash]   " + t.name +
                (t == target ? "   <-- GỐC VỊ TRÍ" : ""),
                t);
        }
    }

    [ContextMenu("Reset Target")]
    private void ResetTarget()
    {
        target = null;
        nextFindTime = 0f;
        warnedNotFound = false;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo || !Application.isPlaying)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(AnchorWorldPosition, 0.08f);
    }
}