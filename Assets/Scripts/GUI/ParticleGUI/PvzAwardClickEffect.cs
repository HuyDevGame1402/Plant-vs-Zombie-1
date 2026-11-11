
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(RectTransform))]
public class PvzAwardClickEffect : MonoBehaviour, IPointerClickHandler
{
    [Header("Atlas")]
    public Texture2D atlasTexture;
    public TextAsset atlasJson;

    [Header("Sprite Names")]
    public string starSpriteName = "AWARDRAYS_STAR";
    public string starAltSpriteName = "AWARDRAYS_STAR";
    public string rayASpriteName = "AWARDRAYS1";
    public string rayBSpriteName = "AWARDRAYS2";
    public string glowSpriteName = "AWARDGLOW";

    [Header("Material")]
    public Material additiveMaterial;

    // =====================================================
    // STAR SETTINGS
    // =====================================================

    [Header("Starburst - Kich thuoc ngoi sao")]

    [Min(0.1f)]
    public float starSizeMultiplier = 1.5f;

    [Min(0.1f)]
    public float starLaunchSpeedMultiplier = 1.5f;

    public float starGravity = 400f;

    // =====================================================
    // RAY SETTINGS
    // =====================================================

    [Header("Award Rays - Tia sang quay")]

    [Range(0f, 1f)]
    public float rayIntensity = 0.6f;

    [Range(0f, 2f)]
    public float rayAlphaMultiplier = 1f;

    [Range(0.05f, 1f)]
    public float raySpinSpeedMultiplier = 0.3f;

    public float rayLifetime = 15f;

    // =====================================================
    // GENERAL
    // =====================================================

    [Header("General")]
    public bool flashOnStart = true;
    public bool allowReplay = false;
    public float unitScale = 1f;

    // =====================================================
    // ARROW
    // =====================================================

    [Header("Arrow Click")]

    [Tooltip("GameObject mui ten se bi an ngay khi click.")]
    public GameObject arrowClickObject;

    // =====================================================
    // MOVEMENT
    // =====================================================

    [Header("Giai doan 1 - Delay sau click")]

    [Min(0f)]
    public float awardDelay = 1.5f;

    [Header("Giai doan 2 - Di chuyen vao tam")]

    public Transform objectToMove;

    [Min(0.01f)]
    public float moveDuration = 2.5f;

    public AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // =====================================================
    // GLOW SETTINGS
    // =====================================================

    [Header("Giai doan 3 - Award Glow")]

    [Min(0f)]
    public float glowDelayAfterMove = 2f;

    [Tooltip("Thoi gian Glow phong to.")]
    [Min(0.1f)]
    public float glowGrowDuration = 3f;

    [Tooltip("Thoi gian alpha Glow tang tu 0 len 1.")]
    [Min(0.01f)]
    public float glowAlphaInDuration = 0.5f;

    [Tooltip("Thoi gian giu Glow sang toi da truoc khi CanvasGroup fade.")]
    [Min(0f)]
    public float glowFadeDelayAfterHide = 1.5f;

    [Tooltip("Thoi gian CanvasGroup fade tu 1 ve 0.")]
    [Min(0.1f)]
    public float canvasGroupFadeDuration = 2.5f;

    [Tooltip("Thoi gian alpha Glow giam tu 1 ve 0 sau khi CanvasGroup an.")]
    [Min(0.01f)]
    public float glowFadeOutDuration = 2.5f;

    [Tooltip("Kich thuoc cuoi cung cua Award Glow.")]
    [Min(0.1f)]
    public float glowMaxScale = 150f;

    [Header("Canvas Group")]

    [Tooltip("CanvasGroup se fade tu alpha 1 ve 0.")]
    public CanvasGroup canvasGroupToFade;

    [Header("Events")]

    public UnityEvent onClicked;

    [Tooltip("Duoc goi sau khi Glow fade het va bi SetActive(false).")]
    public UnityEvent onCanvasGroupFadeComplete;

    // =====================================================
    // CONSTANTS
    // =====================================================

    const float Tick = 0.01f;
    const float StarLife = 0.5f;
    const float StarFriction = 0.05f;

    // =====================================================
    // STAR EMITTERS
    // =====================================================

    struct StarEmitter
    {
        public int count;
        public Vector2 r, g, b;
        public bool alt;

        public StarEmitter(
            int c,
            Vector2 r,
            Vector2 g,
            Vector2 b,
            bool alt)
        {
            count = c;
            this.r = r;
            this.g = g;
            this.b = b;
            this.alt = alt;
        }
    }

    static readonly StarEmitter[] StarEmitters =
    {
        new StarEmitter(
            25,
            new Vector2(1, 1),
            new Vector2(.9f, .9f),
            new Vector2(0f, .4f),
            false
        ),

        new StarEmitter(
            20,
            new Vector2(1, 1),
            new Vector2(1f, 1f),
            new Vector2(.6f, .8f),
            false
        ),

        new StarEmitter(
            5,
            new Vector2(1, 1),
            new Vector2(.6f, .7f),
            new Vector2(0f, 0f),
            true
        )
    };

    // =====================================================
    // RAY DEFINITIONS
    // =====================================================

    struct RayDef
    {
        public bool useB;
        public float angle;
        public float spinMin, spinMax;
        public float start, end;

        public RayDef(
            bool b,
            float a,
            float smin,
            float smax,
            float s,
            float e)
        {
            useB = b;
            angle = a;
            spinMin = smin;
            spinMax = smax;
            start = s;
            end = e;
        }
    }

    static readonly RayDef[] RayDefs =
    {
        new RayDef(false,   0f, -30f, -60f, .01f, .20f),
        new RayDef(false, 180f, -30f, -60f, .01f, .20f),
        new RayDef(true,   90f, -30f, -60f, .01f, .20f),
        new RayDef(true,  270f, -30f, -60f, .01f, .20f),

        new RayDef(false,   0f, -41f, -70f, .02f, .21f),
        new RayDef(false, 180f, -41f, -70f, .02f, .21f),
        new RayDef(true,   90f, -41f, -70f, .02f, .21f),
        new RayDef(true,  270f, -41f, -70f, .02f, .21f)
    };

    // =====================================================
    // FX DATA
    // =====================================================

    class Fx
    {
        public RectTransform rt;
        public Image img;

        public float delay;
        public float life;
        public float age;
        public float sim;

        public Action<Fx, float, float> tick;
    }

    readonly Dictionary<string, Sprite> sprites =
        new Dictionary<string, Sprite>();

    readonly List<Fx> fx = new List<Fx>();

    readonly List<Fx> awardRayFx = new List<Fx>();

    readonly List<GameObject> layers = new List<GameObject>();

    Material runtimeAdditive;

    bool atlasReady;
    bool played;

    // =====================================================
    // SEQUENCE STATE
    // =====================================================

    bool awardPending;
    float awardTimer;

    bool movingObject;
    float moveTimer;

    Vector3 moveStartWorld;
    Vector3 moveTargetWorld;

    RectTransform awardLayer;

    bool glowPending;
    float glowTimer;

    bool objectReachedCenter;

    // =====================================================
    // AWARD GLOW STATE
    // =====================================================

    Fx awardGlowFx;

    bool glowAlphaIncreasing;
    bool glowHolding;
    bool glowWaitingToFade;
    bool glowAlphaDecreasing;
    bool glowFinished;

    float glowAlphaTimer;
    float glowHoldTimer;

    // =====================================================
    // CANVAS GROUP FADE STATE
    // =====================================================

    bool canvasGroupFading;
    float canvasGroupFadeTimer;

    // =====================================================
    // ATLAS JSON
    // =====================================================

    [Serializable]
    class AtlasRect
    {
        public int x, y, width, height;
    }

    [Serializable]
    class AtlasAttr
    {
        public string id;
    }

    [Serializable]
    class AtlasRes
    {
        public AtlasAttr attributes;
        public AtlasRect atlasRect;
    }

    [Serializable]
    class AtlasFile
    {
        public AtlasRes[] resources;
    }

    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================

    void Start()
    {
        if (flashOnStart)
            PlayScreenFlash();
    }

    void OnDestroy()
    {
        Clear();

        foreach (var pair in sprites)
        {
            if (pair.Value != null)
                Destroy(pair.Value);
        }

        sprites.Clear();

        if (runtimeAdditive != null)
            Destroy(runtimeAdditive);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClick();
    }

    // =====================================================
    // CLICK
    // =====================================================

    [ContextMenu("Play Click Effect")]
    public void PlayClick()
    {
        if (played && !allowReplay)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError(
                "[PvzAwardClickEffect] Khong tim thay Canvas.",
                this
            );

            return;
        }

        if (played && allowReplay)
            Clear();

        played = true;

        // An mui ten ngay khi click.
        if (arrowClickObject != null)
            arrowClickObject.SetActive(false);

        // Reset CanvasGroup cho lan chay moi.
        if (canvasGroupToFade != null)
            canvasGroupToFade.alpha = 1f;

        EnsureAtlas();

        Vector3 center = WorldCenter();

        Canvas root = canvas.rootCanvas;

        RectTransform starLayer = MakeLayer(
            "PvzStarLayer",
            root.transform,
            center
        );

        starLayer.SetAsLastSibling();

        SpawnStars(starLayer);

        awardTimer = 0f;
        awardPending = true;

        movingObject = false;

        glowPending = false;
        glowTimer = 0f;

        objectReachedCenter = false;

        onClicked?.Invoke();
    }

    // =====================================================
    // START AWARD
    // =====================================================

    void StartAwardSequence()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return;

        Canvas root = canvas.rootCanvas;

        Vector3 center = WorldCenter();

        awardLayer = MakeLayer(
            "PvzAwardLayer",
            transform.parent,
            center
        );

        awardLayer.SetSiblingIndex(
            transform.GetSiblingIndex()
        );

        // Hien thi Award Rays.
        SpawnAwardRays(awardLayer);

        moveTimer = 0f;

        if (objectToMove != null)
        {
            RectTransform canvasRect =
                root.transform as RectTransform;

            if (canvasRect != null)
            {
                moveStartWorld = objectToMove.position;

                moveTargetWorld =
                    canvasRect.TransformPoint(Vector3.zero);

                movingObject = true;
            }
        }
        else
        {
            movingObject = true;
        }

        objectReachedCenter = false;

        glowPending = false;
        glowTimer = 0f;
    }

    // =====================================================
    // MOVEMENT
    // =====================================================

    void UpdateObjectMovement(float dt)
    {
        if (!movingObject)
            return;

        moveTimer += dt;

        float t = Mathf.Clamp01(
            moveTimer / Mathf.Max(0.01f, moveDuration)
        );

        float easedT = moveCurve != null
            ? moveCurve.Evaluate(t)
            : t;

        if (objectToMove != null)
        {
            objectToMove.position = Vector3.LerpUnclamped(
                moveStartWorld,
                moveTargetWorld,
                easedT
            );
        }

        if (t >= 1f)
        {
            if (objectToMove != null)
                objectToMove.position = moveTargetWorld;

            movingObject = false;

            objectReachedCenter = true;

            glowTimer = 0f;
            glowPending = true;
        }
    }

    // =====================================================
    // START SCREEN FLASH
    // =====================================================

    [ContextMenu("Play Screen Flash")]
    public void PlayScreenFlash()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return;

        Canvas root = canvas.rootCanvas;

        var go = new GameObject(
            "PvzScreenFlashLayer",
            typeof(RectTransform)
        );

        var rt = (RectTransform)go.transform;

        rt.SetParent(root.transform, false);

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;

        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.AddComponent<LayoutElement>().ignoreLayout = true;

        rt.SetAsLastSibling();

        layers.Add(go);

        Fx f = Add(rt, null, false, 0f, 0.5f);

        f.rt.anchorMin = Vector2.zero;
        f.rt.anchorMax = Vector2.one;

        f.rt.offsetMin = Vector2.zero;
        f.rt.offsetMax = Vector2.zero;

        f.tick = (fx_, local, t) =>
        {
            fx_.img.color = new Color(
                1f,
                1f,
                .8f,
                Mathf.Lerp(.5f, 0f, t)
            );
        };
    }

    // =====================================================
    // CLEAR
    // =====================================================

    public void Clear()
    {
        awardPending = false;
        awardTimer = 0f;

        movingObject = false;
        moveTimer = 0f;

        glowPending = false;
        glowTimer = 0f;

        objectReachedCenter = false;

        awardLayer = null;

        // Reset Glow state.
        awardGlowFx = null;

        glowAlphaIncreasing = false;
        glowHolding = false;
        glowWaitingToFade = false;
        glowAlphaDecreasing = false;
        glowFinished = false;

        glowAlphaTimer = 0f;
        glowHoldTimer = 0f;

        // Reset CanvasGroup fade state.
        canvasGroupFading = false;
        canvasGroupFadeTimer = 0f;

        awardRayFx.Clear();

        foreach (var f in fx)
        {
            if (f.rt != null)
                Destroy(f.rt.gameObject);
        }

        fx.Clear();

        DestroyLayers();
    }

    // =====================================================
    // STARBURST
    // =====================================================

    void SpawnStars(RectTransform layer)
    {
        Sprite star = GetSprite(starSpriteName);
        Sprite starAlt = GetSprite(starAltSpriteName);

        foreach (var em in StarEmitters)
        {
            Sprite sp = em.alt ? starAlt : star;

            for (int i = 0; i < em.count; i++)
            {
                float a0 = Random.value * Mathf.PI * 2f;

                Vector2 pos = new Vector2(
                    Mathf.Cos(a0),
                    Mathf.Sin(a0)
                ) * Random.Range(1f, 75f) * unitScale;

                float la = Random.value * Mathf.PI * 2f;

                Vector2 vel = new Vector2(
                    Mathf.Cos(la),
                    Mathf.Sin(la)
                ) * Random.Range(500f, 1800f)
                  * unitScale
                  * starLaunchSpeedMultiplier;

                float ang = Random.Range(0f, 360f);

                float spin0 = Random.Range(-720f, 720f);
                float spin1 = Random.Range(-90f, 90f);

                float scale =
                    Random.Range(.1f, .3f)
                    * starSizeMultiplier;

                Color col = new Color(
                    Random.Range(em.r.x, em.r.y),
                    Random.Range(em.g.x, em.g.y),
                    Random.Range(em.b.x, em.b.y),
                    1f
                );

                Fx f = Add(
                    layer,
                    sp,
                    true,
                    0f,
                    StarLife
                );

                f.rt.localScale = Vector3.one * scale;
                f.rt.anchoredPosition = pos;

                f.tick = (fx_, local, t) =>
                {
                    while (fx_.sim + Tick <= local)
                    {
                        fx_.sim += Tick;

                        vel *= (1f - StarFriction);

                        vel.y -= starGravity * unitScale * Tick;

                        pos += vel * Tick;

                        float life01 = fx_.sim / StarLife;

                        ang += Mathf.Lerp(
                            spin0,
                            spin1,
                            Mathf.Clamp01(life01 / .8f)
                        ) * Tick;
                    }

                    float a = t < .8f
                        ? 1f
                        : Mathf.Lerp(
                            1f,
                            0f,
                            (t - .8f) / .2f
                        );

                    fx_.rt.anchoredPosition = pos;

                    fx_.rt.localEulerAngles =
                        new Vector3(0, 0, ang);

                    col.a = a;

                    fx_.img.color = col;
                };
            }
        }
    }

    // =====================================================
    // AWARD RAYS
    // =====================================================

    void SpawnAwardRays(RectTransform layer)
    {
        Sprite rayA = GetSprite(rayASpriteName);
        Sprite rayB = GetSprite(rayBSpriteName);

        foreach (var def in RayDefs)
        {
            for (int k = 0; k < 4; k++)
            {
                float delay = def.start +
                    k * (def.end - def.start) / 3f;

                float spin = Mathf.Lerp(
                    def.spinMin,
                    def.spinMax,
                    Random.value
                );

                float baseAngle = def.angle;

                Fx f = Add(
                    layer,
                    def.useB ? rayB : rayA,
                    true,
                    delay,
                    rayLifetime
                );

                awardRayFx.Add(f);

                f.tick = (fx_, local, t) =>
                {
                    float s = Mathf.Lerp(
                        .4f,
                        10f,
                        Mathf.Clamp01(t / .1f)
                    ) * unitScale;

                    float alpha = Mathf.Lerp(
                        .7f,
                        .9f,
                        Mathf.Clamp01(t / .5f)
                    );

                    alpha *= rayIntensity * rayAlphaMultiplier;

                    fx_.rt.localScale = Vector3.one * s;

                    fx_.rt.localEulerAngles =
                        new Vector3(
                            0,
                            0,
                            baseAngle +
                            spin * raySpinSpeedMultiplier * local
                        );

                    fx_.img.color = new Color(
                        1f,
                        1f,
                        1f,
                        Mathf.Clamp01(alpha)
                    );
                };
            }
        }
    }

    // =====================================================
    // AWARD GLOW
    // =====================================================

    void SpawnAwardGlow(RectTransform layer)
    {
        Sprite glow = GetSprite(glowSpriteName);

        if (glow == null)
            return;

        awardGlowFx = Add(
            layer,
            glow,
            true,
            0f,
            0f
        );

        awardGlowFx.rt.localScale =
            Vector3.one * 0.1f * unitScale;

        awardGlowFx.img.color =
            new Color(1f, 1f, 1f, 0f);

        // Bat dau tang alpha.
        glowAlphaIncreasing = true;

        glowHolding = false;
        glowWaitingToFade = false;
        glowAlphaDecreasing = false;
        glowFinished = false;

        glowAlphaTimer = 0f;
        glowHoldTimer = 0f;

        // Dieu khien scale Glow.
        awardGlowFx.tick = (fx_, local, t) =>
        {
            float growT = Mathf.Clamp01(
                local / Mathf.Max(0.1f, glowGrowDuration)
            );

            float scale = Mathf.Lerp(
                0.1f,
                glowMaxScale,
                growT
            );

            fx_.rt.localScale =
                Vector3.one * scale * unitScale;
        };
    }

    // =====================================================
    // HIDE AWARD RAYS
    // =====================================================

    void HideAwardRays()
    {
        foreach (var ray in awardRayFx)
        {
            if (ray == null)
                continue;

            if (ray.rt != null)
                ray.rt.gameObject.SetActive(false);

            fx.Remove(ray);
        }

        awardRayFx.Clear();
    }

    // =====================================================
    // UPDATE CANVAS GROUP FADE
    // =====================================================

    void UpdateCanvasGroupFade(float dt)
    {
        if (!canvasGroupFading)
            return;

        canvasGroupFadeTimer += dt;

        float t = Mathf.Clamp01(
            canvasGroupFadeTimer /
            Mathf.Max(0.1f, canvasGroupFadeDuration)
        );

        if (canvasGroupToFade != null)
        {
            canvasGroupToFade.alpha = Mathf.Lerp(
                1f,
                0f,
                t
            );
        }

        if (t >= 1f)
        {
            canvasGroupFading = false;

            if (canvasGroupToFade != null)
                canvasGroupToFade.alpha = 0f;

            // CanvasGroup da an hoan toan.
            // Bay gio moi bat dau fade Glow.
            glowAlphaDecreasing = true;
            glowAlphaTimer = 0f;
        }
    }

    // =====================================================
    // UPDATE AWARD GLOW
    // =====================================================

    void UpdateAwardGlow(float dt)
    {
        if (awardGlowFx == null ||
            awardGlowFx.img == null ||
            glowFinished)
        {
            return;
        }

        // =============================================
        // PHASE 1:
        // ALPHA 0 -> 1
        // =============================================

        if (glowAlphaIncreasing)
        {
            glowAlphaTimer += dt;

            float t = Mathf.Clamp01(
                glowAlphaTimer /
                Mathf.Max(0.01f, glowAlphaInDuration)
            );

            awardGlowFx.img.color = new Color(
                1f,
                1f,
                1f,
                t
            );

            if (t >= 1f)
            {
                glowAlphaIncreasing = false;

                // Glow dat alpha toi da.
                awardGlowFx.img.color = Color.white;

                glowHolding = true;
                glowWaitingToFade = true;

                // Bat dau dem thoi gian giu Glow.
                glowHoldTimer = 0f;
            }

            return;
        }

        // =============================================
        // PHASE 2:
        // GIU GLOW ALPHA = 1
        // =============================================

        if (glowHolding && glowWaitingToFade)
        {
            // Glow van sang toi da.
            awardGlowFx.img.color = Color.white;

            glowHoldTimer += dt;

            if (glowHoldTimer >= glowFadeDelayAfterHide)
            {
                glowHolding = false;
                glowWaitingToFade = false;

                // Tat Award Rays tai thoi diem nay.
                HideAwardRays();

                // Bat dau fade CanvasGroup.
                canvasGroupFading = true;
                canvasGroupFadeTimer = 0f;
            }

            return;
        }

        // =============================================
        // PHASE 3:
        // FADE CANVAS GROUP 1 -> 0
        // GLOW VAN GIU ALPHA = 1
        // =============================================

        if (canvasGroupFading)
        {
            awardGlowFx.img.color = Color.white;

            UpdateCanvasGroupFade(dt);

            return;
        }

        // =============================================
        // PHASE 4:
        // FADE GLOW 1 -> 0
        // =============================================

        if (glowAlphaDecreasing)
        {
            glowAlphaTimer += dt;

            float t = Mathf.Clamp01(
                glowAlphaTimer /
                Mathf.Max(0.01f, glowFadeOutDuration)
            );

            float alpha = Mathf.Lerp(
                1f,
                0f,
                t
            );

            awardGlowFx.img.color = new Color(
                1f,
                1f,
                1f,
                alpha
            );

            if (t >= 1f)
            {
                glowAlphaDecreasing = false;
                glowFinished = true;

                // Dam bao alpha bang 0.
                awardGlowFx.img.color =
                    new Color(1f, 1f, 1f, 0f);

                // Tat GameObject chua Award Glow.
                if (awardGlowFx.rt != null)
                    awardGlowFx.rt.gameObject.SetActive(false);

                // Loai Glow khoi danh sach FX.
                fx.Remove(awardGlowFx);

                awardGlowFx = null;

                // Event sau khi Glow da an hoan toan.
                onCanvasGroupFadeComplete?.Invoke();
            }
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Giai doan 1: Delay sau click.
        if (awardPending)
        {
            awardTimer += dt;

            if (awardTimer >= awardDelay)
            {
                awardPending = false;

                StartAwardSequence();
            }
        }

        // Giai doan 2: Object bay vao tam.
        UpdateObjectMovement(dt);

        // Giai doan 3: Cho sau khi object den tam.
        if (glowPending && objectReachedCenter)
        {
            glowTimer += dt;

            if (glowTimer >= glowDelayAfterMove)
            {
                glowPending = false;

                if (awardLayer != null)
                    SpawnAwardGlow(awardLayer);
            }
        }

        // Cap nhat cac giai doan cua Award Glow.
        UpdateAwardGlow(dt);

        // Update particles.
        for (int i = fx.Count - 1; i >= 0; i--)
        {
            Fx f = fx[i];

            if (f.rt == null)
            {
                fx.RemoveAt(i);
                continue;
            }

            if (!f.rt.gameObject.activeSelf)
                continue;

            f.age += dt;

            float local = f.age - f.delay;

            if (local < 0f)
                continue;

            if (!f.img.enabled)
                f.img.enabled = true;

            float t = f.life > 0f
                ? Mathf.Clamp01(local / f.life)
                : 0f;

            f.tick?.Invoke(f, local, t);

            if (f.life > 0f && local >= f.life)
            {
                Destroy(f.rt.gameObject);

                fx.RemoveAt(i);
            }
        }

        // Cleanup khi khong con FX nao.
        if (fx.Count == 0 && layers.Count > 0)
        {
            DestroyLayers();

            awardLayer = null;
        }
    }

    // =====================================================
    // FX CREATION
    // =====================================================

    Fx Add(
        RectTransform layer,
        Sprite sprite,
        bool additive,
        float delay,
        float life)
    {
        var go = new GameObject(
            sprite != null ? sprite.name : "Fx",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        var rt = (RectTransform)go.transform;

        rt.SetParent(layer, false);

        rt.anchorMin = new Vector2(.5f, .5f);
        rt.anchorMax = new Vector2(.5f, .5f);
        rt.pivot = new Vector2(.5f, .5f);

        if (sprite != null)
            rt.sizeDelta = sprite.rect.size;

        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();

        img.sprite = sprite;
        img.raycastTarget = false;
        img.material = additive ? GetAdditive() : null;
        img.enabled = delay <= 0f;

        Fx f = new Fx
        {
            rt = rt,
            img = img,
            delay = delay,
            life = life
        };

        fx.Add(f);

        return f;
    }

    // =====================================================
    // LAYER
    // =====================================================

    RectTransform MakeLayer(
        string name,
        Transform parent,
        Vector3 worldCenter)
    {
        var go = new GameObject(
            name,
            typeof(RectTransform)
        );

        var rt = (RectTransform)go.transform;

        rt.SetParent(parent, false);

        rt.anchorMin = new Vector2(.5f, .5f);
        rt.anchorMax = new Vector2(.5f, .5f);

        rt.sizeDelta = Vector2.zero;

        rt.localScale = Vector3.one;

        rt.position = worldCenter;

        go.AddComponent<LayoutElement>().ignoreLayout = true;

        layers.Add(go);

        return rt;
    }

    void DestroyLayers()
    {
        foreach (var l in layers)
        {
            if (l != null)
                Destroy(l);
        }

        layers.Clear();
    }

    // =====================================================
    // WORLD CENTER
    // =====================================================

    Vector3 WorldCenter()
    {
        var corners = new Vector3[4];

        ((RectTransform)transform).GetWorldCorners(corners);

        return (corners[0] + corners[2]) * 0.5f;
    }

    // =====================================================
    // ADDITIVE MATERIAL
    // =====================================================

    Material GetAdditive()
    {
        if (additiveMaterial != null)
            return additiveMaterial;

        if (runtimeAdditive == null)
        {
            Shader sh = Shader.Find("PvZ/UI Additive");

            if (sh == null)
            {
                Debug.LogWarning(
                    "[PvzAwardClickEffect] Khong thay shader " +
                    "'PvZ/UI Additive'. Su dung UI/Default."
                );

                sh = Shader.Find("UI/Default");
            }

            runtimeAdditive = new Material(sh);
        }

        return runtimeAdditive;
    }

    // =====================================================
    // ATLAS
    // =====================================================

    void EnsureAtlas()
    {
        if (atlasReady)
            return;

        atlasReady = true;

        if (atlasTexture == null || atlasJson == null)
        {
            Debug.LogError(
                "[PvzAwardClickEffect] Chua gan atlasTexture / atlasJson."
            );

            return;
        }

        var file = JsonUtility.FromJson<AtlasFile>(
            atlasJson.text.TrimStart('\uFEFF')
        );

        if (file == null || file.resources == null)
            return;

        foreach (var r in file.resources)
        {
            if (r.attributes == null ||
                r.atlasRect == null ||
                r.atlasRect.width <= 0 ||
                r.atlasRect.height <= 0 ||
                string.IsNullOrEmpty(r.attributes.id))
            {
                continue;
            }

            string id = r.attributes.id
                .Split('|')[0]
                .ToUpperInvariant();

            var a = r.atlasRect;

            var rect = new Rect(
                a.x,
                atlasTexture.height - a.y - a.height,
                a.width,
                a.height
            );

            var sp = Sprite.Create(
                atlasTexture,
                rect,
                new Vector2(.5f, .5f),
                100f,
                0,
                SpriteMeshType.FullRect
            );

            sp.name = id;

            sprites[id] = sp;
        }
    }

    Sprite GetSprite(string name)
    {
        string key = name.ToUpperInvariant();

        if (!key.StartsWith("IMAGE_"))
            key = "IMAGE_" + key;

        if (sprites.TryGetValue(key, out var sp))
            return sp;

        Debug.LogError(
            "[PvzAwardClickEffect] Khong tim thay sprite '" +
            key + "' trong JSON."
        );

        return null;
    }
}
