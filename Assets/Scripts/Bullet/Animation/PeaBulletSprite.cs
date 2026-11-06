
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

public class PeaBulletSprite : MonoBehaviour
{
    // ---------------------------------------------------------
    // ATLAS
    // ---------------------------------------------------------

    [Header("Atlas")]
    [SerializeField] private Texture2D atlasTexture;
    [SerializeField] private TextAsset atlasJson;

    // ---------------------------------------------------------
    // RENDER
    // ---------------------------------------------------------

    [Header("Render")]
    [SerializeField] private float pixelsPerUnit = 100f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseOrderInLayer = 0;

    // ---------------------------------------------------------
    // FLYING
    // ---------------------------------------------------------

    [Header("Flying")]
    [SerializeField] private float originalScale = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1f;

    // ---------------------------------------------------------
    // SHADOW
    // ---------------------------------------------------------

    [Header("Shadow")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.3f);
    [SerializeField] private Vector2 shadow2ExtraOffset = Vector2.zero;

    // ---------------------------------------------------------
    // SPLAT
    // ---------------------------------------------------------

    [Header("Splat")]
    [SerializeField] private float beginScale = 0.5f;
    [SerializeField] private float splatEndScale = 1.5f;

    [Range(0f, 1f)]
    [SerializeField] private float splatEndAlpha = 0f;

    [Min(0.01f)]
    [SerializeField] private float splatDuration = 0.25f;

    // ---------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------

    public event Action OnSplatCompleted;

    // ---------------------------------------------------------
    // ATLAS ID
    // ---------------------------------------------------------

    private const string ID_PEA = "IMAGE_PROJECTILEPEA|960";
    private const string ID_SHADOWS = "IMAGE_PEA_SHADOWS|960";
    private const string ID_SPLATS = "IMAGE_PEA_SPLATS|960";

    // ---------------------------------------------------------
    // RUNTIME
    // ---------------------------------------------------------

    private SpriteRenderer peaSR;
    private SpriteRenderer shadowSR1;
    private SpriteRenderer shadowSR2;
    private SpriteRenderer splatSR;

    private Sprite peaSprite;
    private Sprite[] shadowSprites;
    private Sprite[] splatSprites;

    private Coroutine splatRoutine;

    // ---------------------------------------------------------
    // JSON
    // ---------------------------------------------------------

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
        public string ax;
        public string ay;
        public string aw;
        public string ah;
        public string cols;
    }

    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------

    private void Awake()
    {
        if (atlasTexture == null || atlasJson == null)
        {
            Debug.LogError(
                "[PeaBulletSprite] Chưa gán Atlas!",
                this
            );

            enabled = false;
            return;
        }

        if (!BuildSprites())
        {
            enabled = false;
            return;
        }

        BuildRenderers();

        ResetVisual();
    }

    private void OnEnable()
    {
        if (peaSR != null)
            Fire();
    }

    private void OnDisable()
    {
        StopSplatRoutine();
    }

    // ---------------------------------------------------------
    // BUILD SPRITES
    // ---------------------------------------------------------

    private bool BuildSprites()
    {
        atlasTexture.filterMode = FilterMode.Bilinear;

        string json = atlasJson.text;

        int brace = json.IndexOf('{');

        if (brace > 0)
            json = json.Substring(brace);

        AtlasRoot root = JsonUtility.FromJson<AtlasRoot>(json);

        if (root == null || root.resources == null)
            return false;

        float scale = 1f;

        foreach (Res resource in root.resources)
        {
            if (resource.attributes == null)
                continue;

            if (resource.attributes.id == null)
                continue;

            if (resource.attributes.id.StartsWith("ATLASIMAGE"))
            {
                float atlasWidth = ParseF(resource.attributes.aw);

                if (atlasWidth > 0f)
                    scale = atlasTexture.width / atlasWidth;

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
            shadowSprites.Length == 0 ||
            splatSprites == null ||
            splatSprites.Length == 0)
        {
            Debug.LogError(
                "[PeaBulletSprite] Thiếu Sprite trong Atlas!",
                this
            );

            return false;
        }

        return true;
    }

    private Sprite[] CutFrames(
        AtlasRoot root,
        string id,
        float scale
    )
    {
        foreach (Res resource in root.resources)
        {
            Attr a = resource.attributes;

            if (a == null || a.id != id)
                continue;

            int ax = ParseI(a.ax);
            int ay = ParseI(a.ay);
            int aw = ParseI(a.aw);
            int ah = ParseI(a.ah);

            int cols = Mathf.Max(1, ParseI(a.cols));

            float frameWidth = aw / (float)cols;

            Sprite[] result = new Sprite[cols];

            for (int i = 0; i < cols; i++)
            {
                float x = (ax + frameWidth * i) * scale;
                float width = frameWidth * scale;
                float height = ah * scale;

                float y = atlasTexture.height - (ay * scale) - height;

                result[i] = Sprite.Create(
                    atlasTexture,
                    new Rect(x, y, width, height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit
                );

                result[i].name = id + "_" + i;
            }

            return result;
        }

        return null;
    }

    private static int ParseI(string value)
    {
        int result;

        return int.TryParse(value, out result)
            ? result
            : 0;
    }

    private static float ParseF(string value)
    {
        float result;

        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result
        ) ? result : 0f;
    }

    // ---------------------------------------------------------
    // BUILD RENDERERS
    // ---------------------------------------------------------

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

        shadowSR2.transform.localPosition =
            shadowOffset + shadow2ExtraOffset;

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

    private SpriteRenderer CreateSR(
        string objectName,
        Sprite sprite,
        int order
    )
    {
        GameObject go = new GameObject(objectName);

        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();

        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;

        return sr;
    }

    // ---------------------------------------------------------
    // FIRE
    // ---------------------------------------------------------

    public void Fire()
    {
        if (peaSR == null)
            return;

        StopSplatRoutine();

        peaSR.sprite = peaSprite;

        peaSR.gameObject.SetActive(true);
        shadowSR1.gameObject.SetActive(true);
        shadowSR2.gameObject.SetActive(true);
        splatSR.gameObject.SetActive(false);

        peaSR.transform.localScale =
            Vector3.one * originalScale;

        shadowSR1.transform.localScale =
            Vector3.one * originalScale;

        shadowSR2.transform.localScale =
            Vector3.one * originalScale;

        splatSR.transform.localScale =
            Vector3.one * beginScale;

        SetAlpha(peaSR, maxAlpha);
        SetAlpha(shadowSR1, maxAlpha);
        SetAlpha(shadowSR2, maxAlpha);
        SetAlpha(splatSR, maxAlpha);
    }

    // ---------------------------------------------------------
    // SPLAT
    // ---------------------------------------------------------

    public void Splat()
    {
        if (peaSR == null || splatRoutine != null)
            return;

        peaSR.gameObject.SetActive(false);
        shadowSR1.gameObject.SetActive(false);
        shadowSR2.gameObject.SetActive(false);

        splatSR.sprite = splatSprites[
            UnityEngine.Random.Range(0, splatSprites.Length)
        ];

        splatSR.gameObject.SetActive(true);

        splatRoutine = StartCoroutine(SplatRoutine());
    }

    private IEnumerator SplatRoutine()
    {
        float duration = Mathf.Max(0.01f, splatDuration);

        float t = 0f;

        splatSR.transform.localScale =
            Vector3.one * beginScale;

        SetAlpha(splatSR, maxAlpha);

        while (t < duration)
        {
            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / duration);

            float currentScale = Mathf.Lerp(
                beginScale,
                splatEndScale,
                k
            );

            float currentAlpha = Mathf.Lerp(
                maxAlpha,
                splatEndAlpha,
                k
            );

            splatSR.transform.localScale =
                Vector3.one * currentScale;

            SetAlpha(splatSR, currentAlpha);

            yield return null;
        }

        splatSR.transform.localScale =
            Vector3.one * splatEndScale;

        SetAlpha(splatSR, splatEndAlpha);

        splatRoutine = null;

        // Báo cho BulletMovement rằng hiệu ứng đã kết thúc.
        // BulletMovement sẽ trả GameObject về Pool.
        OnSplatCompleted?.Invoke();
    }

    // ---------------------------------------------------------
    // RESET
    // ---------------------------------------------------------

    public void ResetVisual()
    {
        if (peaSR == null)
            return;

        StopSplatRoutine();

        peaSR.sprite = peaSprite;

        peaSR.gameObject.SetActive(true);
        shadowSR1.gameObject.SetActive(true);
        shadowSR2.gameObject.SetActive(true);
        splatSR.gameObject.SetActive(false);

        peaSR.transform.localScale =
            Vector3.one * originalScale;

        shadowSR1.transform.localScale =
            Vector3.one * originalScale;

        shadowSR2.transform.localScale =
            Vector3.one * originalScale;

        splatSR.transform.localScale =
            Vector3.one * beginScale;

        SetAlpha(peaSR, maxAlpha);
        SetAlpha(shadowSR1, maxAlpha);
        SetAlpha(shadowSR2, maxAlpha);
        SetAlpha(splatSR, maxAlpha);
    }

    private void StopSplatRoutine()
    {
        if (splatRoutine == null)
            return;

        StopCoroutine(splatRoutine);

        splatRoutine = null;
    }

    // ---------------------------------------------------------
    // ALPHA
    // ---------------------------------------------------------

    private static void SetAlpha(
        SpriteRenderer sr,
        float alpha
    )
    {
        if (sr == null)
            return;

        Color color = sr.color;

        color.a = alpha;

        sr.color = color;
    }
}
