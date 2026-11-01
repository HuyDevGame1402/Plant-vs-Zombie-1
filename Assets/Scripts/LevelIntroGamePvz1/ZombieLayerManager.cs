using UnityEngine;

public class ZombieLayerManager : MonoBehaviour
{
    public static ZombieLayerManager Instance { get; private set; }

    public enum ZombieLane
    {
        Lane1 = 0, // Hàng trên cùng
        Lane2 = 1,
        Lane3 = 2,
        Lane4 = 3,
        Lane5 = 4  // Hàng dưới cùng
    }

    [Header("Cấu hình dải Layer")]
    [Tooltip("Khoảng cách Sorting Order giữa các hàng (Ví dụ: 1000)")]
    public int laneSpacing = 1000;

    [Tooltip("Sorting Order cơ bản cho bóng (Shadow) của mỗi hàng")]
    public int shadowOrderOffset = -10;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Tính toán Sorting Order tổng dựa trên Hàng, Thứ tự bộ phận trong danh sách và vị trí X tùy chọn.
    /// </summary>
    public int GetSortingOrder(ZombieLane lane, int partIndex, float worldX = 0f)
    {
        int baseLaneOrder = (int)lane * laneSpacing;

        // Tùy chỉnh nhỏ theo trục X: con nào đứng trước (X nhỏ hơn) sẽ có order nhỉnh hơn một chút để đè lên con đi sau
        int xOffset = Mathf.RoundToInt(-worldX * 2f);

        return baseLaneOrder + partIndex + xOffset;
    }

    /// <summary>
    /// Lấy sorting order riêng cho bóng của hàng đó
    /// </summary>
    public int GetShadowSortingOrder(ZombieLane lane)
    {
        return ((int)lane * laneSpacing) + shadowOrderOffset;
    }
}