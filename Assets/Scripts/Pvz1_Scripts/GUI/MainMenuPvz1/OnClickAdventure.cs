using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OnClickAdventure : PreciseImageButton
{
    [SerializeField] private GameObject imagePreciseAdventure;
    [SerializeField] private GameObject lightTexture;
    [SerializeField] private VideoControllerStartGame videoControllerStartGame;
    [SerializeField] private float timeDelay;

    protected override void OnButtonClicked()
    {
        Debug.Log("Đã click chính xác vào nút: ADVENTURE!");
        // Viết thêm logic mở màn chơi Adventure ở đây...

        if (imagePreciseAdventure == null || lightTexture == null) return;
        imagePreciseAdventure.SetActive(true);
        lightTexture.SetActive(false);
        transform.GetComponent<Image>().enabled = false;

        // chuyển sang video
        StartCoroutine(CoroutineDelayTime());
    }

    private IEnumerator CoroutineDelayTime()
    {
        yield return new WaitForSeconds(timeDelay);
        videoControllerStartGame.PlayVideoInitGame();
    }
}