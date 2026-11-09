using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class LeafFallEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform container;
    [SerializeField] private Sprite leafSprite;

    [Header("Object Pool")]
    [SerializeField] private int maxLeaves = 2;

    [Header("Spawn Settings")]
    [SerializeField] private float minSpawnDelay = 1.5f;
    [SerializeField] private float maxSpawnDelay = 3.5f;
    [SerializeField] private float minHorizontalSpacing = 180f;

    [Header("Leaf Size")]
    [SerializeField] private Vector2 leafSize = new Vector2(45f, 45f);
    [SerializeField] private float minScale = 0.65f;
    [SerializeField] private float maxScale = 1.35f;

    [Header("Fall Speed")]
    [SerializeField] private float minFallDuration = 6f;
    [SerializeField] private float maxFallDuration = 10f;

    [Header("Diagonal Wind (góc rơi so với phương thẳng đứng)")]
    [SerializeField] private float minFallAngle = 30f;
    [SerializeField] private float maxFallAngle = 45f;

    [Header("Sway")]
    [SerializeField] private float minSwayAmplitude = 12f;
    [SerializeField] private float maxSwayAmplitude = 40f;
    [SerializeField] private float minSwayCycles = 1f;
    [SerializeField] private float maxSwayCycles = 2.5f;

    [Header("Wind Speed Variation")]
    [SerializeField, Range(0f, 0.4f)]
    private float windSpeedVariation = 0.15f;

    [Header("Rotation")]
    [SerializeField] private float minRotation = 120f;
    [SerializeField] private float maxRotation = 360f;

    private readonly List<Image> leafPool = new List<Image>();
    private readonly List<Tween> activeTweens = new List<Tween>();
    private Coroutine spawnCoroutine;
    private int activeLeaves = 0;
    private bool isRunning = false;

    private void Awake()
    {
        CreatePool();
    }

    private void OnEnable()
    {
        isRunning = true;
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        isRunning = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        KillAllTweens();

        foreach (Image leaf in leafPool)
            leaf.gameObject.SetActive(false);

        activeLeaves = 0;
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void KillAllTweens()
    {
        foreach (Tween tween in activeTweens)
        {
            if (tween != null && tween.IsActive())
                tween.Kill();
        }
        activeTweens.Clear();
    }

    // ================= OBJECT POOL =================

    private void CreatePool()
    {
        if (container == null || leafSprite == null)
        {
            Debug.LogError("LeafFallEffect: Missing Container or Leaf Sprite!");
            return;
        }

        for (int i = 0; i < maxLeaves; i++)
        {
            GameObject obj = new GameObject(
                "PooledLeaf_" + i,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            obj.transform.SetParent(container, false);

            Image image = obj.GetComponent<Image>();
            image.sprite = leafSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = leafSize;

            obj.SetActive(false);
            leafPool.Add(image);
        }
    }

    private Image GetAvailableLeaf()
    {
        foreach (Image leaf in leafPool)
        {
            if (!leaf.gameObject.activeSelf)
                return leaf;
        }
        return null;
    }

    // ================= SPAWN SYSTEM =================

    private IEnumerator SpawnRoutine()
    {
        // FIX LỖI 1: chờ Canvas/Layout tính xong kích thước container
        yield return null;
        Canvas.ForceUpdateCanvases();

        while (container == null || container.rect.height <= 1f || container.rect.width <= 1f)
            yield return null;

        while (isRunning)
        {
            if (activeLeaves < maxLeaves)
                SpawnLeaf();

            yield return new WaitForSeconds(Random.Range(minSpawnDelay, maxSpawnDelay));
        }
    }

    private void SpawnLeaf()
    {
        Image leaf = GetAvailableLeaf();

        if (leaf == null || container == null)
            return;

        float width = container.rect.width;
        float height = container.rect.height;

        // Phòng trường hợp kích thước chưa hợp lệ
        if (height <= 1f || width <= 1f)
            return;

        activeLeaves++;

        RectTransform rect = leaf.rectTransform;

        float scale = Random.Range(minScale, maxScale);
        float actualHalfHeight = leafSize.y * scale * 0.5f;

        float startY = height * 0.5f + actualHalfHeight + 10f;
        float endY = -height * 0.5f - actualHalfHeight - 20f;
        float totalFall = startY - endY;

        // ===== FIX LỖI 2: lá bay chéo theo góc 30–45° =====
        float windDirection = Random.value > 0.5f ? 1f : -1f;
        float angle = Random.Range(minFallAngle, maxFallAngle);
        float totalDrift = Mathf.Tan(angle * Mathf.Deg2Rad) * totalFall; // quãng đường lệch ngang

        // Dịch vùng spawn ngược chiều gió để lá vẫn đi ngang qua màn hình
        float minX = -width * 0.5f - windDirection * totalDrift * 0.5f;
        float maxX = width * 0.5f - windDirection * totalDrift * 0.5f;

        float startX = Random.Range(minX, maxX);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float candidateX = Random.Range(minX, maxX);
            bool valid = true;

            foreach (Image other in leafPool)
            {
                if (other == leaf || !other.gameObject.activeSelf)
                    continue;

                float otherX = other.rectTransform.anchoredPosition.x;

                if (Mathf.Abs(candidateX - otherX) < minHorizontalSpacing)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                startX = candidateX;
                break;
            }
        }

        float duration = Random.Range(minFallDuration, maxFallDuration);
        float swayAmplitude = Random.Range(minSwayAmplitude, maxSwayAmplitude);
        float swayCycles = Random.Range(minSwayCycles, maxSwayCycles);
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);

        float rotation = Random.Range(minRotation, maxRotation);
        if (Random.value > 0.5f)
            rotation *= -1f;

        float startRotation = Random.Range(0f, 360f);

        leaf.sprite = leafSprite;
        leaf.color = Color.white;
        leaf.enabled = true;

        rect.sizeDelta = leafSize;
        rect.anchoredPosition = new Vector2(startX, startY);
        rect.localRotation = Quaternion.Euler(0f, 0f, startRotation);
        rect.localScale = Vector3.one * scale;

        leaf.gameObject.SetActive(true);
        leaf.transform.SetAsLastSibling();

        float progress = 0f;
        bool returnedToPool = false;

        float initialSway = Mathf.Sin(swayPhase) * swayAmplitude;

        Tween fallTween = DOTween.To(
            () => progress,
            value =>
            {
                progress = value;

                float verticalProgress =
                    value +
                    windSpeedVariation / (2f * Mathf.PI * swayCycles) *
                    (1f - Mathf.Cos(2f * Mathf.PI * swayCycles * value));

                verticalProgress = Mathf.Clamp01(verticalProgress);

                float y = Mathf.Lerp(startY, endY, verticalProgress);

                // Lệch ngang theo góc chéo
                float windX = windDirection * totalDrift * verticalProgress;

                float swayX = Mathf.Sin(value * swayCycles * Mathf.PI * 2f + swayPhase) * swayAmplitude;

                float x = startX + windX + swayX - initialSway;

                rect.anchoredPosition = new Vector2(x, y);
            },
            1f,
            duration
        ).SetEase(Ease.Linear);

        Tween rotateTween = rect.DOLocalRotate(
            new Vector3(0f, 0f, startRotation + rotation),
            duration,
            RotateMode.FastBeyond360
        ).SetEase(Ease.Linear);

        Sequence sequence = DOTween.Sequence();
        sequence.Join(fallTween);
        sequence.Join(rotateTween);

        activeTweens.Add(sequence);

        sequence.OnComplete(() =>
        {
            if (returnedToPool)
                return;

            returnedToPool = true;
            leaf.gameObject.SetActive(false);
            activeLeaves--;
            activeTweens.Remove(sequence);
        });
    }
}