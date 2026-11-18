using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    [SerializeField] private LevelRewardDatabase levelRewardDatabase;

    public UnLockGUIBase unlockPlant;

    [SerializeField] private CampaignLevelCompletion campaignLevelCompletion;

    [SerializeField] private LevelReward reward;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        campaignLevelCompletion.OnLevelCleared += HandleLevelCleared;
    }

    private void HandleLevelCleared(Vector3 posZombieDead)
    {
        SetupReward();
    }

    public void SetupReward()
    {
        int level = LevelManager.Instance.CurrentLevel + 1;
        if (levelRewardDatabase == null)
        {
            Debug.LogError("LevelRewardDatabase chưa được gán!");
            return;
        }

        // Lấy reward theo level
        reward = levelRewardDatabase.GetReward(level);

        if (reward == null)
        {
            Debug.LogWarning($"Level {level} không có phần thưởng!");
            return;
        }

        // Xử lý phần thưởng tại đây
        Debug.Log($"Đã lấy reward của level {level}");

        reward.ShowAfterLevel();
    }

    public void ActiveDetailReward()
    {
        reward.ShowDetails();
    }
}