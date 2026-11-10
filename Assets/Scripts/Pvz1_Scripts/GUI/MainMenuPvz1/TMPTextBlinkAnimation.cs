
using System.Collections;
using TMPro;
using UnityEngine;

public class TMPTextBlinkAnimation : MonoBehaviour
{
    [Header("Text Reference")]
    public TextMeshProUGUI targetText;

    [Header("Blink Settings")]
    public Color originalColor = new Color(
        243f / 255f,
        225f / 255f,
        169f / 255f,
        1f
    );

    public Color highlightColor = Color.white;

    [Tooltip("Thời gian chuyển từ màu gốc sang màu sáng")]
    public float brightenDuration = 0.8f;

    [Tooltip("Thời gian chuyển từ màu sáng về màu gốc")]
    public float returnDuration = 0.8f;

    private Coroutine blinkCoroutine;

    void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (targetText == null)
            targetText = GetComponent<TextMeshProUGUI>();

        if (targetText == null) return;

        targetText.color = originalColor;

        blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (targetText != null)
            targetText.color = originalColor;
    }

    IEnumerator BlinkCoroutine()
    {
        while (true)
        {
            // Màu gốc -> Màu sáng
            yield return LerpColor(
                originalColor,
                highlightColor,
                brightenDuration
            );

            // Màu sáng -> Màu gốc
            yield return LerpColor(
                highlightColor,
                originalColor,
                returnDuration
            );
        }
    }

    IEnumerator LerpColor(Color from, Color to, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            targetText.color = Color.Lerp(from, to, t);

            yield return null;
        }

        targetText.color = to;
    }
}
