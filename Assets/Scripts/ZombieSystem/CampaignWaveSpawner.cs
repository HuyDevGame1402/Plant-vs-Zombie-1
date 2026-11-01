using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Prototype runner: plays Adventure level 1-1 on a single lane using one test zombie prefab.</summary>
public class CampaignWaveSpawner : MonoBehaviour
{
    [Header("Databases")]
    [SerializeField] private CampaignLevelDatabase levelDatabase;
    [SerializeField] private ZombieDatabase zombieDatabase;

    [Header("Test lane")]
    [SerializeField] private GameObject zombieHeadPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Level 1-1 test settings")]
    [SerializeField, Min(1)] private int world = 1;
    [SerializeField, Range(1, 10)] private int level = 1;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField, Min(0f)] private float delayBetweenZombies = 1.5f;
    [SerializeField, Min(0f)] private float delayBetweenWaves = 8f;
    [SerializeField, Min(0f)] private float horizontalPreviewSpacing = 0.2f;

    private bool hasStarted;

    private void Start()
    {
        StartCoroutine(PlayLevel());
    }

    private IEnumerator PlayLevel()
    {
        if (hasStarted)
            yield break;
        hasStarted = true;

        if (levelDatabase == null || zombieDatabase == null || zombieHeadPrefab == null || spawnPoint == null)
        {
            Debug.LogError(
                "CampaignWaveSpawner: assign Level Database, Zombie Database, Zombie Head Prefab, and Spawn Point in the Inspector.",
                this);
            yield break;
        }

        CampaignLevelData levelData = levelDatabase.GetLevel(world, level);
        if (levelData == null)
            yield break;

        List<CampaignWavePlan> waves;
        try
        {
            waves = CampaignWaveGenerator.Generate(
                levelData,
                zombieDatabase,
                randomSeed,
                isFirstAdventurePlay: true,
                hasFinishedAdventure: false);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            yield break;
        }

        Debug.Log($"Starting World {world}-{level}: {waves.Count} waves", this);

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            CampaignWavePlan wave = waves[waveIndex];
            Debug.Log(
                $"Wave {wave.waveNumber}/{waves.Count} — {(wave.isFlagWave ? "FLAG" : "normal")}, {wave.zombies.Count} zombies",
                this);

            for (int zombieIndex = 0; zombieIndex < wave.zombies.Count; zombieIndex++)
            {
                ZombieType type = wave.zombies[zombieIndex];
                Vector3 previewOffset = Vector3.left * (zombieIndex * horizontalPreviewSpacing);
                GameObject spawned = Instantiate(zombieHeadPrefab, spawnPoint.position + previewOffset, spawnPoint.rotation);
                spawned.name = $"TestZombie_{type}_W{wave.waveNumber}_{zombieIndex + 1}";

                if (delayBetweenZombies > 0f)
                    yield return new WaitForSeconds(delayBetweenZombies);
            }

            if (waveIndex < waves.Count - 1 && delayBetweenWaves > 0f)
                yield return new WaitForSeconds(delayBetweenWaves);
        }

        Debug.Log("Level 1-1 test waves finished spawning.", this);
    }
}
