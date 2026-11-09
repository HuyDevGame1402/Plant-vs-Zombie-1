using UnityEngine;
using System.Text.RegularExpressions;

public class LawnCell : MonoBehaviour
{
    [Min(0)] public int row;
    [Min(0)] public int column;

    [Tooltip("Vị trí cây sẽ xuất hiện. Nếu để trống, dùng vị trí ô.")]
    public Transform plantPoint;

    public GameObject Plant { get; private set; }

    private void OnValidate()
    {
        ParseCoordinatesFromName();
    }

    [ContextMenu("Tự động lấy Row/Column từ tên GameObject")]
    public void ParseCoordinatesFromName()
    {
        MatchCollection matches = Regex.Matches(gameObject.name, @"\d+");

        if (matches.Count >= 2 &&
            int.TryParse(matches[0].Value, out int r) &&
            int.TryParse(matches[1].Value, out int c))
        {
            row = r;
            column = c;
        }
    }

    private void Awake()
    {
        if (plantPoint == null && transform.childCount > 0)
            plantPoint = transform.GetChild(0);
    }

    public Vector3 PlantPosition =>
        plantPoint != null ? plantPoint.position : transform.position;

    public bool IsOccupied => Plant != null;

    public void SetPlant(GameObject plant)
    {
        Plant = plant;
    }

    public void ClearPlant(GameObject plant)
    {
        // Không xóa tham chiếu nếu ô đã được gán một cây khác.
        if (Plant == plant)
            Plant = null;
    }

    private void OnMouseDown()
    {
        if (CreatePlantManager.Instance != null)
            CreatePlantManager.Instance.CreatePlant(this);
    }
}