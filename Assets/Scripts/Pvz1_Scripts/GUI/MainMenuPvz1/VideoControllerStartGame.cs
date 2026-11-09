using UnityEngine;
using UnityEngine.Video;

public class VideoControllerStartGame : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;

    [SerializeField] private GameObject guiMain, cover;

    private void Start()
    {
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    public void PlayVideoInitGame()
    {
        guiMain.SetActive(false);
        cover.SetActive(false);
        videoPlayer.Play();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("Video đã phát xong!");

        // chuyển sang scene map chs
    }

    private void OnDestroy()
    {
        videoPlayer.loopPointReached -= OnVideoFinished;
    }
}