using UnityEngine;

[CreateAssetMenu(
    fileName = "PlantReward",
    menuName = "PvZ/Rewards/Plant Reward"
)]
public class PlantReward : LevelReward
{
    [SerializeField] private PlantData plantData;

    public PlantData PlantData => plantData;

    public override void ShowAfterLevel()
    {
        if(RewardManager.Instance != null)
        {
            RewardManager.Instance.unlockPlant.SetupNewPlant(this);
        }
    }

    public override void ShowDetails()
    {
        if (RewardManager.Instance != null)
        {
            RewardManager.Instance.unlockPlant.ActiveDetail();
        }
    }
}
