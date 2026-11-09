using UnityEngine;
using System;

public class CreatePlantManager : MonoBehaviour
{
    public static CreatePlantManager Instance { get; private set; }

    private const int GridSize = 6;
    private readonly LawnCell[,] cells = new LawnCell[GridSize, GridSize];

    private PlantData plantDataSelected;
    private ButtonSelectPlant buttonSelectPlant;

    public event Action OnCreatedPlantEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        RegisterCells(FindObjectsByType<LawnCell>(FindObjectsSortMode.None));
    }

    public void RegisterCells(LawnCell[] lawnCells)
    {
        Array.Clear(cells, 0, cells.Length);

        foreach (LawnCell cell in lawnCells)
        {
            if (cell == null)
                continue;

            if (!IsValidCoordinate(cell.row, cell.column))
            {
                Debug.LogWarning(
                    $"Cell '{cell.name}' có tọa độ ({cell.row}, {cell.column}) ngoài ma trận 6x6.",
                    cell);
                continue;
            }

            if (cells[cell.row, cell.column] != null)
            {
                Debug.LogWarning(
                    $"Có nhiều cell cùng tọa độ ({cell.row}, {cell.column}).",
                    cell);
                continue;
            }

            cells[cell.row, cell.column] = cell;
        }
    }

    public LawnCell GetCell(int row, int column)
    {
        if (!IsValidCoordinate(row, column))
            return null;

        return cells[row, column];
    }

    public GameObject GetPlant(int row, int column)
    {
        return GetCell(row, column)?.Plant;
    }

    public void SetupPlantSelectSpawn(
        ButtonSelectPlant buttonSelectPlant,
        PlantData plantData)
    {
        this.plantDataSelected = plantData;
        this.buttonSelectPlant = buttonSelectPlant;
    }

    public bool CreatePlant(LawnCell cell)
    {
        if (cell == null || plantDataSelected == null)
            return false;

        if (!IsValidCoordinate(cell.row, cell.column) ||
            cells[cell.row, cell.column] != cell)
        {
            Debug.LogWarning($"Cell '{cell.name}' chưa được đăng ký đúng với Manager.", cell);
            return false;
        }

        if (cell.IsOccupied)
            return false;

        if (plantDataSelected.plantPrefab == null)
        {
            Debug.LogWarning("Plant prefab chưa được gán trong PlantData.");
            return false;
        }

        GameObject plant = Instantiate(
            plantDataSelected.plantPrefab,
            cell.PlantPosition,
            Quaternion.identity);

        cell.SetPlant(plant);

        // Nếu cây bị xóa khi chơi, cập nhật lại trạng thái ô.
        PlantCellOccupant occupant = plant.GetComponent<PlantCellOccupant>();
        if (occupant == null)
            occupant = plant.AddComponent<PlantCellOccupant>();

        occupant.Initialize(cell);

        buttonSelectPlant?.ActiveSelectGUIPlant(false);

        if (SunMoneyManager.Instance != null)
            SunMoneyManager.Instance.SpendSunMoney(plantDataSelected.seedCostSun);

        if (buttonSelectPlant != null)
        {
            buttonSelectPlant.PlantCooldownUI.StartCooldown(
                plantDataSelected.seedPacketRefreshTicks);
            buttonSelectPlant.DisablePlantClick();
        }

        plantDataSelected = null;
        buttonSelectPlant = null;

        OnCreatedPlantEvent?.Invoke();
        return true;
    }

    private bool IsValidCoordinate(int row, int column)
    {
        return row >= 0 && row < GridSize &&
               column >= 0 && column < GridSize;
    }
}