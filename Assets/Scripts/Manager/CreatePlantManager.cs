using UnityEngine;
using System;

public class CreatePlantManager : MonoBehaviour
{
    public static CreatePlantManager Instance { get; private set; }

    [SerializeField] private GameObject plantPrefab;

    private PlantData plantDataSelected;
    private ButtonSelectPlant buttonSelectPlant;

    public event Action OnCreatedPlantEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    } 
    
    public void SetupPlantSelectSpawn(ButtonSelectPlant buttonSelectPlant, PlantData plantData)
    {
        plantDataSelected = plantData;
        this.buttonSelectPlant = buttonSelectPlant;
    }

    public void CreatePlant(Vector3 position)
    {
        if (plantDataSelected != null)
        {
            Instantiate(plantDataSelected.plantPrefab, position, Quaternion.identity);
            buttonSelectPlant.ActiveSelectGUIPlant(false);

            if(SunMoneyManager.Instance != null)
            {
                SunMoneyManager.Instance.SpendSunMoney(plantDataSelected.seedCostSun);
            }

            buttonSelectPlant.PlantCooldownUI.StartCooldown(plantDataSelected.seedPacketRefreshTicks);
            buttonSelectPlant.DisablePlantClick();
            plantDataSelected = null;
            buttonSelectPlant = null;
            OnCreatedPlantEvent?.Invoke();
        }
        else
        {
            Debug.LogWarning("Plant prefab is not assigned in CreatePlantManager.");
        }
    }
}
