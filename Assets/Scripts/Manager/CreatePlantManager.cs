using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreatePlantManager : MonoBehaviour
{
    public static CreatePlantManager Instance { get; private set; }

    [SerializeField] private GameObject plantPrefab;

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

    public void CreatePlant(Vector3 position)
    {
        if (plantPrefab != null)
        {
            Instantiate(plantPrefab, position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("Plant prefab is not assigned in CreatePlantManager.");
        }
    }
}
