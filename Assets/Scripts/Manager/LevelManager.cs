using UnityEngine;
using System.Collections;
using System.Linq;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    [SerializeField] private int currentLevel;
    public int CurrentLevel => currentLevel;
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
        if (introLevels.Contains(currentLevel))
        {
            yield return introManager.PlayIntro(currentLevel);
        }

        StartGameplay();
    }

    private void StartGameplay()
    {
        // Bắt đầu gameplay
    }
}