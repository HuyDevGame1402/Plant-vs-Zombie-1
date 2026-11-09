using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class SceneTransitionInitGamePvz1 : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Image image;

    [Header("Settings")]
    [SerializeField] private float displayTime = 3f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private string sceneName;

    private void Start()
    {
        StartCoroutine(TransitionRoutine());
    }

    private System.Collections.IEnumerator TransitionRoutine()
    {
        // Thời gian hiển thị ban đầu
        yield return new WaitForSeconds(displayTime);

        // Fade đồng thời Text và Image
        Sequence sequence = DOTween.Sequence();

        if (text != null)
        {
            sequence.Join(
                text.DOFade(0f, fadeDuration)
            );
        }

        if (image != null)
        {
            sequence.Join(
                image.DOFade(0f, fadeDuration)
            );
        }

        // Chờ hiệu ứng hoàn tất
        yield return sequence.WaitForCompletion();

        // Chuyển Scene
        SceneManager.LoadScene(sceneName);
    }
}
