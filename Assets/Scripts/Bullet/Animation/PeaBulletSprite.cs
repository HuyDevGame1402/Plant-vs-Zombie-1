
using System;
using System.Collections;
using UnityEngine;

public class PeaBulletSprite : MonoBehaviour
{
    // ------------------------------------------------------------------
    // ATLAS
    // ------------------------------------------------------------------

    [Header("Atlas")]
    [SerializeField] private Texture2D atlasTexture;
    [SerializeField] private TextAsset atlasJson;

    // ------------------------------------------------------------------
    // RENDER
    // ------------------------------------------------------------------

    [Header("Render")]
    [Tooltip("Số pixel = 1 unit")]
    [SerializeField] private float pixelsPerUnit = 100f;

    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseOrderInLayer = 0;

    // ------------------------------------------------------------------
    // FLYING
    // ------------------------------------------------------------------

    [Header("Flying")]
    [Tooltip("Scale ban đầu của đạn và shadow")]
    [SerializeField] private float originalScale = 1f;

    [Tooltip("Alpha tối đa khi bắn")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1f;

    // ------------------------------------------------------------------
    // SHADOW
    // ------------------------------------------------------------------

    [Header("Shadow")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.3f);
    [SerializeField] private Vector2 shadow2ExtraOffset = Vector2.zero;

    // ------------------------------------------------------------------
    // SPLAT
    // ------------------------------------------------------------------

    [Header("Splat (đạn vỡ)")]

    [Tooltip("Scale bắt đầu của hiệu ứng vỡ")]
    [SerializeField] private float beginScale = 0.5f;

    [Tooltip("Scale kết thúc của hiệu ứng vỡ")]
    [SerializeField] private float splatEndScale = 1.5f;

    [Tooltip("Alpha kết thúc của hiệu ứng vỡ")]
    [Range(0f, 1f)]
    [SerializeField] private float splatEndAlpha = 0f;

    [Tooltip("Thời gian hiệu ứng vỡ")]
    [SerializeField] private float splatDuration = 0.25f;

    // ------------------------------------------------------------------
    // TEST
    // ------------------------------------------------------------------

    [Header("Test")]
    [SerializeField] private bool testMode = true;

    // ------------------------------------------------------------------
    // ATLAS ID
    // ------------------------------------------------------------------

    private const string ID_PEA = "IMAGE_PROJECTILEPEA|960";
    private const string ID_SHADOWS = "IMAGE_PEA_SHADOWS|960";
    private const string ID_SPLATS = "IMAGE_PEA_SPLATS|960";

    // ------------------------------------------------------------------
    // RUNTIME
    // ------------------------------------------------------------------

    private SpriteRenderer peaSR;
    private SpriteRenderer shadowSR1;
    private SpriteRenderer shadowSR2;
    private SpriteRenderer splatSR;

    private Sprite peaSprite;
    private Sprite[] shadowSprites;
    private Sprite[] splatSprites;

    private Coroutine splatRoutine;

    // ------------------------------------------------------------------
    // JSON MODEL
    // ------------------------------------------------------------------

    [Serializable]
    private class AtlasRoot
    {
        public Res[] resources;
    }

    [Serializable]
    private class Res
    {
        public Attr attributes;
    }

    [Serializable]
    private class Attr
    {
        public string id;
        public string ax, ay, aw, ah, cols;
    }

    // ------------------------------------------------------------------
    // UNITY
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (atlasTexture == null || atlasJson == null)
        {
            Debug.LogError("[PeaBullet] Chưa kéo atlasTexture hoặc atlasJson!", this);
            enabled = false;
            return;
        }

        if (!BuildSprites())
        {
            enabled = false;
            return;
        }

        BuildRenderers();
    }

    private void OnEnable()
    {
        if (peaSR != null)
            Fire();
    }

    // ------------------------------------------------------------------
    // BUILD SPRITES
    // ------------------------------------------------------------------

    private bool BuildSprites()
    {
        atlasTexture.filterMode = FilterMode.Bilinear;

        string json = atlasJson.text;
        int brace = json.IndexOf('{');

        if (brace > 0)
            json = json.Substring(brace);

        AtlasRoot root = JsonUtility.FromJson<AtlasRoot>(json);

        if (root == null || root.resources == null)
        {
            Debug.LogError("[PeaBullet] Không đọc được JSON!", this);
            return false;
        }

        float scale = 1f;

        foreach (Res r in root.resources)
        {
            if (r.attributes != null &&
                r.attributes.id != null &&
                r.attributes.id.StartsWith("ATLASIMAGE"))
            {
                float atlasW = ParseF(r.attributes.aw);

                if (atlasW > 0f)
                    scale = atlasTexture.width / atlasW;

                break;
            }
        }

        Sprite[] peaFrames = CutFrames(root, ID_PEA, scale);

        peaSprite = peaFrames != null && peaFrames.Length > 0
            ? peaFrames[0]
            : null;

        shadowSprites = CutFrames(root, ID_SHADOWS, scale);
        splatSprites = CutFrames(root, ID_SPLATS, scale);

        if (peaSprite == null ||
            shadowSprites == null ||
            splatSprites == null)
        {
            Debug.LogError("[PeaBullet] Thiếu ID trong JSON!", this);
            return false;
        }

        return true;
    }

    private Sprite[] CutFrames(AtlasRoot root, string id, float scale)
    {
        foreach (Res r in root.resources)
        {
            Attr a = r.attributes;

            if (a == null || a.id != id)
                continue;

            int ax = ParseI(a.ax);
            int ay = ParseI(a.ay);
            int aw = ParseI(a.aw);
            int ah = ParseI(a.ah);

            int cols = Mathf.Max(1, ParseI(a.cols));

            float fw = aw / (float)cols;

            Sprite[] result = new Sprite[cols];

            for (int i = 0; i < cols; i++)
            {
                float x = (ax + fw * i) * scale;
                float w = fw * scale;
                float h = ah * scale;

                float y = atlasTexture.height - (ay * scale) - h;

                result[i] = Sprite.Create(
                    atlasTexture,
                    new Rect(x, y, w, h),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit
                );

                result[i].name = id + "_" + i;
            }

            return result;
        }

        return null;
    }

    private static int ParseI(string s)
    {
        int v;
        return int.TryParse(s, out v) ? v : 0;
    }

    private static float ParseF(string s)
    {
        float v;

        return float.TryParse(
            s,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out v
        ) ? v : 0f;
    }

    // ------------------------------------------------------------------
    // BUILD RENDERERS
    // ------------------------------------------------------------------

    private void BuildRenderers()
    {
        shadowSR1 = CreateSR(
            "Shadow1",
            shadowSprites[0],
            baseOrderInLayer
        );

        shadowSR2 = CreateSR(
            "Shadow2",
            shadowSprites[Mathf.Min(1, shadowSprites.Length - 1)],
            baseOrderInLayer
        );

        shadowSR1.transform.localPosition = shadowOffset;
        shadowSR2.transform.localPosition = shadowOffset + shadow2ExtraOffset;

        peaSR = CreateSR(
            "Pea",
            peaSprite,
            baseOrderInLayer + 1
        );

        splatSR = CreateSR(
            "Splat",
            splatSprites[0],
            baseOrderInLayer + 2
        );

        splatSR.gameObject.SetActive(false);
    }

    private SpriteRenderer CreateSR(string objName, Sprite sprite, int order)
    {
        GameObject go = new GameObject(objName);

        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();

        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;

        return sr;
    }

    // ------------------------------------------------------------------
    // FIRE
    // ------------------------------------------------------------------

    public void Fire()
    {
        if (peaSR == null)
            return;

        if (splatRoutine != null)
        {
            StopCoroutine(splatRoutine);
            splatRoutine = null;
        }

        peaSR.gameObject.SetActive(true);
        shadowSR1.gameObject.SetActive(true);
        shadowSR2.gameObject.SetActive(true);
        splatSR.gameObject.SetActive(false);

        // Reset scale đạn và shadow
        peaSR.transform.localScale = Vector3.one * originalScale;
        shadowSR1.transform.localScale = Vector3.one * originalScale;
        shadowSR2.transform.localScale = Vector3.one * originalScale;

        // Reset scale ảnh vỡ
        splatSR.transform.localScale = Vector3.one * beginScale;

        // Reset alpha
        SetAlpha(peaSR, maxAlpha);
        SetAlpha(shadowSR1, maxAlpha);
        SetAlpha(shadowSR2, maxAlpha);
        SetAlpha(splatSR, maxAlpha);
    }

    // ------------------------------------------------------------------
    // SPLAT
    // ------------------------------------------------------------------

    public void Splat()
    {
        if (peaSR == null || splatRoutine != null)
            return;

        // Ẩn đạn bay và shadow
        peaSR.gameObject.SetActive(false);
        shadowSR1.gameObject.SetActive(false);
        shadowSR2.gameObject.SetActive(false);

        // Random ảnh vỡ
        splatSR.sprite = splatSprites[
            UnityEngine.Random.Range(0, splatSprites.Length)
        ];

        splatSR.gameObject.SetActive(true);

        splatRoutine = StartCoroutine(SplatRoutine());
    }

    private IEnumerator SplatRoutine()
    {
        // Scale bắt đầu theo biến Begin Scale
        float startScale = beginScale;

        // Alpha bắt đầu
        float startAlpha = maxAlpha;

        splatSR.transform.localScale = Vector3.one * startScale;
        SetAlpha(splatSR, startAlpha);

        float duration = Mathf.Max(0.0001f, splatDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / duration);

            // Phóng to từ Begin Scale đến End Scale
            float currentScale = Mathf.Lerp(
                startScale,
                splatEndScale,
                k
            );

            splatSR.transform.localScale = Vector3.one * currentScale;

            // Alpha giảm dần
            float currentAlpha = Mathf.Lerp(
                startAlpha,
                splatEndAlpha,
                k
            );

            SetAlpha(splatSR, currentAlpha);

            yield return null;
        }

        // Đảm bảo đạt chính xác giá trị cuối
        splatSR.transform.localScale = Vector3.one * splatEndScale;
        SetAlpha(splatSR, splatEndAlpha);

        splatRoutine = null;

        if (testMode)
        {
            // Test: chỉ ẩn ảnh vỡ
            splatSR.gameObject.SetActive(false);
        }
        else
        {
            // Vỡ xong: ẩn GameObject
            gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------
    // ALPHA
    // ------------------------------------------------------------------

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null)
            return;

        Color c = sr.color;
        c.a = a;

        sr.color = c;
    }

    // ------------------------------------------------------------------
    // TEST
    // ------------------------------------------------------------------

    [ContextMenu("Test Fire")]
    private void TestFire()
    {
        if (Application.isPlaying)
            Fire();
    }

    [ContextMenu("Test Splat")]
    private void TestSplat()
    {
        if (Application.isPlaying)
            Splat();
    }

    private void OnGUI()
    {
        if (!testMode)
            return;

        if (GUI.Button(new Rect(10, 10, 160, 40), "Fire (hiện đạn)"))
            Fire();

        if (GUI.Button(new Rect(10, 60, 160, 40), "Splat (test vỡ)"))
            Splat();
    }
}
