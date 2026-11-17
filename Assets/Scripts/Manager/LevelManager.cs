
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level")]
    [SerializeField] private int currentLevel;
    public int CurrentLevel => currentLevel;

    [Header("Level Configurations")]
    [SerializeField] private List<LevelPlantConfig> levelPlantConfigs;

    [Header("Intro")]
    [SerializeField] private int[] introLevels;
    [SerializeField] private IntroManager introManager;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartLevel();
    }

    public void StartLevel()
    {
        StartCoroutine(InitializeLevel());
    }

    private IEnumerator InitializeLevel()
    {
        InitializePlants();

        if (IsIntroLevel())
        {
            yield return introManager.PlayIntro(currentLevel);
        }

        StartGameplay();
    }

    private void InitializePlants()
    {
        LevelPlantConfig config = levelPlantConfigs
            .FirstOrDefault(x => x != null && x.level == currentLevel);

        if (config != null && config.useFixedPlants)
        {
            ListPlantSelectManager.Instance.SetFixedPlants(
                config.fixedPlants
            );
        }
        else
        {
            ListPlantSelectManager.Instance.LoadSelectedPlants();
        }
    }

    public bool IsIntroLevel()
    {
        return introLevels.Contains(currentLevel);
    }

    private void StartGameplay()
    {
        // Bắt đầu gameplay
    }
}
