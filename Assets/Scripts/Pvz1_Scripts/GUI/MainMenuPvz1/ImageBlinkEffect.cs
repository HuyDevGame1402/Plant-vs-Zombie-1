using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ImageBlinkEffect : MonoBehaviour
{
    [Header("Cấu hình")]
    [Tooltip("Thời gian cho một chu kỳ mờ hoặc rõ (tính bằng giây)")]
    public float timeT = 1f;

    private Image targetImage;
    private Tween blinkTween;

    void Start()
    {
        // Lấy component Image trên cùng GameObject
        targetImage = GetComponent<Image>();

        if (targetImage != null)
        {
            StartBlink();
        }
        else
        {
            Debug.LogError("Không tìm thấy component Image trên GameObject này!");
        }
    }

    void StartBlink()
    {
        // Đảm bảo alpha bắt đầu từ max (1) hoặc giá trị hiện tại
        Color col = targetImage.color;
        col.a = 1f;
        targetImage.color = col;

        // Tạo hiệu ứng DOFade từ 1 về 0 trong khoảng thời gian timeT
        blinkTween = targetImage.DOFade(0f, timeT)
            .SetLoops(-1, LoopType.Yoyo) // LoopType.Yoyo giúp lật ngược tween: 1 -> 0 rồi tự động từ 0 -> 1
            .SetEase(Ease.InOutSine);     // Tạo độ uyển chuyển, mượt mà khi đổi chiều nhấp nháy
    }

    void OnDestroy()
    {
        // Hủy tween khi object bị hủy để tránh rò rỉ bộ nhớ (memory leak)
        if (blinkTween != null && blinkTween.IsActive())
        {
            blinkTween.Kill();
        }
    }
}