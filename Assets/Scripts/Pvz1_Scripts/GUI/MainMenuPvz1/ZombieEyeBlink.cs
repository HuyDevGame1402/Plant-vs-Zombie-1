using UnityEngine;
using DG.Tweening;

public class ZombieEyeBlink : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo thả GameObject mắt (EyeZombieBG) vào đây")]
    public GameObject eyeZombie;

    [Header("Settings")]
    [Tooltip("Thời gian sáng/ẩn trong mỗi chu kỳ (giây)")]
    public float blinkInterval = 0.5f;

    private Tween blinkTween;

    void Start()
    {
        if (eyeZombie == null)
        {
            Debug.LogWarning("Chưa gán GameObject mắt vào script!", this);
            return;
        }

        StartBlinking();
    }

    void StartBlinking()
    {
        if (eyeZombie == null) return;

        // Sử dụng DOTween để lập lịch bật/tắt liên tục
        blinkTween = DOVirtual.DelayedCall(blinkInterval, () =>
        {
            if (eyeZombie != null)
            {
                // Đảo trạng thái hiển thị của GameObject mắt được kéo vào
                bool isActive = eyeZombie.activeSelf;
                eyeZombie.SetActive(!isActive);
            }

            // Tiếp tục vòng lặp nhấp nháy
            StartBlinking();
        }).SetUpdate(true);
    }

    void OnDestroy()
    {
        // Hủy tween khi đối tượng bị hủy để tránh rò rỉ bộ nhớ
        if (blinkTween != null && blinkTween.IsActive())
        {
            blinkTween.Kill();
        }
    }
}