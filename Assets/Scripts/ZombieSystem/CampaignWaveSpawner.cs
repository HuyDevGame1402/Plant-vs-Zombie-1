
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CampaignWaveSpawner : MonoBehaviour
{
    [Header("Databases")]
    [SerializeField] private CampaignLevelDatabase levelDatabase;
    [SerializeField] private ZombieDatabase zombieDatabase;

    [Header("Lane spawning")]
    [SerializeField] private CampaignLaneManager laneManager;
    [SerializeField] private GameObject zombieHeadPrefab;

    [Header("Campaign selection")]
    [SerializeField, Min(1)] private int world = 1;
    [SerializeField, Range(1, 10)] private int level = 1;
    [SerializeField] private bool hasFinishedAdventure;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField, Min(0f)] private float horizontalPreviewSpacing = 0.2f;

    private bool hasStarted;
    private bool skipInitialDelay;

    private Coroutine spawningCoroutine;

    private GameObject zombieInit;
    [SerializeField] private CameraIntroController cameraIntroController;


    private void Start()
    {
        // spawn ra zombie idle để ng chs bt level đó có những loại zombie gì 
        if (LevelManager.Instance != null)
        {
            zombieInit = Instantiate(levelDatabase.Levels[LevelManager.Instance.CurrentLevel - 1].zombieIdleShowBeforeGame);
        }

        if(cameraIntroController != null)
        {
            cameraIntroController.OnCameraReturn += DestroyZombieInit;
        }
    }

    public void DestroyZombieInit()
    {
        if(zombieInit == null) return;
        Destroy(zombieInit);
    }

    public void StartSpawning()
    {
        if (hasStarted)
            return;

        spawningCoroutine = StartCoroutine(PlayLevel());
    }

    /// <summary>
    /// Bỏ qua delay đầu Level và spawn wave đầu tiên ngay lập tức.
    /// Chỉ có tác dụng trong lúc đang chờ delay đầu Level.
    /// </summary>
    public void SkipInitialDelay()
    {
        skipInitialDelay = true;
    }

    private IEnumerator PlayLevel()
    {
        if (hasStarted)
            yield break;

        hasStarted = true;

        if (levelDatabase == null || zombieDatabase == null ||
            laneManager == null || zombieHeadPrefab == null)
        {
            Debug.LogError(
                "CampaignWaveSpawner: assign Level Database, Zombie Database, Lane Manager, and Zombie Head Prefab in the Inspector.",
                this);
            yield break;
        }

        CampaignLevelData levelData = levelDatabase.GetLevel(world, level);

        if (levelData == null)
            yield break;

        CampaignTimingData timing = levelDatabase.Timing ?? new CampaignTimingData();

        List<CampaignWavePlan> waves;

        try
        {
            waves = CampaignWaveGenerator.Generate(
                levelData,
                zombieDatabase,
                randomSeed,
                isFirstAdventurePlay: !hasFinishedAdventure,
                hasFinishedAdventure: hasFinishedAdventure);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            yield break;
        }

        int firstDelayTicks = hasFinishedAdventure
            ? levelData.replayInitialDelayTicks
            : levelData.firstPlayInitialDelayTicks;

        if (firstDelayTicks <= 0)
        {
            int globalLevel = levelData.GlobalLevel;

            firstDelayTicks =
                globalLevel == 10 || globalLevel == 20 || globalLevel == 30
                ? timing.miniBossInitialDelayTicks
                : (!hasFinishedAdventure && globalLevel == 2
                    ? timing.level2FirstTimeInitialDelayTicks
                    : timing.standardInitialDelayTicks);
        }

        Debug.Log(
            $"Starting World {world}-{level}: {waves.Count} waves after {firstDelayTicks} ticks",
            this);

        // Initial level delay (can be skipped externally).
        float delay = timing.TicksToSeconds(firstDelayTicks);
        float elapsed = 0f;

        while (elapsed < delay && !skipInitialDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        skipInitialDelay = false;

        // Spawn waves.
        var laneRandom = new System.Random(randomSeed ^ 0x5F3759DF);

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            CampaignWavePlan wave = waves[waveIndex];

            Debug.Log(
                $"Wave {wave.waveNumber}/{waves.Count} — {(wave.isFlagWave ? "FLAG" : "normal")}, {wave.zombies.Count} zombies",
                this);

            // Spawn all zombies in the current wave.
            for (int zombieIndex = 0; zombieIndex < wave.zombies.Count; zombieIndex++)
            {
                ZombieType type = wave.zombies[zombieIndex];

                if (!laneManager.TryPickSpawn(
                    levelData.GlobalLevel,
                    laneRandom,
                    out int laneIndex,
                    out Vector3 spawnPosition))
                {
                    yield break;
                }

                spawnPosition += Vector3.left * (zombieIndex * horizontalPreviewSpacing);

                GameObject spawned = Instantiate(
                    zombieHeadPrefab,
                    spawnPosition,
                    Quaternion.identity);

                spawned.name =
                    $"TestZombie_{type}_L{laneIndex}_W{wave.waveNumber}_{zombieIndex + 1}";

                CampaignZombie campaignZombie = spawned.GetComponent<CampaignZombie>();

                if (campaignZombie == null)
                    campaignZombie = spawned.AddComponent<CampaignZombie>();

                campaignZombie.Initialize(
                    type,
                    laneIndex,
                    wave.waveNumber,
                    laneManager);
            }

            // Delay between waves.
            if (waveIndex < waves.Count - 1)
            {
                CampaignWavePlan nextWave = waves[waveIndex + 1];

                int delayTicks;

                if (nextWave.isFlagWave)
                {
                    delayTicks =
                        timing.flagWaveBetweenDelayTicks +
                        timing.hugeWaveWarningTicks;
                }
                else
                {
                    delayTicks = Random.Range(
                        timing.regularBetweenWaveMinTicks,
                        timing.regularBetweenWaveMaxTicks + 1);
                }

                yield return new WaitForSeconds(
                    timing.TicksToSeconds(delayTicks));
            }
        }

        Debug.Log("All campaign waves finished spawning.", this);
    }
}
