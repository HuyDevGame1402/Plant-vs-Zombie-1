using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawn zombie theo level, port của Board::UpdateZombieSpawning / SpawnZombieWave.
/// Thời gian tính bằng "tick" (100 tick = 1 giây, theo CampaignTimingData).
/// </summary>
public class CampaignWaveSpawner : MonoBehaviour
{
    [Header("Databases")]
    [SerializeField] private CampaignLevelDatabase levelDatabase;
    [SerializeField] private ZombieDatabase zombieDatabase;

    [Header("Lane spawning")]
    [SerializeField] private CampaignLaneManager laneManager;
    [Tooltip("Prefab dự phòng khi ZombieDefinition.prefab bị trống.")]
    [SerializeField] private GameObject zombieHeadPrefab;

    [Header("Campaign selection")]
    [Tooltip("Nếu bật và có LevelManager.Instance thì world/level lấy từ LevelManager.CurrentLevel (1..50).")]
    [SerializeField] private bool useLevelManagerSelection = true;
    [SerializeField, Min(1)] private int world = 1;
    [SerializeField, Range(1, 10)] private int level = 1;
    [SerializeField] private bool hasFinishedAdventure;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool randomizeSeedOnAwake = false;

    [Header("Yeti (Board::CanSpawnYetis)")]
    [SerializeField] private bool canSpawnYetis = false;
    [SerializeField] private bool yetiAlreadySeen = false;

    [Header("Special")]
    [Tooltip("Bobsled: số zombie đi kèm leader (game gốc = 3). Đặt 0 nếu prefab đã chứa cả đội.")]
    [SerializeField, Min(0)] private int bobsledExtraMembers = 3;

    [Header("Start")]
    [SerializeField] private CameraIntroController cameraIntroController;
    [Tooltip("Bật nếu level này không phát camera intro: spawner tự chạy ngay khi vào scene.")]
    [SerializeField] private bool startWithoutCameraIntro = false;
    [Tooltip("Chỉ để test: bỏ qua countdown của wave đầu.")]
    [SerializeField] private bool debugSkipInitialCountdown = false;

    // ---- Events cho UI / âm thanh
    public event Action<int, int, bool> OnWaveSpawned;   // (waveNumber, totalWaves, isFlag)
    public event Action OnHugeWaveWarning;               // "A huge wave of zombies is approaching!"
    public event Action OnFinalWaveWarning;              // "Final wave!"
    public event Action OnAllWavesSpawned;

    private const int NextWaveWarningTick = 5;     // ZombieCountDown == 5
    private const int HealthCheckEveryTicks = 10;  // tối ưu: không cần kiểm tra mỗi tick
    private const int MaxTicksPerFrame = 200;

    private GameObject zombieInit;
    private int runtimeSeed;

    // ---- runtime
    private bool running;
    private bool hasStarted;
    private List<CampaignWavePlan> waves;
    private CampaignLevelData levelData;
    private CampaignTimingData timing;
    private System.Random laneRandom;
    private System.Random timingRandom;
    private double tickAccumulator;
    private long tickCounter;

    private int currentWave;            // số wave ĐÃ spawn (= index wave kế tiếp)
    private int countDown;
    private int countDownStart;
    private int hugeWaveCountDown;
    private float healthWaveStart;
    private float healthToNextWave = -1f;
    private bool warnedMissingPrefab;

    public int TotalWaves => waves != null ? waves.Count : 0;
    public int CurrentWaveNumber => currentWave;
    public bool IsRunning => running;

    /// <summary>
    /// Tiến độ level 0..1 cho thanh Zombie Progress Bar.
    /// 0 tại lúc wave 1 spawn (KHÔNG tính countdown đầu level), 1 tại lúc wave cuối spawn.
    /// Giữa 2 wave, tiến độ chạy theo countdown thật (kể cả cảnh báo huge wave và việc rút ngắn theo máu).
    /// </summary>
    public float LevelProgress01
    {
        get
        {
            if (!hasStarted || waves == null || currentWave <= 0)
                return 0f;
            if (currentWave >= waves.Count)
                return 1f;

            bool nextIsFlag = waves[currentWave].isFlagWave;
            int huge = Mathf.Max(1, timing.hugeWaveWarningTicks);

            float span = countDownStart + (nextIsFlag ? huge : 0);
            float remaining = countDown + (nextIsFlag ? (hugeWaveCountDown > 0 ? hugeWaveCountDown : huge) : 0);
            float frac = span > 0f ? Mathf.Clamp01(1f - remaining / span) : 1f;

            return Mathf.Clamp01((currentWave - 1 + frac) / (waves.Count - 1));
        }
    }

    /// <summary>
    /// Vị trí (0..1) của từng wave cờ trên thanh tiến độ, cùng thang đo với LevelProgress01.
    /// </summary>
    public List<float> GetFlagProgressFractions()
    {
        var result = new List<float>();
        if (!ResolveLevel(out CampaignLevelData data) || !TryBuildWaves(data, out List<CampaignWavePlan> plan))
            return result;

        int denom = Mathf.Max(1, plan.Count - 1);
        for (int i = 0; i < plan.Count; i++)
            if (plan[i].isFlagWave)
                result.Add((float)i / denom);

        return result;
    }

    // ------------------------------------------------------------------ Unity

    private void Awake()
    {
        runtimeSeed = randomizeSeedOnAwake ? Environment.TickCount : randomSeed;
    }

    private void Start()
    {
        if (!ResolveLevel(out levelData))
            return;

        if (LevelManager.Instance == null) return;

        level = LevelManager.Instance.CurrentLevel;

        // Spawn zombie "idle" để người chơi biết level có loại zombie nào.
        if (levelData.zombieIdleShowBeforeGame != null)
            zombieInit = Instantiate(levelData.zombieIdleShowBeforeGame);

        if (cameraIntroController != null && !startWithoutCameraIntro)
        {
            cameraIntroController.OnCameraReturn += DestroyZombieInit;

            if (!LevelManager.Instance.IsIntroLevelHD())
            {
                cameraIntroController.OnCameraReturn += StartSpawning;
            }
        }
        // test
        else
        {
            StartSpawning();
        }
    }

    private void OnDestroy()
    {
        if (cameraIntroController != null)
        {
            cameraIntroController.OnCameraReturn -= DestroyZombieInit;
            cameraIntroController.OnCameraReturn -= StartSpawning;
        }
    }

    private void Update()
    {
        if (!running)
            return;

        tickAccumulator += Time.deltaTime * timing.ticksPerSecond;

        int steps = 0;
        while (tickAccumulator >= 1.0 && running && steps < MaxTicksPerFrame)
        {
            tickAccumulator -= 1.0;
            StepTick();
            steps++;
        }

        if (steps >= MaxTicksPerFrame)
            tickAccumulator = 0; // tránh "xoắn ốc" khi frame quá dài
    }

    // ------------------------------------------------------------------ public API

    public void DestroyZombieInit()
    {
        if (zombieInit == null) return;
        Destroy(zombieInit);
        zombieInit = null;
    }

    public void StartSpawning()
    {
        if (hasStarted)
            return;

        if (levelDatabase == null || zombieDatabase == null || laneManager == null)
        {
            Debug.LogError(
                "CampaignWaveSpawner: gán Level Database, Zombie Database và Lane Manager trong Inspector.",
                this);
            return;
        }

        if (!ResolveLevel(out levelData))
            return;

        int g = levelData.GlobalLevel;
        if (CampaignRules.UsesSpecialSpawning(g))
        {
            Debug.LogWarning(
                $"Level {g} (World {levelData.world}-{levelData.level}) dùng cơ chế spawn riêng (Whack-a-Zombie / Boss), spawner thường bị bỏ qua.",
                this);
            return;
        }

        timing = levelDatabase.Timing ?? new CampaignTimingData();

        if (!TryBuildWaves(levelData, out waves))
            return;

        laneRandom = new System.Random(runtimeSeed ^ 0x5F3759DF);
        timingRandom = new System.Random(runtimeSeed ^ 0x2545F491);

        currentWave = 0;
        hugeWaveCountDown = 0;
        healthToNextWave = -1f;
        healthWaveStart = 0f;
        tickCounter = 0;
        tickAccumulator = 0;

        countDown = ResolveFirstCountdownTicks(levelData);
        if (debugSkipInitialCountdown)
            countDown = 1;
        countDownStart = countDown;

        hasStarted = true;
        running = true;

        Debug.Log(
            $"Starting World {levelData.world}-{levelData.level} (global {g}): {waves.Count} waves, first wave in {countDown} ticks",
            this);
    }

    /// <summary>Bỏ qua countdown của wave đầu (chỉ có tác dụng trước khi wave đầu spawn).</summary>
    public void SkipInitialDelay()
    {
        if (!hasStarted)
        {
            debugSkipInitialCountdown = true;
            StartSpawning();
            return;
        }

        if (running && currentWave == 0 && countDown > 1)
            countDown = 1;
    }

    // ------------------------------------------------------------------ countdown core

    /// <summary>Tương đương 1 lần gọi Board::UpdateZombieSpawning.</summary>
    private void StepTick()
    {
        tickCounter++;

        if (hugeWaveCountDown > 0)
        {
            hugeWaveCountDown--;
            if (hugeWaveCountDown == 0)
            {
                RaiseNextWaveComing();
                countDown = 1;
            }
            else
            {
                return;
            }
        }

        if (currentWave >= waves.Count)
        {
            running = false;
            return;
        }

        countDown--;

        // Rút ngắn countdown nếu người chơi đã tiêu diệt đủ zombie của wave trước.
        if (healthToNextWave >= 0f &&
            countDown > timing.healthAccelerationRemainingTicks &&
            countDownStart - countDown > timing.healthAccelerationMinimumElapsedTicks &&
            tickCounter % HealthCheckEveryTicks == 0 &&
            laneManager.GetWaveThreatHealth(currentWave) <= healthToNextWave)
        {
            countDown = timing.healthAccelerationRemainingTicks;
        }

        if (countDown == NextWaveWarningTick)
        {
            if (waves[currentWave].isFlagWave)
            {
                // Huge wave: cảnh báo, chờ hugeWaveWarningTicks rồi spawn.
                hugeWaveCountDown = Mathf.Max(1, timing.hugeWaveWarningTicks);
                OnHugeWaveWarning?.Invoke();
                return;
            }

            RaiseNextWaveComing();
        }

        if (countDown == 0)
            SpawnCurrentWave();
    }

    private void RaiseNextWaveComing()
    {
        if (currentWave + 1 == waves.Count)
            OnFinalWaveWarning?.Invoke();
    }

    private void SpawnCurrentWave()
    {
        CampaignWavePlan wave = waves[currentWave];
        int g = levelData.GlobalLevel;

        Debug.Log(
            $"Wave {wave.waveNumber}/{waves.Count} — {(wave.isFlagWave ? "FLAG" : "normal")}, {wave.zombies.Count} zombies",
            this);

        laneManager.BeginSpawnWave();

        foreach (ZombieType type in wave.zombies)
        {
            if (type == ZombieType.BobsledTeam && !laneManager.CanAddBobsled())
            {
                // Không có vệt băng: game gốc spawn 4 zombie thường thay cho đội bobsled.
                for (int i = 0; i < 4; i++)
                    SpawnZombie(ZombieType.Normal, wave.waveNumber);
            }
            else
            {
                SpawnZombie(type, wave.waveNumber);
            }
        }

        currentWave++;
        healthWaveStart = laneManager.GetWaveThreatHealth(wave.waveNumber);
        OnWaveSpawned?.Invoke(wave.waveNumber, waves.Count, wave.isFlagWave);

        if (currentWave >= waves.Count)
        {
            running = false;
            Debug.Log("All campaign waves finished spawning.", this);
            OnAllWavesSpawned?.Invoke();
            return;
        }

        // Countdown cho wave kế tiếp.
        bool nextIsFlag = waves[currentWave].isFlagWave;
        if (nextIsFlag)
        {
            healthToNextWave = 0f;
            countDown = timing.flagWaveBetweenDelayTicks;
        }
        else
        {
            float t = Mathf.Lerp(
                timing.healthThresholdMinimum,
                timing.healthThresholdMaximum,
                (float)timingRandom.NextDouble());
            healthToNextWave = t * healthWaveStart;

            countDown = CampaignRules.IsLittleTroubleLevel(g)
                ? 750
                : RandomTicks(timing.regularBetweenWaveMinTicks, timing.regularBetweenWaveMaxTicks);
        }

        countDownStart = countDown;
    }

    private int RandomTicks(int min, int maxInclusive)
    {
        if (maxInclusive < min) maxInclusive = min;
        return timingRandom.Next(min, maxInclusive + 1);
    }

    // ------------------------------------------------------------------ spawning

    private void SpawnZombie(ZombieType type, int waveNumber)
    {
        int g = levelData.GlobalLevel;

        if (!laneManager.TryPickSpawn(
                g, type, waveNumber - 1, laneRandom,
                out int laneIndex, out Vector3 position))
        {
            return;
        }

        InstantiateZombie(type, waveNumber, laneIndex, position, 1);

        if (type == ZombieType.BobsledTeam)
        {
            for (int i = 0; i < bobsledExtraMembers; i++)
                InstantiateZombie(type, waveNumber, laneIndex, position + Vector3.right * (0.4f * (i + 1)), i + 2);
        }
    }

    private void InstantiateZombie(ZombieType type, int waveNumber, int laneIndex, Vector3 position, int memberIndex)
    {
        GameObject prefab = ResolvePrefab(type);
        if (prefab == null)
        {
            Debug.LogError($"Không có prefab cho {type}: gán prefab trong ZombieDatabase hoặc Zombie Head Prefab.", this);
            return;
        }

        GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
        spawned.name = $"Zombie_{type}_L{laneIndex}_W{waveNumber}_{memberIndex}";

        CampaignZombie campaignZombie = spawned.GetComponent<CampaignZombie>();
        if (campaignZombie == null)
            campaignZombie = spawned.AddComponent<CampaignZombie>();

        campaignZombie.Initialize(type, laneIndex, waveNumber, laneManager);
    }

    private GameObject ResolvePrefab(ZombieType type)
    {
        if (zombieDatabase != null &&
            zombieDatabase.TryGetZombie(type, out ZombieDefinition def) &&
            def.prefab != null)
        {
            return def.prefab;
        }

        if (!warnedMissingPrefab)
        {
            warnedMissingPrefab = true;
            Debug.LogWarning($"ZombieDefinition của {type} chưa có prefab, dùng Zombie Head Prefab dự phòng.", this);
        }

        return zombieHeadPrefab;
    }

    // ------------------------------------------------------------------ level helpers

    private bool ResolveLevel(out CampaignLevelData data)
    {
        data = null;

        if (levelDatabase == null)
        {
            Debug.LogError("CampaignWaveSpawner: chưa gán Level Database.", this);
            return false;
        }

        if (useLevelManagerSelection && LevelManager.Instance != null)
        {
            int global = Mathf.Clamp(LevelManager.Instance.CurrentLevel, 1, 50);
            world = (global - 1) / 10 + 1;
            level = (global - 1) % 10 + 1;
        }

        data = levelDatabase.GetLevel(world, level);
        return data != null;
    }

    private bool TryBuildWaves(CampaignLevelData data, out List<CampaignWavePlan> result)
    {
        result = null;
        if (levelDatabase == null || zombieDatabase == null || data == null)
            return false;

        try
        {
            result = CampaignWaveGenerator.Generate(
                data,
                zombieDatabase,
                runtimeSeed,
                hasFinishedAdventure,
                canSpawnYetis,
                yetiAlreadySeen);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private int ResolveFirstCountdownTicks(CampaignLevelData data)
    {
        int ticks = hasFinishedAdventure
            ? data.replayInitialDelayTicks
            : data.firstPlayInitialDelayTicks;

        if (ticks > 0)
            return ticks;

        int g = data.GlobalLevel;
        if (g == 10 || g == 20 || g == 30)
            return timing.miniBossInitialDelayTicks;
        if (!hasFinishedAdventure && g == 2)
            return timing.level2FirstTimeInitialDelayTicks;
        return timing.standardInitialDelayTicks;
    }

    // ------------------------------------------------------------------ thống kê (UI progress bar...)

    public int CalculateCurrentLevelFlagWaveCount()
    {
        if (!ResolveLevel(out CampaignLevelData data) || !TryBuildWaves(data, out List<CampaignWavePlan> plan))
            return 0;

        int count = 0;
        foreach (CampaignWavePlan wave in plan)
            if (wave.isFlagWave)
                count++;
        return count;
    }

    /// <summary>
    /// Thời gian ước tính (giây) từ wave đầu đến wave cuối, dùng delay trung bình
    /// và không tính countdown đầu level / hiệu ứng rút ngắn theo máu.
    /// </summary>
    public float CalculateCurrentLevelDuration()
    {
        if (!ResolveLevel(out CampaignLevelData data) || !TryBuildWaves(data, out List<CampaignWavePlan> plan))
            return 0f;

        CampaignTimingData t = levelDatabase.Timing ?? new CampaignTimingData();
        int g = data.GlobalLevel;
        int totalTicks = 0;

        for (int i = 0; i < plan.Count - 1; i++)
        {
            if (plan[i + 1].isFlagWave)
            {
                // flagWaveBetween - 5 tick, + huge wave warning, + 1 tick
                totalTicks += t.flagWaveBetweenDelayTicks - NextWaveWarningTick + t.hugeWaveWarningTicks + 1;
            }
            else
            {
                totalTicks += CampaignRules.IsLittleTroubleLevel(g)
                    ? 750
                    : (t.regularBetweenWaveMinTicks + t.regularBetweenWaveMaxTicks) / 2;
            }
        }

        float seconds = t.TicksToSeconds(totalTicks);
        Debug.Log($"[Duration] World {data.world}-{data.level}: {plan.Count} waves, ~{seconds:F1}s");
        return seconds;
    }
}