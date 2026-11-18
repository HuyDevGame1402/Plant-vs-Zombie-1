using UnityEngine.UI;
using TMPro;
using UnityEngine;

public class UnLockGUIBase : MonoBehaviour
{
    [SerializeField] private GameObject main;
    [SerializeField] private GameObject infomationPlantGameObject;

    [Header("Base")]
    [SerializeField] private TextMeshProUGUI cost;
    [SerializeField] private Image icon;

    [Header("Infomation")]
    [SerializeField] private Image iconVector;
    [SerializeField] private TextMeshProUGUI namePlant;
    [SerializeField] private TextMeshProUGUI descriptionPlant;

    [SerializeField] private PlantReward plantReward;
    
    public void SetupNewPlant(PlantReward plantReward)
    {
        cost.text = plantReward.PlantData.seedCostSun.ToString();
        icon.sprite = plantReward.UnlockImage;
        main.SetActive(true);
        this.plantReward = plantReward;
    }

    public void ActiveDetail()
    {
        main.SetActive(false);

        if (iconVector == null)
        {
            Debug.LogError("iconVector đang bị NULL!");
            return;
        }

        if (plantReward == null)
        {
            Debug.LogError("plantReward đang bị NULL!");
            return;
        }

        if (plantReward.DetailImage == null)
        {
            Debug.LogError("DetailImage đang bị NULL!");
            return;
        }

        iconVector.sprite = plantReward.DetailImage;
        namePlant.text = plantReward.PlantData.displayName;
        descriptionPlant.text = plantReward.Description;
        infomationPlantGameObject.SetActive(true);
    }

}
