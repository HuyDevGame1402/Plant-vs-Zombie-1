
using System.Collections.Generic;
using UnityEngine;

public class ListPlantSelectManager : MonoBehaviour
{
    public static ListPlantSelectManager Instance { get; private set; }

    [SerializeField] private List<PlantData> plantDatas = new();

    public List<PlantData> PlantDatas => plantDatas;

    private void Awake()
    {
        Instance = this;
    }

    public void SetFixedPlants(List<PlantData> fixedPlants)
    {
        plantDatas.Clear();
        plantDatas.AddRange(fixedPlants);
    }

    public void LoadSelectedPlants()
    {
        plantDatas.Clear();

        // TODO:
        // Gọi logic load danh sách Plant mà người chơi đã chọn.
        // Ví dụ: lấy dữ liệu từ PlayerData hoặc hệ thống Selection hiện tại.
    }
}
