using UnityEngine;

public class PlantCellOccupant : MonoBehaviour
{
    private LawnCell cell;

    public void Initialize(LawnCell ownerCell)
    {
        cell = ownerCell;
    }

    private void OnDestroy()
    {
        if (cell != null)
            cell.ClearPlant(gameObject);
    }
}