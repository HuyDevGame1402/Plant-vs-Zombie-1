using UnityEngine.UI;
using UnityEngine;
using System;
using TMPro;

public class ButtonSelectPlant : MonoBehaviour
{
    [SerializeField] private PlantData plantData;

    public event Action<ButtonSelectPlant, PlantData> OnChoosePlantEvent;

    [SerializeField] private bool hasOnChoosePlant;

    [Header("GUI Infomation Plant(Image and Cost)")]
    [SerializeField] private Image plantImage;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image plantGrowBG;
    [SerializeField] private GameObject borderSlectPlantGUI;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClickChoosePlant);
    }

    private void OnClickChoosePlant()
    {
        if (hasOnChoosePlant == false) return;

        OnChoosePlantEvent?.Invoke(this, plantData);
    }

    public void SetPlantData(PlantData plantData)
    {
        this.plantData = plantData;
        plantImage.sprite = plantData.imageSelectGUI;
        costText.text = plantData.seedCostSun.ToString();
        
        if(SunMoneyManager.Instance != null)
        {
            hasOnChoosePlant = SunMoneyManager.Instance.CheckEnoughSunMoney(
                plantData.seedCostSun);
            plantGrowBG.gameObject.SetActive(!SunMoneyManager.Instance.CheckEnoughSunMoney(
                plantData.seedCostSun));
        }
    }

    public void ActiveSelectGUIPlant(bool isActive)
    {
        borderSlectPlantGUI.SetActive(isActive);
    }
}
