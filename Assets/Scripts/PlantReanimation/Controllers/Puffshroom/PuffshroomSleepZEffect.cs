using System.Collections.Generic;
using PlantReanimation.Controllers.Puffshroom;
using PlantReanimation.Core;
using UnityEngine;

[DisallowMultipleComponent]
public class PuffshroomSleepZEffect : MonoBehaviour
{
    [Header("Tham chiếu")]
    public PuffshroomController controller;

    [Header("Sprite Z trong atlas")]
    public string zAtlasId = "IMAGE_REANIM_Z";
    public string sleepClipName = "sleep";
    public Color color = Color.white;

    [Header("Số lượng & kích thước")]
    [Tooltip("Số chữ Z cùng tồn tại (2 = Z1 nhỏ dần thì Z2 hiện)")]
    [Range(1, 5)] public int zCount = 2;

    [Tooltip("Nhân kích thước chữ Z. Nhỏ hơn = Z bé lại")]
    [Min(0.05f)] public float sizeMultiplier = 0.6f;

    [Header("Tốc độ (logic gốc PvZ)")]
    [Tooltip("mAnimRate = RandRangeFloat(6, 8)")]
    public Vector2 animRateRange = new Vector2(6f, 8f);

    [Tooltip("mAnimTime = RandRangeFloat(0, 0.9): pha bắt đầu")]
    public Vector2 startPhaseRange = new Vector2(0f, 0.9f);

    [Tooltip("Thời gian 1 vòng = totalFrames / animRate. Giảm để Z ra nhanh/dày hơn")]
    [Min(1f)] public float totalFrames = 18f;

    [Header("Vị trí (local)")]
    public Vector2 localSpawnOffset = new Vector2(0.35f, 0.9f);

    [Header("Chuyển động theo pha 0..1")]
    [Min(0f)] public float riseHeight = 0.6f;
    public AnimationCurve riseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float driftX = 0.12f;
    public AnimationCurve driftCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Nhỏ -> to ra -> nhỏ dần ở nửa sau")]
    public AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.35f),
        new Keyframe(0.4f, 1f),
        new Keyframe(1f, 0.3f));

    [Tooltip("Hiện nhanh, giữ đến giữa vòng, mờ dần ở nửa sau")]
    public AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.12f, 1f),
        new Keyframe(0.6f, 1f),
        new Keyframe(1f, 0f));

    public AnimationCurve rotationCurve = new AnimationCurve(
        new Keyframe(0f, -6f),
        new Keyframe(1f, 8f));

    [Header("Sorting")]
    public int sortingOrderOffset = 20;

    private sealed class SleepZ
    {
        public GameObject gameObject;
        public SpriteRenderer renderer;
        public float phaseOffset;
    }

    private readonly List<SleepZ> zs = new List<SleepZ>();

    private Sprite zSprite;
    private float animRate;
    private float basePhase;
    private bool atlasLoadAttempted;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<PuffshroomController>();

        LoadZSprite();
    }

    private void Update()
    {
        bool sleeping = IsSleepClipPlaying();

        if (sleeping)
        {
            if (zs.Count == 0)
                CreateZs();

            UpdateZs();
        }
        else if (zs.Count > 0)
        {
            DestroyZs();
        }
    }

    private bool IsSleepClipPlaying()
    {
        if (controller == null || controller.Player == null)
            return false;

        PlantReanimClip clip = controller.Player.CurrentClip;

        return controller.Player.IsPlaying &&
               clip != null &&
               string.Equals(
                   clip.name,
                   sleepClipName,
                   System.StringComparison.OrdinalIgnoreCase);
    }

    private void LoadZSprite()
    {
        if (atlasLoadAttempted)
            return;

        atlasLoadAttempted = true;

        if (controller == null ||
            controller.profile == null ||
            controller.profile.atlasJson == null ||
            controller.profile.atlasTexture == null)
        {
            Debug.LogError(
                "[PuffshroomSleepZEffect] Controller, Profile, Atlas JSON " +
                "hoặc Atlas Texture chưa được gán.",
                this);
            return;
        }

        PlantReanimData atlasData = new PlantReanimData();

        try
        {
            PlantReanimLoader.LoadAtlas(
                controller.profile.atlasJson,
                atlasData);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[PuffshroomSleepZEffect] Không đọc được Atlas JSON: " +
                exception.Message,
                this);
            return;
        }

        PlantAtlasPart part = atlasData.GetAtlasPart(zAtlasId);

        if (part == null)
        {
            Debug.LogError(
                "[PuffshroomSleepZEffect] Không tìm thấy atlas part '" +
                zAtlasId + "'.",
                this);
            return;
        }

        Texture2D texture = controller.profile.atlasTexture;

        Rect rect = new Rect(
            part.x,
            texture.height - part.y - part.height,
            part.width,
            part.height);

        float pixelsPerUnit = Mathf.Max(
            0.01f,
            controller.profile.pixelsPerUnit);

        zSprite = Sprite.Create(
            texture,
            rect,
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);

        if (zSprite != null)
            zSprite.name = "Puffshroom_Sleep_Z_Atlas";
    }

    // Tạo zCount chữ Z, lệch pha đều nhau trong 1 vòng lặp
    private void CreateZs()
    {
        if (zSprite == null || controller == null)
            return;

        animRate = Random.Range(animRateRange.x, animRateRange.y);
        basePhase = Random.Range(startPhaseRange.x, startPhaseRange.y);

        int count = Mathf.Max(1, zCount);

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Puffshroom_Sleep_Z_" + i);
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = zSprite;
            sr.color = new Color(color.r, color.g, color.b, 0f);

            sr.sortingLayerName =
                string.IsNullOrWhiteSpace(controller.sortingLayerName)
                    ? "Default"
                    : controller.sortingLayerName;

            sr.sortingOrder = controller.sortingOrder + sortingOrderOffset;

            SleepZ z = new SleepZ
            {
                gameObject = go,
                renderer = sr,
                phaseOffset = (float)i / count
            };

            zs.Add(z);
            ApplyPose(z);
        }
    }

    private void UpdateZs()
    {
        basePhase += (animRate / Mathf.Max(1f, totalFrames)) * Time.deltaTime;
        basePhase -= Mathf.Floor(basePhase);

        for (int i = 0; i < zs.Count; i++)
            ApplyPose(zs[i]);
    }

    private void ApplyPose(SleepZ z)
    {
        if (z.gameObject == null)
            return;

        float t = basePhase + z.phaseOffset;
        t -= Mathf.Floor(t); // loop 0..1

        z.gameObject.transform.localPosition = new Vector3(
            localSpawnOffset.x + driftX * driftCurve.Evaluate(t),
            localSpawnOffset.y + riseHeight * riseCurve.Evaluate(t),
            -0.02f);

        Vector2 profileScale = controller.profile.plantScale;
        float globalScale = controller.profile.globalScale;
        float s = scaleCurve.Evaluate(t) * sizeMultiplier;

        z.gameObject.transform.localScale = new Vector3(
            globalScale * profileScale.x * s,
            globalScale * profileScale.y * s,
            1f);

        z.gameObject.transform.localRotation = Quaternion.Euler(
            0f, 0f, rotationCurve.Evaluate(t));

        Color c = color;
        c.a = color.a * Mathf.Clamp01(alphaCurve.Evaluate(t));
        z.renderer.color = c;
    }

    private void DestroyZs()
    {
        for (int i = 0; i < zs.Count; i++)
        {
            if (zs[i].gameObject != null)
                Destroy(zs[i].gameObject);
        }

        zs.Clear();
    }

    private void OnDisable()
    {
        DestroyZs();
    }

    private void OnDestroy()
    {
        if (zSprite != null)
            Destroy(zSprite);
    }
}