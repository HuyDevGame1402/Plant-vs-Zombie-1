using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ListPlantSelectManager : MonoBehaviour
{
    public static ListPlantSelectManager Instance { get; private set; }

    [SerializeField] private List<PlantData> plantDatas = new List<PlantData>();

    public List<PlantData> PlantDatas => plantDatas;

    private void Awake()
    {
        Instance = this;
    }
}
