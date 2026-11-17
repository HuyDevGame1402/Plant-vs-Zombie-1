using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }
    [SerializeField] private LevelRewardDatabase levelRewardDatabase;

    public UnLockGUIBase unlockPlant;

    private void Awake()
    {
        Instance = this;
    }

    public void SetupReward(int level)
    {
        
    }
}
