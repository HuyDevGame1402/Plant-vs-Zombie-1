
using UnityEngine;

public abstract class LevelReward : ScriptableObject
{
    [Header("Reward Information")]
    [SerializeField] private string rewardID;

    [SerializeField] private Sprite unlockImage;
    [SerializeField] private Sprite detailImage;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    public string RewardID => rewardID;
    public Sprite UnlockImage => unlockImage;
    public Sprite DetailImage => detailImage;
    public string Description => description;

    // Hiển thị phần thưởng sau khi hoàn thành Level
    public abstract void ShowAfterLevel();

    // Hiển thị thông tin chi tiết phần thưởng
    public abstract void ShowDetails();
}
