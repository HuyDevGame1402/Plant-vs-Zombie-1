using UnityEngine;
using System.Collections.Generic;

public class LogicSelectPlantUI : MonoBehaviour
{
    [SerializeField] private GameObject plantSelectGameObject;

    [SerializeField] private List<ButtonSelectPlant> listButtonSelectPlant = new List<ButtonSelectPlant>();
    [SerializeField] private ButtonSelectPlant plantSelect;

    private void Awake()
    {
        for(int i = 0; i < plantSelectGameObject.transform.childCount; i++)
        {
            listButtonSelectPlant.Add(plantSelectGameObject.transform.GetChild(i).GetChild(0).GetComponent<ButtonSelectPlant>());
        }

        for(int i = 0; i < listButtonSelectPlant.Count; i++)
        {
            listButtonSelectPlant[i].OnChoosePlantEvent += OnClickedPlantGUI;
        }
    }

    private void OnClickedPlantGUI(ButtonSelectPlant buttonSelectPlant, PlantData plantData)
    {
        Debug.Log("Clicked Plant: " + buttonSelectPlant.name);
        if (plantSelect == null)
        {
            plantSelect = buttonSelectPlant;
            buttonSelectPlant.ActiveSelectGUIPlant(true);
        }
        else
        {
            if(plantSelect == buttonSelectPlant)
            {
                plantSelect = null;
                buttonSelectPlant.ActiveSelectGUIPlant(false);
            }
            else
            {
                plantSelect.ActiveSelectGUIPlant(false);
                plantSelect = buttonSelectPlant;
                plantSelect.ActiveSelectGUIPlant(true);
            }
        }
    }

    private void Start()
    {
        // test thu
        OnLoadPlantUpGUI();


        // event
        if (CreatePlantManager.Instance != null)
        {
            CreatePlantManager.Instance.OnCreatedPlantEvent += ResetPlantSelectGUI;
        }
    }

    private void ResetPlantSelectGUI()
    {
        plantSelect = null;
    }


    // gọi khi start game để cập nhật danh sách cây trồng lên trên ds gui để vào game có thể chọn cây để trồng
    private void OnLoadPlantUpGUI()
    {
        if(ListPlantSelectManager.Instance == null) return;

        for(int i = 0; i < ListPlantSelectManager.Instance.PlantDatas.Count; i++)
        {
            plantSelectGameObject.transform.GetChild(i).GetChild(0).GetComponent
                <ButtonSelectPlant>().SetPlantData(plantData: ListPlantSelectManager.Instance.PlantDatas[i]);
            plantSelectGameObject.transform.GetChild(i).gameObject.SetActive(true);
        }
    }
}
