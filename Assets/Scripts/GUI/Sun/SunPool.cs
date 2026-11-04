using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Object pool cho SunUI (dùng UnityEngine.Pool, cần Unity 2021.1+).
/// Dùng chung cho sun rơi từ trời và sau này sun của Sunflower.
/// </summary>
public class SunPool : MonoBehaviour
{
    [Tooltip("Prefab: RectTransform + SunReanimUI + SunUI (+ CanvasGroup).")]
    public SunUI prefab;

    [Tooltip("RectTransform chứa các sun. Nên là con của Canvas, stretch full màn hình, nằm trên các UI khác.")]
    public RectTransform container;

    [Tooltip("Bảng số lượng sun (Image UI). Khi click, sun sẽ bay tới đây rồi mới biến mất.")]
    public RectTransform collectTarget;

    public int prewarm = 5;
    public int maxSize = 30;

    ObjectPool<SunUI> _pool;

    void Awake()
    {
        _pool = new ObjectPool<SunUI>(
            createFunc: Create,
            actionOnGet: s => { s.gameObject.SetActive(true); s.transform.SetAsLastSibling(); },
            actionOnRelease: s => s.gameObject.SetActive(false),
            actionOnDestroy: s => { if (s != null) Destroy(s.gameObject); },
            collectionCheck: false,
            defaultCapacity: prewarm,
            maxSize: maxSize);

        // Tạo sẵn
        var tmp = new List<SunUI>(prewarm);
        for (int i = 0; i < prewarm; i++) tmp.Add(_pool.Get());
        foreach (var s in tmp) _pool.Release(s);
    }

    SunUI Create()
    {
        var s = Instantiate(prefab, container);
        s.SetPool(this);
        return s;
    }

    public SunUI Get() => _pool.Get();
    public void Release(SunUI sun) => _pool.Release(sun);
}