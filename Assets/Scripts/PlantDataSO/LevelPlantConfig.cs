
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelPlantConfig",
    menuName = "PvZ/Level Plant Config"
)]
public class LevelPlantConfig : ScriptableObject
{
    [Header("Level Information")]
    public int level;

    [Header("Plant Selection")]
    public bool useFixedPlants;

    public List<PlantData> fixedPlants = new List<PlantData>();
}
