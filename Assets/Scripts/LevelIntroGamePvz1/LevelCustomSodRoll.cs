using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Script điều khiển cuộn cỏ và hiệu ứng hạt đất nâu rơi:
/// - Tăng cường số lượng, tốc độ rơi mượt mà.
/// - Thời gian sống/biến mất random từ 0.1s - 1.0s.
/// - Thêm hiệu ứng xoay (rotation) ngẫu nhiên cả chiều thuận và nghịch khi hạt rơi.
/// </summary>
public class LevelSodRollMover : MonoBehaviour
{
    [Header("Cấu hình Atlas & Dữ liệu")]
    public Texture2D atlasTexture;           // LoadingImages_PlantsZombies_2_960_00.png
    public TextAsset atlasJson;              // LoadingImages_PlantsZombies_2_960.json

    [Header("Tùy chỉnh Quãng đường Lăn")]
    public float pixelsPerUnit = 100f;
    [Tooltip("Vị trí bắt đầu (đặt là 0 để đứng đúng chỗ hiện tại trên Scene)")]
    public float startX = 0f;
    [Tooltip("Vị trí kết thúc bên phải sân")]
    public float endX = 6f;

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

    [Header("⚙️ TÙY CHỈNH HIỆU ỨNG HẠT ĐẤT")]
    [Tooltip("Tần suất sinh chùm hạt đất (giây/lần)")]
    public float dirtSpawnInterval = 0.03f;

    [Tooltip("Số lượng hạt đất sinh ra mỗi đợt")]
    public int particlesPerSpawn = 25;

    [Tooltip("Quãng đường rơi của hạt đất (Chỉnh nhỏ để hạt rơi chậm rãi)")]
    public float dirtFallDistance = 0.15f;

    [Header("⏳ TÙY CHỈNH THỜI GIAN SỐNG & BIẾN MẤT")]
    [Tooltip("Thời gian sống ngắn nhất của hạt (giây)")]
    public float minLifeTime = 0.1f;

    [Tooltip("Thời gian sống dài nhất của hạt (giây)")]
    public float maxLifeTime = 1.0f;

    [Header("🌀 TÙY CHỈNH XOAY HẠT ĐẤT (ROTATION)")]
    [Tooltip("Tốc độ quay tối thiểu (Độ/giây)")]
    public float minRotationSpeed = -180f;

    [Tooltip("Tốc độ quay tối đa (Độ/giây). Giá trị âm/dương sẽ quyết định chiều quay ngẫu nhiên.")]
    public float maxRotationSpeed = 180f;

    [Header("Sorting Order (Hiển thị cao nhất)")]
    [Tooltip("Đặt từ 13 trở lên để cao hơn thân đất và cỏ xanh")]
    public int dirtParticleSortingOrder = 15;
    public int baseSortingOrder = 10;

    [Header("Tiến độ & Chạy thử")]
    public bool autoPlay = true;
    [Range(0f, 1f)]
    [Tooltip("Kéo thanh này bằng tay để test vị trí và hiệu ứng bất cứ lúc nào")]
    public float progress = 0f;

    [Tooltip("Thời gian chạy hoàn thành (giây) khi bật autoPlay")]
    public float duration = 5f;

    private Transform dirtRollTf;   // Thân cuộn đất
    private Transform grassRollTf;  // Cuộn cỏ xanh tròn
    private float timer;
    private Vector3 spawnPosition;  // Vị trí gốc trên Scene

    // Danh sách Sprite các hạt đất nâu (vừa và nhỏ)
    private readonly List<Sprite> dirtParticleSprites = new List<Sprite>();

    [Header("⚙️ TÙY CHỈNH TỐC ĐỘ QUAY CỎ")]
    [Tooltip("Tổng số độ xoay của cuộn cỏ xanh từ đầu đến cuối (360 = 1 vòng, 720 = 2 vòng...)")]
    public float totalRollAngle = 720f;

    private bool isCompleted = false;

    // Hệ thống Object Pool cho hạt đất rơi (Độc lập chuyển động & Xoay)
    private class DirtParticle
    {
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public Vector3 startPos;
        public float lifeTime;
        public float maxLifeTime;
        public float rotationSpeed; // Tốc độ và chiều quay riêng cho từng hạt
        public bool isActive;
    }
    private readonly List<DirtParticle> dirtPool = new List<DirtParticle>();
    private float dirtSpawnTimer;

    void Awake()
    {
        spawnPosition = transform.localPosition;
        BuildRollObjects();
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
            // Chỉ lấy đất nâu từ IMAGE_DIRTSMALL và IMAGE_DIRTBIG
            else if (cleanId == "IMAGE_DIRTSMALL" || cleanId == "IMAGE_DIRTBIG")
            {
                float cellW = w / cols;
                float cellH = h / rows;

                bool isBigDirtSheet = (cleanId == "IMAGE_DIRTBIG");

                for (int rIdx = 0; rIdx < rows; rIdx++)
                {
                    for (int cIdx = 0; cIdx < cols; cIdx++)
                    {
                        if (isBigDirtSheet && (cellW > 60 || cellH > 60)) continue;

                        float cx = x + (cIdx * cellW);
                        float cy = y + (rIdx * cellH);
                        var subRect = new Rect(cx, atlasTexture.height - cy - cellH, cellW, cellH);
                        dirtParticleSprites.Add(Sprite.Create(atlasTexture, subRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect));
                    }
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

        // --- Tạo GameObject con 2: Cuộn Cỏ Xanh Tròn ---
        var grassGo = new GameObject("GrassRoll_Cap");
        grassGo.transform.SetParent(transform, false);
        var grassSr = grassGo.AddComponent<SpriteRenderer>();
        grassSr.sprite = grassGreenSprite;
        grassSr.sortingOrder = baseSortingOrder + 2;
        grassRollTf = grassGo.transform;

        AnimateRoll(progress);
    }

    void Update()
    {
        // 1. Logic chạy animation cuộn cỏ và sinh hạt đất (chỉ chạy khi chưa hoàn thành)
        if (!isCompleted)
        {
            if (autoPlay && !Mathf.Approximately(duration, 0f))
            {
                timer += Time.deltaTime;
                progress = Mathf.Clamp01(timer / duration);
            }

            AnimateRoll(progress);

            // Sinh hạt đất liên tục đến sát mốc cuối (0.999f)
            if (dirtParticleSprites.Count > 0 && progress > 0f && progress < 0.999f)
            {
                dirtSpawnTimer += Time.deltaTime;
                if (dirtSpawnTimer >= dirtSpawnInterval)
                {
                    dirtSpawnTimer = 0f;
                    for (int i = 0; i < particlesPerSpawn; i++)
                    {
                        SpawnDirtParticle();
                    }
                }
            }

            // Kiểm tra khi tiến độ chạm đích hoàn toàn
            if (progress >= 0.9999f && !isCompleted)
            {
                isCompleted = true;
                StartCoroutine(HideRollObjectsWithDelay(0f)); // Chờ 0.5s rồi mới ẩn thân đất & cuộn cỏ
            }
        }

        // 2. 🌟 LUÔN CẬP NHẬT HẠT ĐẤT: Đặt ở ngoài khối !isCompleted để các hạt đất đang rơi dở
        // tiếp tục rơi, xoay và mờ dần alpha về 0 theo đúng vòng đời mà không bị đứng đơ hay phụ thuộc vào cuộn đất.
        UpdateDirtParticles(Time.deltaTime);
    }

    // Coroutine xử lý độ trễ và ẩn các đối tượng + hạt đất
    private System.Collections.IEnumerator HideRollObjectsWithDelay(float delay)
    {
        // Chờ đúng số giây delay (0.5s) để người chơi thấy rõ điểm dừng cuối
        yield return new WaitForSeconds(delay);

        // Ẩn thân đất cuộn và cuộn cỏ xanh
        if (dirtRollTf != null) dirtRollTf.gameObject.SetActive(false);
        if (grassRollTf != null) grassRollTf.gameObject.SetActive(false);

        // Đảm bảo ẩn nốt bất kỳ hạt đất nào còn sót lại đang active trong pool
        foreach (var p in dirtPool)
        {
            if (p != null && p.isActive)
            {
                p.isActive = false;
                if (p.gameObject != null)
                {
                    p.gameObject.SetActive(false);
                }
            }
        }
    }

    // --- Quản lý Object Pool & Hiệu ứng hạt rơi, xoay ngẫu nhiên ---
    void SpawnDirtParticle()
    {
        if (dirtRollTf == null || grassRollTf == null || dirtParticleSprites.Count == 0) return;

        DirtParticle particle = null;
        foreach (var p in dirtPool)
        {
            if (!p.isActive)
            {
                particle = p;
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

        float topY = dirtRollTf.position.y + 0.55f;
        float bottomY = grassRollTf.position.y - 0.3f;
        float randomY = Random.Range(bottomY, topY);

        float spawnX = transform.position.x + Random.Range(-0.18f, 0.05f);

        particle.startPos = new Vector3(spawnX, randomY, transform.position.z);
        particle.gameObject.transform.position = particle.startPos;

        // Random góc xoay ban đầu ngẫu nhiên từ 0 đến 360 độ
        particle.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // Random tốc độ và chiều quay (cùng chiều hoặc ngược chiều kim đồng hồ)
        particle.rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);

        particle.lifeTime = 0f;
        particle.maxLifeTime = Random.Range(minLifeTime, maxLifeTime);
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
                // Rơi chậm rãi theo quãng đường ngắn
                float currentY = p.startPos.y - (t * dirtFallDistance);
                p.gameObject.transform.position = new Vector3(p.startPos.x, currentY, p.startPos.z);

                // Xoay vòng hạt đất liên tục theo tốc độ và chiều đã random
                p.gameObject.transform.Rotate(0f, 0f, p.rotationSpeed * dt);

                // Mờ dần alpha tương ứng theo vòng đời sống của từng hạt
                Color col = p.spriteRenderer.color;
                col.a = Mathf.Lerp(1f, 0f, t);
                p.spriteRenderer.color = col;
            }
        }
    }

    void AnimateRoll(float p)
    {
        // Đồng bộ hàm mũ easing (dùng chung 1.8f giống bên thảm cỏ để tốc độ tăng/giảm khớp nhau từng khung hình)
        float easeP = Mathf.Pow(p, 1.8f);

        // Dùng easeP thay vì p thuần để vị trí di chuyển ngang cũng có độ trễ/gia tốc giống hiệu ứng mở thảm cỏ
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

            // Góc xoay cuộn cỏ chạy theo easeP để đồng điệu tốc độ lăn
            float rollAngle = -easeP * totalRollAngle;
            grassRollTf.localRotation = Quaternion.Euler(0f, 0f, rollAngle);

            float interpolatedGrassPosY = Mathf.Lerp(-0.755f, -1.005f, p);
            Vector3 finalPos = new Vector3(grassRollOffset.x, interpolatedGrassPosY + grassRollOffset.y, 0f);
            grassRollTf.localPosition = finalPos;
        }
    }

    public void SetProgress(float p) => progress = Mathf.Clamp01(p);
}