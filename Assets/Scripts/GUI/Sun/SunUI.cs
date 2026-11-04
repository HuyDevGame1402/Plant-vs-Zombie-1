using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Một mặt trời: rơi xuống, nằm chờ, bị click thì BAY TỚI BẢNG SỐ LƯỢNG SUN rồi mới ẩn đi (trả về pool).
/// Bảng sun được gán ở SunPool.collectTarget.
/// Click chính xác theo hình sun nhờ alpha hit test của các Image con (xem SunReanimUI.alphaHitThreshold).
/// </summary>
[RequireComponent(typeof(SunReanimUI))]
public class SunUI : MonoBehaviour, IPointerClickHandler
{
    /// <summary>Bắn ra khi sun BAY TỚI bảng, tham số = giá trị sun.</summary>
    public static event Action<int> OnCollected;

    [Header("Giá trị")]
    public int value = 25;

    [Header("Vòng đời trên đất")]
    [Tooltip("Thời gian nằm trên đất trước khi mờ dần rồi biến mất (PvZ gốc ~7.5s).")]
    public float groundLifetime = 7.5f;
    public float fadeOutDuration = 0.5f;

    [Header("Bay tới bảng sun")]
    [Tooltip("Thời gian bay tới bảng. Tăng lên để bay chậm hơn.")]
    public float flyDuration = 1.0f;
    public Ease flyEase = Ease.InOutSine;

    [Header("Sau khi tới bảng")]
    [Tooltip("Thời gian nhỏ lại + mờ đi SAU KHI đã bay tới bảng.")]
    public float arriveShrinkDuration = 0.25f;
    public Ease arriveShrinkEase = Ease.InBack;

    [Header("Fallback (không có bảng sun)")]
    public float collectDuration = 0.2f;
    public float collectScaleMul = 1.25f;

    RectTransform _rt;
    CanvasGroup _group;
    SunReanimUI _anim;
    SunPool _pool;
    Vector3 _baseScale;
    Sequence _seq;
    bool _collected;

    private SunValue sunValueScript;

    void Awake()
    {
        _rt = (RectTransform)transform;
        _anim = GetComponent<SunReanimUI>();
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _baseScale = _rt.localScale;

        sunValueScript = GetComponent<SunValue>();
    }

    public void SetPool(SunPool pool) { _pool = pool; }

    void ResetState()
    {
        _seq?.Kill();
        _seq = null;
        _collected = false;
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _rt.localScale = _baseScale;
        _anim.Restart();
    }

    /// <summary>
    /// Thả sun rơi thẳng từ startPos xuống landY (toạ độ local của container), tốc độ speed đơn vị/giây.
    /// </summary>
    public void FallFrom(Vector2 startPos, float landY, float speed, bool autoDisappear = true)
    {
        ResetState();
        _rt.localPosition = new Vector3(startPos.x, startPos.y, 0f);

        float dist = Mathf.Max(0f, startPos.y - landY);
        float duration = speed > 0f ? dist / speed : 0f;

        _seq = DOTween.Sequence();
        _seq.Append(_rt.DOLocalMoveY(landY, duration).SetEase(Ease.Linear));
        if (autoDisappear)
        {
            _seq.AppendInterval(groundLifetime);
            _seq.Append(_group.DOFade(0f, fadeOutDuration));
            _seq.OnComplete(Release);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Collect();
    }

    public void Collect()
    {
        if (_collected) return;
        _collected = true;

        _group.blocksRaycasts = false;   // không click lại được nữa
        _seq?.Kill();
        _rt.SetAsLastSibling();          // bay phía trên các sun khác

        RectTransform target = _pool != null ? _pool.collectTarget : null;
        if (target == null)
        {
            // Chưa gán bảng sun -> chỉ phóng nhẹ + mờ rồi ẩn
            _seq = DOTween.Sequence();
            _seq.Append(_rt.DOScale(_baseScale * collectScaleMul, collectDuration).SetEase(Ease.OutQuad));
            _seq.Join(_group.DOFade(0f, collectDuration));
            _seq.OnComplete(() => { OnArrivedAtCounter(); Release(); });
            return;
        }

        Vector3 startPos = _rt.localPosition;

        // Giai đoạn 1: bay tới bảng, GIỮ NGUYÊN kích thước và độ đậm.
        // Lerp tới vị trí HIỆN TẠI của bảng mỗi frame -> bảng có di chuyển/animation vẫn bay trúng.
        _seq = DOTween.Sequence();
        _seq.Append(DOVirtual.Float(0f, 1f, flyDuration, t =>
        {
            _rt.localPosition = Vector3.LerpUnclamped(startPos, GetTargetLocalPos(target), t);
        }).SetEase(flyEase));

        // Tới nơi: gọi hook xử lý (cộng sun, hiệu ứng bảng...)
        _seq.AppendCallback(OnArrivedAtCounter);

        // Giai đoạn 2: chỉ SAU KHI tới bảng mới nhỏ lại + mờ đi rồi ẩn.
        _seq.Append(_rt.DOScale(Vector3.zero, arriveShrinkDuration).SetEase(arriveShrinkEase));
        _seq.Join(_group.DOFade(0f, arriveShrinkDuration));
        _seq.OnComplete(Release);
    }

    /// <summary>Tâm của bảng sun, quy về toạ độ local của container chứa sun.</summary>
    Vector3 GetTargetLocalPos(RectTransform target)
    {
        Vector3 world = target.TransformPoint(target.rect.center);
        Vector3 local = _rt.parent.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

    /// <summary>Gọi khi sun đã bay tới bảng sun.</summary>
    void OnArrivedAtCounter()
    {
        OnCollected?.Invoke(value);

        // =====================================================================
        // >>> SUN ĐÃ BAY TỚI BẢNG SỐ LƯỢNG SUN <<<
        // TODO: xử lý việc khác ở đây, ví dụ:
        //   - cộng `value` vào tổng sun hiện tại và cập nhật text trên bảng
        //   - hiệu ứng bảng (punch scale, flash...), phát âm thanh nhặt sun
        // Cũng có thể không sửa file này mà đăng ký SunUI.OnCollected += (int v) => { ... };
        // (Sau hàm này sun sẽ tự nhỏ lại, mờ đi rồi trả về pool.)
        // =====================================================================

        if(SunMoneyManager.Instance != null && sunValueScript != null)
        {
            SunMoneyManager.Instance.AddSunMoney(sunValueScript.SunValueAmount);    
        }
    }

    void Release()
    {
        _seq?.Kill();
        _seq = null;
        if (_pool != null) _pool.Release(this);
        else gameObject.SetActive(false);
    }

    void OnDisable()
    {
        _seq?.Kill();
        _seq = null;
    }
}