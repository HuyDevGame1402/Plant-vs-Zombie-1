using UnityEngine;

/// <summary>
/// Dữ liệu một loại cây PvZ. Các giá trị thời gian trong bộ dữ liệu gốc tính bằng game tick,
/// không phải mili giây.
/// </summary>
[CreateAssetMenu(fileName = "NewPlant", menuName = "Plants/Plant Data")]
public class PlantData : ScriptableObject
{
    [Header("Thông tin cơ bản")]
    [Tooltip("ID kỹ thuật trong dữ liệu, ví dụ PEASHOOTER.")]
    public string id;
    [Tooltip("Tên hiển thị trong game; có thể liên kết với hệ thống localization.")]
    public string displayName;
    [TextArea(3, 6)]
    [Tooltip("Mô tả do bạn viết cho giao diện hoặc gameplay.")]
    public string description;
    public string seedEnum;
    public string nameId;
    public string reanimationType;
    public string subclass;
    public int packetIndex;

    [Header("Giá và thời gian")]
    [Min(0)] public int seedCostSun;
    [Min(0)] public int seedPacketRefreshTicks;
    [Min(0)] public int launchRateTicks;
    [Tooltip("Giải thích launchRateTicks, ví dụ shoot cycle hoặc production cycle.")]
    public string launchRateMeaning;

    [Header("Máu")]
    [Min(0)] public int initialHp = 300;
    [Min(0)] public int maxHpAtPlanting = 300;
    public bool hasConditionalHealthMultiplier;
    public string conditionalHealthMode;
    [Min(0f)] public float conditionalHealthMultiplier = 1f;

    [Header("Tấn công")]
    public string projectileType;
    public bool hasDamagePerHit;
    [Min(0)] public int damagePerHit;
    public bool hasProjectilesPerSequence;
    [Min(0)] public int projectilesPerSequence;
    public bool hasAttackInterval;
    [Min(0)] public int nominalIntervalTicks;
    [Min(0)] public int intervalJitterTicks;
    [TextArea(2, 5)]
    [Tooltip("Ghi chú cơ chế tấn công từ dữ liệu nguồn; có thể bao gồm sát thương đặc biệt hoặc hiệu ứng.")]
    public string attackNotes;

    [Header("Sản xuất (nếu có)")]
    public bool producesResource;
    public string productionType;
    [Min(0)] public int amountPerCycle;
    public string coinKind;
    public string productionInitialState;

    [Header("Thông tin bổ sung")]
    [TextArea(2, 5)]
    public string extraNotes;
    public Sprite imageSelectGUI;
    public GameObject plantPrefab;

    // bo sung offset khi trong cay cho chuan
    public Vector2 offsetCreatePlant;
}
