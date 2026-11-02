using System.Collections.Generic;
using DG.Tweening;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Chomper
{
    /// <summary>
    /// Hiệu ứng nổ Cherry Bomb giống PvZ1, dùng DOTween để chuyển động mượt.
    ///  - Các đám mây lớn (IMAGE_EXPLOSIONCLOUD, cam nhạt) bung ra tại chỗ,
    ///    chuyển dần sang đỏ rồi thu nhỏ biến mất.
    ///  - Các xoáy nhỏ (IMAGE_IMITATERPUFFS) nằm dưới mây lớn, bắn từ tâm ra
    ///    theo hướng ngẫu nhiên, chuyển đỏ rồi nhỏ dần biến mất.
    ///  - Chữ POWIE!! (tự vẽ bằng TextMesh hoặc gán sprite).
    /// Ảnh trong atlas màu trắng + viền đen, nên màu được tạo bằng cách
    /// nhân SpriteRenderer.color.
    /// Yêu cầu: đã import DOTween vào project.
    /// </summary>
    [DisallowMultipleComponent]
    public class CherrybombExplosionEffect : MonoBehaviour
    {
        private const int PuffFrameCount = 8;

        [Header("Cherrybomb")]
        public CherrybombController cherrybomb;

        [Header("Particles Atlas")]
        public TextAsset particlesAtlasJson;
        public Texture2D particlesAtlasTexture;

        [Min(1f)]
        public float spritePixelsPerUnit = 100f;

        [Header("Vị trí và kích thước")]
        [Tooltip("Tâm hiệu ứng so với gốc cây, tính theo local position.")]
        public Vector2 blastLocalOffset = new Vector2(0f, 0.45f);

        [Min(0.1f)]
        [Tooltip("Các đám mây lớn nằm ngẫu nhiên trong vòng tròn bán kính này quanh tâm (nhỏ = gần nhau hơn).")]
        public float clusterRadius = 0.55f;

        [Min(1)]
        [Tooltip("Số lượng đám mây lớn.")]
        public int largeCloudCount = 7;

        [Min(0.1f)]
        [Tooltip("Đường kính nhỏ nhất của một đám mây lớn (random).")]
        public float largeCloudMinDiameter = 1.0f;

        [Min(0.1f)]
        [Tooltip("Đường kính lớn nhất của một đám mây lớn (random).")]
        public float largeCloudMaxDiameter = 1.6f;

        [Tooltip("Bật: mỗi lần nổ ra một hình khác nhau. Tắt: luôn ra cùng một hình theo Random Seed.")]
        public bool randomizeEachTime = true;

        public int randomSeed = 2024;

        [Min(0.05f)]
        [Tooltip("Đường kính một xoáy nhỏ.")]
        public float smallPuffDiameter = 0.34f;

        [Min(0f)]
        [Tooltip("Xoáy nhỏ bay tới cách tâm tối thiểu bao nhiêu (đơn vị world).")]
        public float puffMinDistance = 0.9f;

        [Min(0.2f)]
        [Tooltip("Xoáy nhỏ bay tới cách tâm tối đa bao nhiêu (đơn vị world).")]
        public float puffMaxDistance = 1.8f;

        [Min(1)]
        public int smallPuffCount = 12;

        [Header("Màu: bắt đầu cam -> cuối đỏ (nhân vào ảnh trắng của atlas)")]
        [Tooltip("Màu đám mây lớn lúc mới nổ (cam nhạt).")]
        public Color largeStartColor = new Color(1f, 0.78f, 0.16f, 1f);

        [Tooltip("Màu đám mây lớn trước khi thu nhỏ biến mất.")]
        public Color largeEndColor = new Color(0.82f, 0.12f, 0.04f, 1f);

        [Tooltip("Màu xoáy nhỏ lúc mới bắn ra.")]
        public Color puffStartColor = new Color(1f, 0.5f, 0.06f, 1f);

        [Tooltip("Màu xoáy nhỏ trước khi thu nhỏ biến mất.")]
        public Color puffEndColor = new Color(0.8f, 0.08f, 0.04f, 1f);

        [Range(0f, 0.2f)]
        [Tooltip("Độ lệch sáng/tối ngẫu nhiên giữa các đám mây.")]
        public float colorJitter = 0.06f;

        [Range(0f, 1f)]
        [Tooltip("Lúc mới nổ các đám mây sáng lóe lên (pha trắng) rồi về màu cam. 0 = tắt.")]
        public float startFlash = 0.5f;

        [Header("Timing")]
        [Min(0.1f)]
        [Tooltip("Tổng thời gian sống của đám mây lớn.")]
        public float cloudLifetime = 1.5f;

        [Min(0.02f)]
        [Tooltip("Thời gian đám mây lớn bung ra lúc đầu.")]
        public float cloudGrowTime = 0.18f;

        [Min(0f)]
        [Tooltip("Các đám mây lớn bung lệch nhau ngẫu nhiên trong khoảng này (giây) cho tự nhiên.")]
        public float cloudStagger = 0.06f;

        [Min(0.1f)]
        [Tooltip("Tổng thời gian sống của xoáy nhỏ.")]
        public float puffLifetime = 1.3f;

        [Min(0.05f)]
        [Tooltip("Thời gian xoáy nhỏ bắn từ tâm ra tới vị trí cuối (giây).")]
        public float puffShootTime = 0.45f;

        [Min(0f)]
        [Tooltip("Các xoáy nhỏ bắt đầu bắn rải rác trong khoảng thời gian này (giây).")]
        public float puffSpawnWindow = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("Tới phần nào của vòng đời thì bắt đầu chuyển từ cam sang đỏ.")]
        public float turnRedStart = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("Tới phần nào thì đã đỏ hoàn toàn.")]
        public float turnRedEnd = 0.78f;

        [Range(0f, 1f)]
        [Tooltip("Từ phần nào của vòng đời thì bắt đầu thu nhỏ tới khi biến mất.")]
        public float shrinkStart = 0.72f;

        [Header("DOTween Ease")]
        [Tooltip("Độ vọt khi bung ra. 0 = không vọt, 1.7 = mặc định của OutBack.")]
        public float popOvershoot = 1.8f;

        [Tooltip("Độ 'hít vào' trước khi thu nhỏ biến mất. 0 = không.")]
        public float vanishAnticipation = 1.2f;

        [Header("Chữ POWIE!!")]
        [Tooltip("Tự vẽ chữ POWIE!! (trắng, viền đen) bằng TextMesh.")]
        public bool showPowieText = true;

        [Min(0.05f)]
        [Tooltip("Chiều cao chữ (đơn vị world).")]
        public float powieTextHeight = 0.55f;

        [Tooltip("Nếu bạn có sprite IMAGE_POWIE thì gán vào đây, nó sẽ thay cho chữ tự vẽ.")]
        public Sprite powieSprite;

        [Min(0.1f)]
        public float powieWidth = 2.0f;

        public Vector2 powieOffset = new Vector2(0.05f, 0.1f);

        [Header("Sorting")]
        public string sortingLayerName = "";
        public int sortingOrder = 100;

        [Header("Test")]
        [Tooltip("Bật trong Play Mode để thử hiệu ứng ngay.")]
        public bool testEffectNow;

        // ------------------------------------------------------------------

        private sealed class Piece
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Color startColor;
            public Color endColor;
            public Vector2 target;
            public float baseScale;
            public float delay;
            public float life;
            public float growTime;
            public float moveTime;      // chỉ dùng cho xoáy nhỏ
            public float rotationStart;
            public float rotationDelta; // tổng độ xoay trong vòng đời
            public Ease rotationEase;
            public bool isPuff;
            public bool flash;
        }

        private int runtimeSeed;
        private bool playbackObject;
        private bool effectTriggered;
        private float totalDuration;

        private readonly List<Sprite> createdSprites = new List<Sprite>();

        private Sprite cloudSprite;
        private Sprite[] puffSprites;

        private void OnEnable()
        {
            if (playbackObject)
                return;

            if (cherrybomb == null)
                cherrybomb = GetComponent<CherrybombController>();

            if (cherrybomb != null)
                cherrybomb.Exploded += HandleCherrybombExploded;
        }

        private void OnDisable()
        {
            if (cherrybomb != null)
                cherrybomb.Exploded -= HandleCherrybombExploded;
        }

        private void Update()
        {
            if (playbackObject || !testEffectNow)
                return;

            testEffectNow = false;
            TriggerExplosionEffect();
        }

        private void HandleCherrybombExploded()
        {
            TriggerExplosionEffect();
        }

        public void TriggerExplosionEffect()
        {
            if (effectTriggered)
                return;

            if (particlesAtlasJson == null || particlesAtlasTexture == null)
            {
                Debug.LogError(
                    "[CherrybombExplosionEffect] Hãy gán Particles Atlas JSON và Texture.",
                    this);
                return;
            }

            Vector3 effectPosition = transform.TransformPoint(
                new Vector3(blastLocalOffset.x, blastLocalOffset.y, 0f));

            GameObject playback = new GameObject("CherrybombExplosionVFX");
            playback.transform.position = effectPosition;

            CherrybombExplosionEffect effect =
                playback.AddComponent<CherrybombExplosionEffect>();

            if (!effect.StartPlayback(this))
            {
                Destroy(playback);
                return;
            }

            effectTriggered = true;
            HidePlantRenderers();
        }

        // ------------------------------------------------------------------
        //  Khởi tạo hiệu ứng
        // ------------------------------------------------------------------

        private bool StartPlayback(CherrybombExplosionEffect s)
        {
            playbackObject = true;

            CopySettings(s);

            PlantReanimData atlasData = new PlantReanimData();

            try
            {
                PlantReanimLoader.LoadAtlas(s.particlesAtlasJson, atlasData);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "[CherrybombExplosionEffect] Đọc Particles Atlas thất bại: " +
                    exception.Message,
                    this);
                return false;
            }

            PlantAtlasPart cloudPart = atlasData.GetAtlasPart("IMAGE_EXPLOSIONCLOUD");
            PlantAtlasPart puffStrip = atlasData.GetAtlasPart("IMAGE_IMITATERPUFFS");

            if (cloudPart == null || puffStrip == null)
            {
                Debug.LogError(
                    "[CherrybombExplosionEffect] Không tìm thấy IMAGE_EXPLOSIONCLOUD " +
                    "hoặc IMAGE_IMITATERPUFFS trong atlas JSON.",
                    this);
                return false;
            }

            cloudSprite = CreateStripSprite(
                s.particlesAtlasTexture, cloudPart, 0, 1,
                s.spritePixelsPerUnit, "CherrybombCloud");

            if (cloudSprite == null)
                return false;

            createdSprites.Add(cloudSprite);

            puffSprites = new Sprite[PuffFrameCount];
            for (int i = 0; i < PuffFrameCount; i++)
            {
                puffSprites[i] = CreateStripSprite(
                    s.particlesAtlasTexture, puffStrip, i, PuffFrameCount,
                    s.spritePixelsPerUnit, "CherrybombPuff_" + i);

                if (puffSprites[i] == null)
                    return false;

                createdSprites.Add(puffSprites[i]);
            }

            totalDuration = 0f;

            BuildLargeClouds(s.sortingLayerName, s.sortingOrder);
            BuildSmallPuffs(s.sortingLayerName, s.sortingOrder);
            BuildPowie(s.sortingLayerName, s.sortingOrder);

            // Tự huỷ khi mọi tween đã xong.
            DOVirtual
                .DelayedCall(totalDuration + 0.05f, () => Destroy(gameObject), false)
                .SetLink(gameObject);

            return true;
        }

        private void CopySettings(CherrybombExplosionEffect s)
        {
            clusterRadius = s.clusterRadius;
            largeCloudCount = s.largeCloudCount;
            largeCloudMinDiameter = s.largeCloudMinDiameter;
            largeCloudMaxDiameter = s.largeCloudMaxDiameter;
            runtimeSeed = s.randomizeEachTime ? Random.Range(0, 1000000) : s.randomSeed;

            smallPuffDiameter = s.smallPuffDiameter;
            puffMinDistance = s.puffMinDistance;
            puffMaxDistance = s.puffMaxDistance;
            smallPuffCount = s.smallPuffCount;

            largeStartColor = s.largeStartColor;
            largeEndColor = s.largeEndColor;
            puffStartColor = s.puffStartColor;
            puffEndColor = s.puffEndColor;
            colorJitter = s.colorJitter;
            startFlash = s.startFlash;

            cloudLifetime = s.cloudLifetime;
            cloudGrowTime = s.cloudGrowTime;
            cloudStagger = s.cloudStagger;
            puffLifetime = s.puffLifetime;
            puffShootTime = s.puffShootTime;
            puffSpawnWindow = s.puffSpawnWindow;
            turnRedStart = s.turnRedStart;
            turnRedEnd = s.turnRedEnd;
            shrinkStart = s.shrinkStart;

            popOvershoot = s.popOvershoot;
            vanishAnticipation = s.vanishAnticipation;

            showPowieText = s.showPowieText;
            powieTextHeight = s.powieTextHeight;
            powieSprite = s.powieSprite;
            powieWidth = s.powieWidth;
            powieOffset = s.powieOffset;
        }

        // ------------------------------------------------------------------
        //  Mây lớn
        // ------------------------------------------------------------------

        private void BuildLargeClouds(string layerName, int baseOrder)
        {
            System.Random rng = new System.Random(runtimeSeed);
            int count = Mathf.Max(1, largeCloudCount);

            float minD = Mathf.Min(largeCloudMinDiameter, largeCloudMaxDiameter);
            float maxD = Mathf.Max(largeCloudMinDiameter, largeCloudMaxDiameter);

            for (int i = 0; i < count; i++)
            {
                // Vị trí ngẫu nhiên trong vòng tròn nhỏ quanh tâm -> các đám sát và đè lên nhau.
                Vector2 position = PolarToVector(
                    Range(rng, 0f, 360f),
                    clusterRadius * Mathf.Sqrt((float)rng.NextDouble()));

                float shade = 1f + Range(rng, -colorJitter, colorJitter);

                SpriteRenderer sr = CreateRenderer(
                    cloudSprite, "LargeCloud", layerName, baseOrder + i);

                Piece piece = new Piece
                {
                    transform = sr.transform,
                    renderer = sr,
                    startColor = Shade(largeStartColor, shade),
                    endColor = Shade(largeEndColor, shade),
                    target = position,
                    baseScale = ScaleForDiameter(cloudSprite, Range(rng, minD, maxD)),
                    delay = Range(rng, 0f, cloudStagger),
                    life = cloudLifetime,
                    growTime = cloudGrowTime,
                    rotationStart = Range(rng, 0f, 360f),
                    rotationDelta = Range(rng, -10f, 10f),
                    rotationEase = Ease.OutSine,
                    isPuff = false,
                    flash = true
                };

                StartPiece(piece);
            }
        }

        // ------------------------------------------------------------------
        //  Mây nhỏ
        // ------------------------------------------------------------------

        private void BuildSmallPuffs(string layerName, int baseOrder)
        {
            System.Random rng = new System.Random(runtimeSeed + 7);
            int count = Mathf.Max(1, smallPuffCount);

            for (int i = 0; i < count; i++)
            {
                // Hướng và khoảng cách hoàn toàn ngẫu nhiên.
                Vector2 target = PolarToVector(
                    Range(rng, 0f, 360f),
                    Range(rng, puffMinDistance, Mathf.Max(puffMinDistance, puffMaxDistance)));

                Sprite sprite = puffSprites[rng.Next(PuffFrameCount)];
                float shade = 1f + Range(rng, -colorJitter, colorJitter);
                float moveTime = puffShootTime * Range(rng, 0.85f, 1.15f);

                // Nằm dưới mây lớn.
                SpriteRenderer sr = CreateRenderer(
                    sprite, "SmallPuff", layerName, baseOrder - 100 + i);

                Piece piece = new Piece
                {
                    transform = sr.transform,
                    renderer = sr,
                    startColor = Shade(puffStartColor, shade),
                    endColor = Shade(puffEndColor, shade),
                    target = target,
                    baseScale = ScaleForDiameter(
                        sprite, smallPuffDiameter * Range(rng, 0.7f, 1.3f)),
                    delay = Range(rng, 0f, puffSpawnWindow),
                    life = puffLifetime * Range(rng, 0.9f, 1.1f),
                    growTime = moveTime * 0.6f,
                    moveTime = moveTime,
                    rotationStart = Range(rng, 0f, 360f),
                    rotationDelta = Range(rng, -200f, 200f),
                    rotationEase = Ease.OutCubic,
                    isPuff = true,
                    flash = false
                };

                StartPiece(piece);
            }
        }

        // ------------------------------------------------------------------
        //  Điều khiển một đám mây theo thời gian (DOTween chỉ đếm thời gian,
        //  mọi giá trị tính trực tiếp bằng các ease của DOTween nên không bị
        //  xung đột giữa nhiều tween trên cùng một thuộc tính).
        // ------------------------------------------------------------------

        private void StartPiece(Piece p)
        {
            ApplyPiece(p, 0f); // trạng thái ban đầu: scale 0

            DOVirtual
                .Float(0f, p.life, p.life, v => ApplyPiece(p, v))
                .SetDelay(p.delay)
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .OnComplete(() => ApplyPiece(p, p.life));

            totalDuration = Mathf.Max(totalDuration, p.delay + p.life);
        }

        private void ApplyPiece(Piece p, float local)
        {
            float t = Mathf.Clamp01(local / p.life);

            // ---- Màu: cam nhạt -> đỏ dần ----
            float redT = Mathf.InverseLerp(
                turnRedStart,
                Mathf.Max(turnRedStart + 0.01f, turnRedEnd),
                t);

            float red = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(redT), Ease.InOutSine);
            Color color = Color.Lerp(p.startColor, p.endColor, red);

            // Lóe sáng lúc mới bung, rồi về màu cam.
            if (p.flash && startFlash > 0f)
            {
                float flashT = Mathf.Clamp01(local / (p.growTime * 2f));
                float flashAmount =
                    1f - DOVirtual.EasedValue(0f, 1f, flashT, Ease.OutQuad);

                color = Color.Lerp(color, Color.white, startFlash * flashAmount);
            }

            color.a = 1f;
            p.renderer.color = color;

            // ---- Scale: bung ra (OutBack) rồi thu nhỏ biến mất (InBack) ----
            float growT = Mathf.Clamp01(local / Mathf.Max(0.01f, p.growTime));
            float grow = DOVirtual.EasedValue(0f, 1f, growT, Ease.OutBack, popOvershoot);

            float shrinkT = Mathf.Clamp01(Mathf.InverseLerp(shrinkStart, 1f, t));
            float shrink = DOVirtual.EasedValue(1f, 0f, shrinkT, Ease.InBack, vanishAnticipation);

            float scale = Mathf.Max(0f, grow * shrink);
            if (shrinkT >= 1f)
                scale = 0f;

            p.transform.localScale = Vector3.one * p.baseScale * scale;

            // ---- Vị trí ----
            Vector2 pos = p.target;
            if (p.isPuff)
            {
                float move = DOVirtual.EasedValue(
                    0f, 1f, Mathf.Clamp01(local / p.moveTime), Ease.OutQuart);
                pos = p.target * move;
            }

            p.transform.localPosition = new Vector3(pos.x, pos.y, 0f);

            // ---- Xoay ----
            float rot = DOVirtual.EasedValue(0f, 1f, t, p.rotationEase);
            p.transform.localRotation =
                Quaternion.Euler(0f, 0f, p.rotationStart + p.rotationDelta * rot);
        }

        // ------------------------------------------------------------------
        //  Chữ POWIE!!
        // ------------------------------------------------------------------

        private void BuildPowie(string layerName, int baseOrder)
        {
            if (powieSprite == null && !showPowieText)
                return;

            GameObject root = new GameObject("Powie");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(powieOffset.x, powieOffset.y, 0f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            root.transform.localScale = Vector3.zero;

            int order = baseOrder + 200;
            float baseScale = 1f;

            if (powieSprite != null)
            {
                SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = powieSprite;

                if (!string.IsNullOrEmpty(layerName))
                    sr.sortingLayerName = layerName;

                sr.sortingOrder = order;
                baseScale = ScaleForDiameter(powieSprite, powieWidth);
            }
            else
            {
                BuildPowieText(root.transform, layerName, order);
            }

            Transform t = root.transform;
            float life = cloudLifetime;

            DOVirtual
                .Float(0f, life, life, v =>
                {
                    float pop = DOVirtual.EasedValue(
                        0f, 1f, Mathf.Clamp01(v / 0.16f), Ease.OutBack, popOvershoot + 0.4f);

                    float shrinkT = Mathf.Clamp01(
                        Mathf.InverseLerp(shrinkStart, 1f, v / life));

                    float shrink = DOVirtual.EasedValue(
                        1f, 0f, shrinkT, Ease.InBack, vanishAnticipation);

                    float scale = Mathf.Max(0f, pop * shrink);
                    if (shrinkT >= 1f)
                        scale = 0f;

                    t.localScale = Vector3.one * baseScale * scale;

                    // Rung tắt dần ngay sau khi pop.
                    float shake = Mathf.Sin(v * 50f) * 8f * Mathf.Exp(-v * 7f);
                    t.localRotation = Quaternion.Euler(0f, 0f, -2f + shake);
                })
                .SetEase(Ease.Linear)
                .SetLink(root);

            totalDuration = Mathf.Max(totalDuration, life);
        }

        private void BuildPowieText(Transform parent, string layerName, int order)
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Arial Black", "Arial", "Verdana" }, 64);

            // Viền đen: 8 bản sao dịch quanh chữ trắng.
            float o = powieTextHeight * 0.12f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                AddPowieTextMesh(
                    parent, font, layerName, order,
                    new Vector2(Mathf.Cos(a) * o, Mathf.Sin(a) * o),
                    Color.black);
            }

            AddPowieTextMesh(parent, font, layerName, order + 1, Vector2.zero, Color.white);
        }

        private void AddPowieTextMesh(
            Transform parent,
            Font font,
            string layerName,
            int order,
            Vector2 offset,
            Color color)
        {
            GameObject go = new GameObject("PowieText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = "POWIE!!";
            tm.font = font;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = powieTextHeight / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;

            if (!string.IsNullOrEmpty(layerName))
                mr.sortingLayerName = layerName;

            mr.sortingOrder = order;
        }

        // ------------------------------------------------------------------
        //  Tiện ích
        // ------------------------------------------------------------------

        private SpriteRenderer CreateRenderer(
            Sprite sprite, string objectName, string layerName, int order)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            if (!string.IsNullOrEmpty(layerName))
                sr.sortingLayerName = layerName;

            sr.sortingOrder = order;
            return sr;
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static Vector2 PolarToVector(float degrees, float distance)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * distance;
        }

        private static Color Shade(Color c, float shade)
        {
            return new Color(
                Mathf.Clamp01(c.r * shade),
                Mathf.Clamp01(c.g * shade),
                Mathf.Clamp01(c.b * shade),
                1f);
        }

        private static Sprite CreateStripSprite(
            Texture2D texture,
            PlantAtlasPart strip,
            int frameIndex,
            int frameCount,
            float pixelsPerUnit,
            string spriteName)
        {
            if (texture == null ||
                strip == null ||
                frameCount <= 0 ||
                frameIndex < 0 ||
                frameIndex >= frameCount)
            {
                return null;
            }

            float frameWidth = strip.width / frameCount;
            float frameHeight = strip.height;

            Rect rect = new Rect(
                strip.x + frameIndex * frameWidth,
                texture.height - strip.y - strip.height,
                frameWidth,
                frameHeight);

            Sprite sprite = Sprite.Create(
                texture,
                rect,
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, pixelsPerUnit),
                0,
                SpriteMeshType.FullRect);

            sprite.name = spriteName;
            return sprite;
        }

        private static float ScaleForDiameter(Sprite sprite, float diameter)
        {
            float width = sprite.bounds.size.x;
            return width <= 0f ? 1f : diameter / width;
        }

        private void HidePlantRenderers()
        {
            Renderer[] plantRenderers = GetComponentsInChildren<Renderer>(true);

            foreach (Renderer plantRenderer in plantRenderers)
                plantRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            // Tween đã được SetLink nên tự dừng khi object bị huỷ.
            foreach (Sprite sprite in createdSprites)
            {
                if (sprite != null)
                    Destroy(sprite);
            }

            createdSprites.Clear();
        }
    }
}