using UnityEngine;

/// <summary>
/// Làm sun GUI bay vòng cung từ đầu cây ra vị trí rơi. Tự hủy sau khi xong.
/// </summary>
public class SunPopArc : MonoBehaviour
{
    private RectTransform rect;
    private Vector3 from;
    private Vector3 to;
    private float height;
    private float duration;
    private float time;
    private bool running;

    public void Begin(Vector3 fromLocal, Vector3 toLocal, float arcHeight, float arcDuration)
    {
        rect = transform as RectTransform;
        from = fromLocal;
        to = toLocal;
        height = arcHeight;
        duration = Mathf.Max(0.05f, arcDuration);
        time = 0f;
        running = rect != null;

        if (running)
            rect.localPosition = from;
    }

    private void Update()
    {
        if (!running)
            return;

        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / duration);

        Vector3 pos = Vector3.Lerp(from, to, t);
        pos.y += 4f * height * t * (1f - t); // parabol

        rect.localPosition = pos;

        if (t >= 1f)
        {
            running = false;
            Destroy(this);
        }
    }
}