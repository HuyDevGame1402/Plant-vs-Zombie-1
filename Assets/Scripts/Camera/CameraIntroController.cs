using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class CameraIntroController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Vị trí X mà camera sẽ di chuyển đến")]
    [SerializeField] private float targetX = 10f;

    [Tooltip("Thời gian di chuyển camera")]
    [SerializeField] private float moveDuration = 3f;

    [Tooltip("Thời gian chờ tại vị trí đích")]
    [SerializeField] private float waitTime = 1.5f;

    [Header("Player Name UI")]
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Tooltip("Bắt đầu ẩn tên người chơi khi camera đi được bao nhiêu % quãng đường")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartProgress = 0.5f;

    [Tooltip("Thời gian fade out tên người chơi")]
    [SerializeField] private float fadeDuration = 0.5f;

    private Vector3 startPosition = new Vector3(0f, 0f, -10f);

    // Event được gọi khi camera bắt đầu quay về vị trí cũ.
    public event Action OnCameraReturn;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            cameraTransform.position = startPosition;

        // StartCoroutine(PlayCameraIntro());
    }

    public IEnumerator PlayCameraIntro()
    {
        if (cameraTransform == null)
        {
            Debug.LogError("CameraIntroController: Camera Transform is missing!", this);
            yield break;
        }

        // Di chuyển camera sang phải.
        yield return StartCoroutine(MoveCamera(
            startPosition,
            new Vector3(targetX, 0f, -10f),
            true
        ));

        // Chờ tại vị trí đích.
        yield return new WaitForSeconds(waitTime);

        // Quay camera về vị trí ban đầu.
        yield return StartCoroutine(MoveCamera(
            new Vector3(targetX, 0f, -10f),
            startPosition,
            false
        ));
        // Invoke event khi camera chuẩn bị quay về.
        OnCameraReturn?.Invoke();
    }

    private IEnumerator MoveCamera(
        Vector3 from,
        Vector3 to,
        bool fadeName
    )
    {
        float elapsed = 0f;
        bool fadeStarted = false;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / moveDuration);

            // Di chuyển mượt.
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            cameraTransform.position = Vector3.Lerp(
                from,
                to,
                smoothProgress
            );

            // Bắt đầu fade tên người chơi khi vượt ngưỡng.
            if (fadeName && !fadeStarted && progress >= fadeStartProgress)
            {
                fadeStarted = true;

                if (playerNameText != null)
                    StartCoroutine(FadeOutPlayerName());
            }

            yield return null;
        }

        cameraTransform.position = to;
    }

    private IEnumerator FadeOutPlayerName()
    {
        if (playerNameText == null)
            yield break;

        Color color = playerNameText.color;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float alpha = Mathf.Lerp(
                1f,
                0f,
                Mathf.Clamp01(elapsed / fadeDuration)
            );

            color.a = alpha;
            playerNameText.color = color;

            yield return null;
        }

        color.a = 0f;
        playerNameText.color = color;
    }
}
