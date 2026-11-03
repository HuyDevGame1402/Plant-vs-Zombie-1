using UnityEngine;
using System.Text.RegularExpressions;

public class LawnCell : MonoBehaviour
{
    [Min(0)] public int row;
    [Min(0)] public int column;

    [Tooltip("Vị trí cây sẽ xuất hiện. Nếu để trống, dùng vị trí ô.")]
    public Transform plantPoint;

    private Vector3 positionInCell;

    // Tự động chạy khi kéo script vào, đổi tên GameObject hoặc thay đổi giá trị trong Inspector
    private void OnValidate()
    {
        ParseCoordinatesFromName();
    }

    // Hoặc bạn có thể bấm chuột phải vào component này trên Inspector và chọn dòng này để ép chạy thủ công
    [ContextMenu("Tự động lấy Row/Column từ tên GameObject")]
    public void ParseCoordinatesFromName()
    {
        // Tìm tất cả các chuỗi số nguyên xuất hiện trong tên GameObject
        MatchCollection matches = Regex.Matches(gameObject.name, @"\d+");

        if (matches.Count >= 2)
        {
            if (int.TryParse(matches[0].Value, out int r) && int.TryParse(matches[1].Value, out int c))
            {
                row = r;
                column = c;
            }
        }
    }

    private void Awake()
    {
        plantPoint = transform.GetChild(0);
    }

    private void OnMouseDown()
    {
        positionInCell = plantPoint != null
            ? plantPoint.position
            : transform.position;

        if(CreatePlantManager.Instance != null)
        {
            CreatePlantManager.Instance.CreatePlant(positionInCell);
        }
        else
        {
            Debug.LogWarning("CreatePlantManager instance is not available.");
        }

        Debug.Log(
            $"Ô hàng {row + 1}, cột {column + 1} | " +
            $"Vị trí đặt cây: {positionInCell}"
        );
    }
}