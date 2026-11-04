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

    private PlantCooldownUI plantCooldownUI;
    public PlantCooldownUI PlantCooldownUI => plantCooldownUI;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClickChoosePlant);
        plantCooldownUI = GetComponent<PlantCooldownUI>();
        plantCooldownUI.OnCooldownCompleteEvent += ActivePlantClick;
    }

    private void Start()
    {
        if(SunMoneyManager.Instance != null)
        {
            SunMoneyManager.Instance.OnSunMoneyChanged += UpdatePlantSelectGUI;
        }
    }

    private void UpdatePlantSelectGUI(int sunMoneyCurrent)
    {
        if (SunMoneyManager.Instance.CheckEnoughSunMoney(plantData.seedCostSun))
        {
            plantGrowBG.gameObject.SetActive(false);
        }
        else
        {
            plantGrowBG.gameObject.SetActive(true);
        }
    }

    private void OnClickChoosePlant()
    {
        if (hasOnChoosePlant == false) return;

        OnChoosePlantEvent?.Invoke(this, plantData);

        if(CreatePlantManager.Instance != null)
        {
            CreatePlantManager.Instance.SetupPlantSelectSpawn(this, plantData);
        }
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

    public void DisablePlantClick()
    {
        hasOnChoosePlant = false;
    }

    private void ActivePlantClick()
    {
        hasOnChoosePlant = true;
    }
}
