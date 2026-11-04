using UnityEngine;
using DG.Tweening;
using System;

public class PlantCooldownUI : MonoBehaviour
{
    [Header("UI Cooldown")]
    [SerializeField] private RectTransform cooldownTransform;

    private Tween cooldownTween;

    public event Action OnCooldownCompleteEvent;

    /// <summary>
    /// Bắt đầu hiệu ứng cooldown.
    /// timeTicks: Thời gian hồi chiêu tính bằng game tick.
    /// </summary>
    public void StartCooldown(int timeTicks)
    {
        // Quy đổi game tick sang giây (100 tick = 1 giây)
        float duration = timeTicks / 100f;

        // Dừng tween cũ nếu đang chạy
        cooldownTween?.Kill();

        // Đưa scale Y về 1 trước khi bắt đầu
        Vector3 scale = cooldownTransform.localScale;
        scale.y = 1f;
        cooldownTransform.localScale = scale;

        // Giảm scale Y từ 1 về 0
        cooldownTween = cooldownTransform
            .DOScaleY(0f, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // TODO: Xử lý khi cooldown hoàn tất tại đây.

                OnCooldownCompleteEvent?.Invoke();

            });
    }

    private void OnDestroy()
    {
        cooldownTween?.Kill();
    }
}