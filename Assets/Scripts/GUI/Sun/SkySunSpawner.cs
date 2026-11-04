using UnityEngine;

/// <summary>
/// Sinh mặt trời rơi từ trời theo logic Board::UpdateSunSpawning của PvZ1.
///  - Lần đầu: RandRange(425, 700) tick.
///  - Các lần sau: min(950, 425 + N*10) + Rand(275) tick, N = số sun đã rơi (sau khi ++).
///  - 1 tick = 10ms (100 tick/giây).
/// Vị trí:
///  - X: từ cạnh trái của spawnArea đến mép phải màn hình (trừ rightOffset).
///  - Y xuất phát: phía trên mép trên màn hình (ngoài tầm nhìn).
///  - Y tiếp đất: từ mép trên đến mép dưới của spawnArea (cộng bottomOffset để không sát đáy).
/// </summary>
public class SkySunSpawner : MonoBehaviour
{
    // Hằng số lấy từ Board.cpp
    const int SUN_COUNTDOWN = 425;
    const int SUN_COUNTDOWN_RANGE = 275;
    const int SUN_COUNTDOWN_MAX = 950;
    const int FIRST_MIN = 425;
    const int FIRST_MAX = 700;

    [Header("Tham chiếu")]
    public SunPool pool;
    [Tooltip("RectTransform xác định vùng sun được phép rơi xuống (góc trên-trái của vùng chơi).")]
    public RectTransform spawnArea;
    [Tooltip("Để trống = tự lấy root Canvas. Dùng để biết mép phải / mép trên của màn hình.")]
    public RectTransform screenRect;

    [Header("Offset (đơn vị canvas)")]
    public int rightOffset = 60;
    public int bottomOffset = 60;
    [Tooltip("Khoảng cách phía TRÊN mép màn hình để sun bắt đầu rơi (phải >= nửa kích thước sun).")]
    public int topMargin = 150;

    [Header("Rơi")]
    [Tooltip("Tốc độ rơi (đơn vị canvas / giây).")]
    public float fallSpeed = 80f;

    [Header("Điều khiển")]
    public bool autoStart = true;
    [Tooltip("Màn ban đêm: không có sun rơi từ trời.")]
    public bool isNight = false;
    public float ticksPerSecond = 100f;

    int _numSunsFallen;
    float _countdown;
    bool _running;
    readonly Vector3[] _corners = new Vector3[4];

    public int NumSunsFallen => _numSunsFallen;

    void Start()
    {
        if (screenRect == null)
        {
            var canvas = pool.container.GetComponentInParent<Canvas>();
            if (canvas != null) screenRect = (RectTransform)canvas.rootCanvas.transform;
        }
        if (autoStart) StartSpawning();
    }

    public void StartSpawning()
    {
        _numSunsFallen = 0;
        _countdown = Random.Range(FIRST_MIN, FIRST_MAX + 1); // RandRangeInt(425, 700)
        _running = true;
    }

    public void StopSpawning() { _running = false; }

    void Update()
    {
        if (!_running || isNight) return;

        _countdown -= Time.deltaTime * ticksPerSecond;
        if (_countdown > 0f) return;

        SpawnSkySun();

        _numSunsFallen++;
        _countdown = Mathf.Min(SUN_COUNTDOWN_MAX, SUN_COUNTDOWN + _numSunsFallen * 10)
                     + Random.Range(0, SUN_COUNTDOWN_RANGE); // Rand(275) = 0..274
    }

    public SunUI SpawnSkySun()
    {
        var space = pool.container;
        Rect area = GetRectIn(spawnArea, space);
        Rect screen = GetRectIn(screenRect, space);

        float xMin = area.xMin;
        float xMax = Mathf.Max(xMin, screen.xMax - rightOffset);

        float landTop = area.yMax;
        float landBottom = Mathf.Min(landTop, area.yMin + bottomOffset);

        float x = Random.Range(xMin, xMax);
        float landY = Random.Range(landBottom, landTop);
        float startY = screen.yMax + topMargin;

        var sun = pool.Get();
        sun.FallFrom(new Vector2(x, startY), landY, fallSpeed);
        return sun;
    }

    /// <summary>Hình chữ nhật bao của target, biểu diễn trong toạ độ local của space.</summary>
    Rect GetRectIn(RectTransform target, RectTransform space)
    {
        target.GetWorldCorners(_corners);
        Vector2 min = space.InverseTransformPoint(_corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 p = space.InverseTransformPoint(_corners[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}