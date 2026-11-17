using UnityEngine;
using PlantReanimation.Controllers.Sunflower;

public class Sunflower : Plant, IIdle, IProduce
{
    [Header("References")]
    [SerializeField] private SunflowerController sunflowerController;
    [SerializeField] private SunGlowFlash glowFlash;

    [Header("Sun (GUI)")]
    [Tooltip("SunPool trong scene (đã có prefab + container + collectTarget).")]
    [SerializeField] private SunPool sunPool;

    [Tooltip("Camera nhìn thấy cây. Để trống = Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("Camera của Canvas. Để trống nếu Canvas là Screen Space - Overlay.")]
    [SerializeField] private Camera uiCamera;

    [Tooltip("Dự phòng khi không có SunGlowFlash. Trống = vị trí cây.")]
    [SerializeField] private Transform sunSpawnPoint;

    [Header("Sun Pop (đơn vị Canvas)")]
    [SerializeField] private Vector2 landOffsetMin = new Vector2(-70f, -70f);
    [SerializeField] private Vector2 landOffsetMax = new Vector2(70f, -20f);
    [Tooltip("Độ cao nảy lên thêm so với đường thẳng start -> land.")]
    [SerializeField] private float popHeight = 40f;
    [SerializeField] private float popDuration = 0.6f;
    [Tooltip("Scale lúc mới sinh ra so với scale gốc (0.75 = 75%).")]
    [SerializeField, Range(0.1f, 1f)] private float startScaleMul = 0.75f;

    [Header("Timing")]
    [Tooltip("Số giây của một game tick.")]
    [SerializeField, Min(0f)] private float secondsPerGameTick = 0.01f;

    [SerializeField, Min(0)] private int firstSunMinTicks = 700;
    [SerializeField, Min(0)] private int firstSunMaxTicks = 1000;

    [Tooltip("Sai số ± tick cho các chu kỳ sau (0 = đúng launchRateTicks).")]
    [SerializeField, Min(0)] private int cycleJitterTicks = 0;

    [Header("Glow")]
    [SerializeField, Min(0.01f)] private float glowLeadSeconds = 1.2f;
    [SerializeField, Min(0.01f)] private float glowFadeSeconds = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [Tooltip("Tắt = chỉ xem glow, không sinh sun.")]
    [SerializeField] private bool spawnSunEnabled = true;

    [Header("Runtime (Read Only)")]
    [SerializeField] private float cycleRemaining;
    [SerializeField] private float produceTimer;

    private IdleState idleState;
    private ProduceState produceState;

    protected override void Start()
    {
        int min = Mathf.Min(firstSunMinTicks, firstSunMaxTicks);
        int max = Mathf.Max(firstSunMinTicks, firstSunMaxTicks);
        cycleRemaining = Random.Range(min, max + 1) * secondsPerGameTick;

        if (debugLog)
        {
            Debug.Log(
                $"[Sunflower] Start | first sun in {cycleRemaining:F2}s | " +
                $"glowFlash={(glowFlash != null)} | sunPool={(sunPool != null)}",
                this);
        }

        base.Start();
    }

    private void OnDisable()
    {
        SetGlow(0f);
    }

    private void SetGlow(float value)
    {
        if (glowFlash != null)
            glowFlash.SetIntensity(value);
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    protected override void InitializeStates()
    {
        idleState = new IdleState(this, this);
        produceState = new ProduceState(this, this);

        stateMachine.ChangeState(idleState);
    }

    private float NextCycleSeconds()
    {
        int baseTicks = plantData != null ? plantData.launchRateTicks : 2500;
        int ticks = baseTicks + Random.Range(-cycleJitterTicks, cycleJitterTicks + 1);
        return Mathf.Max(0, ticks) * secondsPerGameTick;
    }

    // =========================================================
    // IDLE
    // =========================================================

    public void OnIdleEnter()
    {
        if (debugLog)
            Debug.Log($"[Sunflower] Enter Idle | next sun in {cycleRemaining:F2}s", this);
    }

    public void OnIdleUpdate()
    {
        cycleRemaining -= Time.deltaTime;

        if (cycleRemaining <= glowLeadSeconds)
            SetGlow(1f - Mathf.Clamp01(cycleRemaining / glowLeadSeconds));

        if (cycleRemaining <= 0f)
            stateMachine.ChangeState(produceState);
    }

    public void OnIdleExit() { }

    // =========================================================
    // PRODUCE
    // =========================================================

    public void OnProduceEnter()
    {
        if (debugLog)
            Debug.Log("[Sunflower] Enter Produce", this);

        if (spawnSunEnabled)
            SpawnSun();

        cycleRemaining = NextCycleSeconds();
        produceTimer = glowFadeSeconds;

        SetGlow(1f);
    }

    public void OnProduceUpdate()
    {
        produceTimer -= Time.deltaTime;

        SetGlow(Mathf.Clamp01(produceTimer / glowFadeSeconds));

        if (produceTimer <= 0f)
            stateMachine.ChangeState(idleState);
    }

    public void OnProduceExit()
    {
        SetGlow(0f);
    }

    // =========================================================
    // SPAWN SUN (dùng SunPool)
    // =========================================================

    private void SpawnSun()
    {
        if (sunPool == null || sunPool.container == null)
        {
            Debug.LogError("[Sunflower] Chưa gán SunPool (hoặc SunPool chưa có container)!", this);
            return;
        }

        SunUI sun = sunPool.Get();

        Vector3 spawnWorld =
            glowFlash != null ? glowFlash.AnchorWorldPosition :
            (sunSpawnPoint != null ? sunSpawnPoint : transform).position;

        Vector2 start = WorldToContainerLocal(spawnWorld);

        Vector2 land = start + new Vector2(
            Random.Range(landOffsetMin.x, landOffsetMax.x),
            Random.Range(landOffsetMin.y, landOffsetMax.y));

        sun.PopFrom(start, land, popHeight, popDuration, startScaleMul);

        if (debugLog)
            Debug.Log($"[Sunflower] Spawn sun at {start} -> {land}", sun);
    }

    private Vector2 WorldToContainerLocal(Vector3 worldPos)
    {
        Camera cam = worldCamera != null ? worldCamera : Camera.main;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sunPool.container, screen, uiCamera, out Vector2 local);

        return local;
    }

    // =========================================================
    // TEST
    // =========================================================

    [ContextMenu("Force Produce Now")]
    private void ForceProduceNow()
    {
        cycleRemaining = glowLeadSeconds;
    }
}