using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Controls the Level 1 sod roll and reproduces the SodRoll.xml debris emitter values.
/// - Dirt particles spawn directly on the roll body (world space), so they follow the roll.
/// - Can drive LevelSodRollAnimation (grass) so the grass and the roll move in perfect sync.
/// - Được Level1Intro điều khiển: gọi Play() để bắt đầu, chờ IsFinished để biết đã xong.
/// </summary>
public class LevelSodRollMover : MonoBehaviour
{
    [Header("Cấu hình Atlas & Dữ liệu")]
    public Texture2D atlasTexture;           // LoadingImages_PlantsZombies_2_960_00.png
    public TextAsset atlasJson;              // LoadingImages_PlantsZombies_2_960.json

    [Header("Điều khiển từ Intro")]
    [Tooltip("Tắt: cuộn đất đứng yên (và bị ẩn) chờ Level1Intro gọi Play(). Bật: tự chạy ngay khi Start.")]
    public bool playOnStart = false;

    [Header("Tùy chỉnh Quãng đường Lăn")]
    public float pixelsPerUnit = 100f;
    [Tooltip("Vị trí bắt đầu (đặt là 0 để đứng đúng chỗ hiện tại trên Scene)")]
    public float startX = 0f;
    [Tooltip("Vị trí kết thúc bên phải sân")]
    public float endX = 6f;

    [Header("Đồng bộ với thảm cỏ")]
    [Tooltip("Kéo object chứa LevelSodRollAnimation vào đây để cỏ chạy khớp với cuộn đất")]
    public LevelSodRollAnimation grassAnimation;
    [Tooltip("Bật: End X tự tính bằng đúng chiều rộng thảm cỏ (ghi đè giá trị End X ở trên lúc chạy)")]
    public bool syncEndXWithGrass = true;

    [Header("Cấu hình Thu Nhỏ (Scale) khi Lăn")]
    [Tooltip("Scale nhỏ nhất khi lăn đến đích cuối (ví dụ: 0.45)")]
    public float minScale = 0.45f;

    [Header("Tùy chỉnh Scale Ban Đầu (Vector2)")]
    [Tooltip("Scale gốc (X, Y) của Thân Đất")]
    public Vector2 dirtScale = new Vector2(1f, 1f);
    [Tooltip("Scale gốc (X, Y) của Cỏ Xanh Tròn")]
    public Vector2 grassScale = new Vector2(1f, 1f);

    [Header("Canh chỉnh Cỏ Xanh Tròn (Offset so với Thân Đất)")]
    [Tooltip("Độ lệch X và Y tinh chỉnh thêm cho cuộn cỏ xanh")]
    public Vector2 grassRollOffset = new Vector2(0f, 0f);
    [Tooltip("Độ chồng lấn giữa cuộn cỏ và mép dưới thân đất (đơn vị Unity). Tăng nếu còn khe hở.")]
    public float grassOverlap = 0.02f;

    [Header("⚙️ TÙY CHỈNH HIỆU ỨNG HẠT ĐẤT")]
    [Tooltip("SodRoll.xml: SpawnRate giảm từ 200 xuống 100 hạt/giây")]
    public Vector2 dirtSpawnRate = new Vector2(200f, 100f);
    [Tooltip("SodRoll.xml: ParticleDuration 10–25 ticks, quy đổi 100 tick/giây")]
    public Vector2 dirtLifeSeconds = new Vector2(0.10f, 0.25f);
    [Tooltip("SodRoll.xml: LaunchSpeed 100, quy đổi ra đơn vị Unity/giây. Tăng lên 2-4 nếu muốn hạt văng mạnh hơn")]
    public float dirtLaunchSpeed = 1f;
    [Tooltip("SodRoll.xml: LaunchAngle [90,180] độ")]
    public Vector2 dirtLaunchAngle = new Vector2(90f, 180f);
    [Tooltip("Source ma sát mỗi tick: velocity *= 1 - 0.05")]
    [Range(0f, 1f)] public float dirtFrictionPerTick = 0.05f;
    [Tooltip("Gia tốc rơi (đơn vị Unity/giây²)")]
    public float dirtAccelerationY = 10f;
    [Tooltip("SodRoll.xml: ParticleScale [0.7,0.9]")]
    public Vector2 dirtParticleScale = new Vector2(0.7f, 0.9f);

    [Header("Giữ độ dày & độ nét của hạt đất đến cuối")]
    [Tooltip("Bật: giữ số hạt/giây ở mức đầu (Dirt Spawn Rate X), không giảm dần về cuối")]
    public bool keepSpawnRateConstant = true;
    [Tooltip("Bù cho việc cuộn đất chạy nhanh dần (p^1.8): 0 = không bù, 1 = bù mạnh. Càng cao càng nhiều hạt ở cuối")]
    [Range(0f, 1.5f)] public float speedCompensation = 0.8f;
    [Tooltip("Bật: hạt không bị mờ dần theo tiến độ ở cuối (vẫn mờ theo vòng đời riêng của từng hạt)")]
    public bool keepAlphaToEnd = true;
    [Tooltip("Bật: vùng phát hạt giữ nguyên độ rộng, không hẹp dần về cuối")]
    public bool keepEmitWidth = true;

    [Header("Vùng phát hạt (bám theo thân đất)")]
    [Tooltip("Hạt văng ra phía sau (trái) cuộn đất. Bỏ tick nếu muốn văng sang phải.")]
    public bool throwBackwards = true;
    [Tooltip("Độ rộng vùng phát tại mép sau của thân đất (pixel): đầu → cuối")]
    public Vector2 emitWidthPixels = new Vector2(25f, 1f);

    [Header("Particle spin")]
    [Tooltip("SodRoll.xml: ParticleSpinSpeed [-1720,1720]")]
    public Vector2 dirtSpinSpeed = new Vector2(-1720f, 1720f);

    [Header("Sorting Order (Hiển thị cao nhất)")]
    [Tooltip("Đặt từ 13 trở lên để cao hơn thân đất và cỏ xanh")]
    public int dirtParticleSortingOrder = 15;
    public int baseSortingOrder = 10;

    [Header("Tiến độ & Chạy thử")]
    [Tooltip("Chỉ có tác dụng khi đã Play(). Nếu tắt, progress phải được set tay bằng thanh kéo bên dưới.")]
    public bool autoPlay = true;
    [Range(0f, 1f)]
    [Tooltip("Kéo thanh này bằng tay để test vị trí và hiệu ứng (cần bật Play On Start hoặc gọi Play())")]
    public float progress = 0f;

    [Tooltip("SodRoll.xml SystemDuration=200 ticks, or 2 seconds at the source's 100 Hz update rate. Đây là thời gian chạy chung cho cả cuộn đất và thảm cỏ.")]
    public float duration = 2f;

    [Header("⚙️ TÙY CHỈNH TỐC ĐỘ QUAY CỎ")]
    [Tooltip("Tổng số độ xoay của cuộn cỏ xanh từ đầu đến cuối (360 = 1 vòng, 720 = 2 vòng...)")]
    public float totalRollAngle = 720f;

    /// <summary>True khi cuộn đất đã lăn xong (cỏ cũng đã trải xong vì chạy cùng progress).</summary>
    public bool IsFinished => isCompleted;

    /// <summary>True khi đang chạy (đã Play() nhưng chưa xong).</summary>
    public bool IsPlaying => isPlaying && !isCompleted;

    private Transform dirtRollTf;   // Thân cuộn đất
    private Transform grassRollTf;  // Cuộn cỏ xanh tròn
    private SpriteRenderer dirtBodySr;
    private float timer;
    private Vector3 spawnPosition;  // Vị trí gốc trên Scene

    // Danh sách Sprite các hạt đất nâu (vừa và nhỏ)
    private readonly List<Sprite> dirtParticleSprites = new List<Sprite>();

    private bool isPlaying = false;
    private bool isCompleted = false;

    // Hệ thống Object Pool cho hạt đất rơi (Độc lập chuyển động & Xoay)
    private class DirtParticle
    {
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public Vector3 position;
        public float lifeTime;
        public float maxLifeTime;
        public float particleScale;
        public float rotationSpeed; // Tốc độ và chiều quay riêng cho từng hạt
        public Vector2 velocity;
        public bool isActive;
    }
    private readonly List<DirtParticle> dirtPool = new List<DirtParticle>();
    private float dirtSpawnAccumulator;
    private const float SourceTicksPerSecond = 100f;

    void Awake()
    {
        spawnPosition = transform.localPosition;
        BuildRollObjects();
    }

    void Start()
    {
        if (playOnStart) isPlaying = true;
        SetupGrassSync();
    }

    /// <summary>
    /// Khóa dải cỏ vào progress của cuộn đất và tính lại endX theo chiều rộng thảm cỏ.
    /// Gọi ở Start và ở Play() để chắc chắn luôn được áp dụng.
    /// </summary>
    void SetupGrassSync()
    {
        if (grassAnimation == null)
        {
            Debug.LogWarning("[LevelSodRollMover] Chưa gán Grass Animation! Dải cỏ sẽ tự chạy riêng (demoDuration) và " +
                             "cuộn đất sẽ lăn theo End X thủ công, nên hai bên sẽ lệch nhau.", this);
            return;
        }

        // Cỏ không tự chạy nữa, chỉ đi theo progress của cuộn đất
        grassAnimation.externalControl = true;
        grassAnimation.ForceProgress(progress);

        // Cuộn đất lăn đúng bằng chiều rộng thảm cỏ.
        // WorldWidth chỉ là chiều rộng theo đơn vị cục bộ của dải cỏ, nên phải nhân với scale thực tế
        // của object dải cỏ (lossyScale), rồi quy đổi về không gian của object cha của cuộn đất
        // (vì cuộn đất di chuyển bằng localPosition trong không gian đó).
        if (syncEndXWithGrass)
        {
            float grassWorldWidth = grassAnimation.WorldWidth * Mathf.Abs(grassAnimation.transform.lossyScale.x);
            float parentScaleX = transform.parent != null ? Mathf.Abs(transform.parent.lossyScale.x) : 1f;
            if (parentScaleX < 0.0001f) parentScaleX = 1f;

            endX = startX + grassWorldWidth / parentScaleX;
        }
    }

    /// <summary>
    /// Bắt đầu chạy cuộn đất + dải cỏ. Level1Intro gọi hàm này sau khi camera intro xong.
    /// </summary>
    public void Play()
    {
        if (isPlaying || isCompleted) return;

        timer = 0f;
        progress = 0f;
        isPlaying = true;

        SetupGrassSync();

        if (dirtRollTf != null) dirtRollTf.gameObject.SetActive(true);
        if (grassRollTf != null) grassRollTf.gameObject.SetActive(true);

        AnimateRoll(progress);
        if (grassAnimation != null) grassAnimation.ForceProgress(progress);
    }

    // Cắt sprite và tạo các GameObject con + Pool hạt đất
    void BuildRollObjects()
    {
        if (atlasJson == null || atlasTexture == null) return;

        var root = JObject.Parse(atlasJson.text);
        Sprite dirtSprite = null;
        Sprite grassGreenSprite = null;

        foreach (var r in (JArray)root["resources"])
        {
            var rect = r["atlasRect"];
            if (rect == null) continue;
            string id = (string)r["attributes"]["id"];
            string cleanId = id.Split('|')[0].ToUpperInvariant();

            float x = (float)rect["x"], y = (float)rect["y"];
            float w = (float)rect["width"], h = (float)rect["height"];

            int rows = 1;
            int cols = 1;
            if (r["attributes"]["rows"] != null) int.TryParse(r["attributes"]["rows"].ToString(), out rows);
            if (r["attributes"]["cols"] != null) int.TryParse(r["attributes"]["cols"].ToString(), out cols);

            var uRect = new Rect(x, atlasTexture.height - y - h, w, h);

            if (cleanId == "IMAGE_REANIM_SODROLL")
            {
                dirtSprite = Sprite.Create(atlasTexture, uRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            else if (cleanId == "IMAGE_REANIM_SODROLLCAP" || cleanId.Contains("SODROLLCAP"))
            {
                grassGreenSprite = Sprite.Create(atlasTexture, uRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            // Chỉ lấy đất nâu từ IMAGE_DIRTSMALL
            else if (cleanId == "IMAGE_DIRTSMALL")
            {
                float cellW = w / cols;
                float cellH = h / rows;

                // SodRoll.xml has ImageFrames=8; cut the eight small-dirt frames from row one.
                int frameCount = Mathf.Min(8, cols);
                for (int cIdx = 0; cIdx < frameCount; cIdx++)
                {
                    float cx = x + cIdx * cellW;
                    var subRect = new Rect(cx, atlasTexture.height - y - cellH, cellW, cellH);
                    dirtParticleSprites.Add(Sprite.Create(atlasTexture, subRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect));
                }
            }
        }

        // --- Tạo GameObject con 1: Thân Đất Cuộn ---
        var dirtGo = new GameObject("DirtRoll_Body");
        dirtGo.transform.SetParent(transform, false);
        dirtGo.transform.localPosition = Vector3.zero;
        var dirtSr = dirtGo.AddComponent<SpriteRenderer>();
        dirtSr.sprite = dirtSprite;
        dirtSr.sortingOrder = baseSortingOrder;
        dirtRollTf = dirtGo.transform;
        dirtBodySr = dirtSr;

        // --- Tạo GameObject con 2: Cuộn Cỏ Xanh Tròn ---
        var grassGo = new GameObject("GrassRoll_Cap");
        grassGo.transform.SetParent(transform, false);
        var grassSr = grassGo.AddComponent<SpriteRenderer>();
        grassSr.sprite = grassGreenSprite;
        grassSr.sortingOrder = baseSortingOrder + 2;
        grassRollTf = grassGo.transform;

        AnimateRoll(progress);

        // Ẩn cuộn đất cho đến khi Play() để không hiện sẵn lúc camera đang chạy
        dirtGo.SetActive(playOnStart);
        grassGo.SetActive(playOnStart);
    }

    void Update()
    {
        // Chưa Play: đứng yên, chỉ cập nhật hạt (nếu có)
        if (!isPlaying)
        {
            UpdateDirtParticles(Time.deltaTime);
            return;
        }

        // 1. Logic chạy animation cuộn cỏ và sinh hạt đất (chỉ chạy khi chưa hoàn thành)
        if (!isCompleted)
        {
            if (autoPlay && !Mathf.Approximately(duration, 0f))
            {
                timer += Time.deltaTime;
                progress = Mathf.Clamp01(timer / duration);
            }

            AnimateRoll(progress);

            // Đồng bộ thảm cỏ theo đúng progress của cuộn đất (cùng frame, không trễ)
            if (grassAnimation != null) grassAnimation.ForceProgress(progress);

            // SodRoll.xml: SpawnRate 200→100 over SystemDuration=200 source ticks (2 seconds).
            if (dirtParticleSprites.Count > 0 && progress > 0f && progress < 0.999f)
            {
                float rate = keepSpawnRateConstant
                    ? dirtSpawnRate.x
                    : Mathf.Lerp(dirtSpawnRate.x, dirtSpawnRate.y, progress);

                // Cuộn đất chạy nhanh dần nên cùng 1 số hạt/giây sẽ bị thưa ra ở cuối.
                // Nhân thêm hệ số theo tiến độ để mật độ hạt trên quãng đường không giảm.
                rate *= 1f + speedCompensation * progress;

                dirtSpawnAccumulator += rate * Time.deltaTime;
                while (dirtSpawnAccumulator >= 1f)
                {
                    SpawnDirtParticle(progress);
                    dirtSpawnAccumulator -= 1f;
                }
            }

            // Kiểm tra khi tiến độ chạm đích hoàn toàn
            if (progress >= 0.9999f && !isCompleted)
            {
                isCompleted = true;
                StartCoroutine(HideRollObjectsWithDelay(0f));
            }
        }

        // 2. LUÔN CẬP NHẬT HẠT ĐẤT: đặt ngoài khối !isCompleted để các hạt đang rơi dở
        // tiếp tục rơi, xoay và mờ dần theo đúng vòng đời.
        UpdateDirtParticles(Time.deltaTime);
    }

    private System.Collections.IEnumerator HideRollObjectsWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (dirtRollTf != null) dirtRollTf.gameObject.SetActive(false);
        if (grassRollTf != null) grassRollTf.gameObject.SetActive(false);

        // Debris already thrown finishes its own flight and lifetime.
    }

    // --- Object pool for SodRoll.xml dirt particles ---
    void SpawnDirtParticle(float p)
    {
        if (dirtRollTf == null || grassRollTf == null || dirtBodySr == null || dirtParticleSprites.Count == 0) return;

        DirtParticle particle = null;
        foreach (var pooledParticle in dirtPool)
        {
            if (!pooledParticle.isActive)
            {
                particle = pooledParticle;
                break;
            }
        }

        if (particle == null)
        {
            var go = new GameObject("FallingDirt_Particle");
            go.transform.SetParent(transform.parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = dirtParticleSortingOrder;

            particle = new DirtParticle
            {
                gameObject = go,
                spriteRenderer = sr,
                isActive = false
            };
            dirtPool.Add(particle);
        }

        int randIndex = Random.Range(0, dirtParticleSprites.Count);
        particle.spriteRenderer.sprite = dirtParticleSprites[randIndex];

        particle.particleScale = Random.Range(dirtParticleScale.x, dirtParticleScale.y);
        particle.gameObject.transform.localScale = Vector3.one * particle.particleScale;

        // Sinh hạt ngay trên thân đất thực tế (world space) để luôn bám theo cuộn đất.
        Bounds b = dirtBodySr.bounds;

        // Vùng phát ở mép sau (bên trái) của cuộn đất, hẹp dần theo tiến độ.
        float emitWidth = (keepEmitWidth ? emitWidthPixels.x : Mathf.Lerp(emitWidthPixels.x, emitWidthPixels.y, p)) / pixelsPerUnit + 0.05f;
        emitWidth = Mathf.Min(emitWidth, b.size.x);
        float worldX = b.min.x + Random.Range(0f, emitWidth);
        float worldY = Random.Range(b.min.y, b.max.y);
        particle.position = new Vector3(worldX, worldY, dirtRollTf.position.z);

        // Hạt văng ra phía sau và lên trên (ngược chiều cuộn đất đang lăn sang phải)
        float angle = Random.Range(dirtLaunchAngle.x, dirtLaunchAngle.y) * Mathf.Deg2Rad;
        float dirX = throwBackwards ? -1f : 1f;
        particle.velocity = new Vector2(dirX * Mathf.Sin(angle) * dirtLaunchSpeed, -Mathf.Cos(angle) * dirtLaunchSpeed);
        particle.gameObject.transform.position = particle.position;

        // SodRoll.xml RandomLaunchSpin: each particle starts at a random angle.
        particle.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        particle.rotationSpeed = Random.Range(dirtSpinSpeed.x, dirtSpinSpeed.y);
        particle.lifeTime = 0f;
        particle.maxLifeTime = Random.Range(dirtLifeSeconds.x, dirtLifeSeconds.y);
        particle.isActive = true;
        particle.gameObject.SetActive(true);

        Color col = particle.spriteRenderer.color;
        col.a = 1f;
        particle.spriteRenderer.color = col;
    }

    void UpdateDirtParticles(float dt)
    {
        foreach (var p in dirtPool)
        {
            if (!p.isActive) continue;

            p.lifeTime += dt;
            float t = p.lifeTime / p.maxLifeTime;

            if (t >= 1f)
            {
                p.isActive = false;
                if (p.gameObject != null)
                {
                    p.gameObject.SetActive(false);
                }
            }
            else
            {
                // Match the source's 100 Hz particle update: friction 0.05 and downward acceleration.
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt * SourceTicksPerSecond));
                float step = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    p.velocity *= Mathf.Pow(1f - dirtFrictionPerTick, step * SourceTicksPerSecond);
                    p.velocity.y -= dirtAccelerationY * step;
                    p.position += (Vector3)(p.velocity * step);
                    p.gameObject.transform.Rotate(0f, 0f, p.rotationSpeed * step);
                }
                p.gameObject.transform.position = p.position;

                // XML ParticleAlpha stays at 1 for 70% of life then fades; SystemAlpha fades after 80%.
                Color col = p.spriteRenderer.color;
                float particleAlpha = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                float systemAlpha = (keepAlphaToEnd || progress < 0.8f)
                    ? 1f
                    : Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.8f, 1f, progress));
                col.a = particleAlpha * systemAlpha;
                p.spriteRenderer.color = col;
            }
        }
    }

    void AnimateRoll(float p)
    {
        float easeP = Mathf.Pow(p, 1.8f);
        float currentX = Mathf.Lerp(spawnPosition.x + startX, spawnPosition.x + endX, easeP);
        transform.localPosition = new Vector3(currentX, spawnPosition.y, spawnPosition.z);
        float currentScaleX = Mathf.Lerp(1f, minScale, p);

        if (dirtRollTf != null)
        {
            float currentDirtScaleY = Mathf.Lerp(1f, 1.15f, p) * dirtScale.y;
            dirtRollTf.localScale = new Vector3(currentScaleX * dirtScale.x, currentDirtScaleY, 1f);
            float currentDirtPosY = Mathf.Lerp(0f, -0.123f, p);
            dirtRollTf.localPosition = new Vector3(0f, currentDirtPosY, 0f);
        }

        if (grassRollTf != null)
        {
            grassRollTf.localScale = new Vector3(currentScaleX * grassScale.x, currentScaleX * grassScale.y, 1f);
            grassRollTf.localRotation = Quaternion.Euler(0f, 0f, -easeP * totalRollAngle);

            // Cuộn cỏ bám vào mép dưới thực tế của Sprite thân đất.
            float grassLocalY = -0.755f; // fallback nếu thiếu SpriteRenderer/Sprite
            SpriteRenderer dirtRenderer = dirtRollTf != null
                ? dirtRollTf.GetComponent<SpriteRenderer>()
                : null;
            SpriteRenderer grassRenderer = grassRollTf.GetComponent<SpriteRenderer>();

            if (dirtRenderer != null && dirtRenderer.sprite != null &&
                grassRenderer != null && grassRenderer.sprite != null)
            {
                float dirtBottomY =
                    dirtRollTf.localPosition.y +
                    dirtRenderer.sprite.bounds.min.y * Mathf.Abs(dirtRollTf.localScale.y);

                grassLocalY = dirtBottomY + grassOverlap;
            }

            grassRollTf.localPosition = new Vector3(
                grassRollOffset.x,
                grassLocalY + grassRollOffset.y,
                0f
            );
        }
    }

    public void SetProgress(float p) => progress = Mathf.Clamp01(p);
}