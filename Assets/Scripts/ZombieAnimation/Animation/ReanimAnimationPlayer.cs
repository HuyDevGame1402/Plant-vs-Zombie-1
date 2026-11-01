
using System;
using UnityEngine;

/// <summary>
/// Điều khiển việc phát animation từ một ReanimData.
/// Không phụ thuộc vào ZombieState, Renderer hoặc gameplay.
/// </summary>
public class ReanimAnimationPlayer
{
    private ReanimData data;

    private ReanimData.ReanimClip currentClip;

    private float clipTime;
    private float blendTimer;

    private bool clipLoop = true;

    /// <summary>
    /// Tốc độ phát animation.
    /// 1 = tốc độ bình thường.
    /// 2 = nhanh gấp đôi.
    /// 0.5 = chậm một nửa.
    /// </summary>
    public float AnimationSpeed { get; set; } = 1f;

    /// <summary>
    /// Thời gian chuyển mượt giữa hai clip.
    /// </summary>
    public float BlendDuration { get; set; } = 0.12f;

    /// <summary>
    /// Dữ liệu Reanim mà Player đang sử dụng.
    /// </summary>
    public ReanimData Data
    {
        get { return data; }
    }

    /// <summary>
    /// Clip hiện tại.
    /// </summary>
    public ReanimData.ReanimClip CurrentClip
    {
        get { return currentClip; }
    }

    /// <summary>
    /// Tên clip hiện tại.
    /// </summary>
    public string CurrentClipName
    {
        get { return currentClip != null ? currentClip.name : null; }
    }

    /// <summary>
    /// Thời gian đã chạy của clip, tính bằng giây.
    /// </summary>
    public float ClipTime
    {
        get { return clipTime; }
    }

    /// <summary>
    /// Clip hiện tại có chạy lặp hay không.
    /// </summary>
    public bool IsLooping
    {
        get { return clipLoop; }
    }

    /// <summary>
    /// Đã chạy hết clip chưa.
    /// Chỉ có ý nghĩa với clip không lặp.
    /// </summary>
    public bool IsFinished
    {
        get
        {
            if (currentClip == null || clipLoop)
                return false;

            return clipTime * data.Fps >= currentClip.Length;
        }
    }

    /// <summary>
    /// Có đang trong quá trình blend hay không.
    /// </summary>
    public bool IsBlending
    {
        get { return blendTimer > 0f; }
    }

    /// <summary>
    /// Trọng số blend từ 0 đến 1.
    /// 0 = bắt đầu chuyển.
    /// 1 = hoàn thành chuyển.
    /// </summary>
    public float BlendWeight
    {
        get
        {
            if (BlendDuration <= 0f)
                return 1f;

            return 1f - Mathf.Clamp01(
                blendTimer / BlendDuration);
        }
    }

    /// <summary>
    /// Frame đầu của khoảng animation hiện tại.
    /// </summary>
    public int FrameA { get; private set; }

    /// <summary>
    /// Frame tiếp theo dùng để nội suy.
    /// </summary>
    public int FrameB { get; private set; }

    /// <summary>
    /// Tỷ lệ nội suy giữa FrameA và FrameB.
    /// </summary>
    public float FrameLerp { get; private set; }

    /// <summary>
    /// Được gọi khi bắt đầu chuyển sang clip khác.
    /// Renderer có thể sử dụng event này để lưu pose cũ.
    /// </summary>
    public event Action OnBlendStarted;

    // ==========================================================
    // Khởi tạo
    // ==========================================================

    public bool Initialize(ReanimData reanimData)
    {
        if (reanimData == null || !reanimData.IsValid)
        {
            Debug.LogError(
                "ReanimAnimationPlayer: ReanimData không hợp lệ.");

            return false;
        }

        data = reanimData;

        currentClip = null;
        clipTime = 0f;
        blendTimer = 0f;
        clipLoop = true;

        FrameA = 0;
        FrameB = 0;
        FrameLerp = 0f;

        return true;
    }

    // ==========================================================
    // Phát animation
    // ==========================================================

    /// <summary>
    /// Phát clip theo tên.
    /// loop = true: phát lặp.
    /// loop = false: phát một lần.
    /// </summary>
    public bool Play(string clipName, bool loop = true)
    {
        if (data == null)
        {
            Debug.LogError(
                "ReanimAnimationPlayer: Chưa Initialize.");

            return false;
        }

        if (string.IsNullOrEmpty(clipName))
        {
            Debug.LogWarning(
                "ReanimAnimationPlayer: Tên clip không hợp lệ.");

            return false;
        }

        ReanimData.ReanimClip clip;

        if (!data.Clips.TryGetValue(clipName, out clip))
        {
            Debug.LogWarning(
                "ReanimAnimationPlayer: Không tìm thấy clip '"
                + clipName + "'.");

            return false;
        }

        // Nếu đang phát đúng clip và cùng chế độ loop,
        // không reset thời gian animation.
        if (currentClip == clip && clipLoop == loop)
            return true;

        // Báo cho Renderer biết sắp chuyển animation
        // để Renderer có thể lưu lại pose hiện tại.
        if (currentClip != null && BlendDuration > 0f)
        {
            blendTimer = BlendDuration;

            if (OnBlendStarted != null)
                OnBlendStarted();
        }
        else
        {
            blendTimer = 0f;
        }

        currentClip = clip;
        clipLoop = loop;
        clipTime = 0f;

        UpdateFrameRange();

        return true;
    }

    // ==========================================================
    // Cập nhật animation
    // ==========================================================

    /// <summary>
    /// Cập nhật thời gian animation.
    /// Được gọi từ Update của class bên ngoài.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (data == null || currentClip == null)
            return;

        if (deltaTime < 0f)
            return;

        clipTime += deltaTime * Mathf.Max(0f, AnimationSpeed);

        if (blendTimer > 0f)
        {
            blendTimer = Mathf.Max(
                0f,
                blendTimer - deltaTime);
        }

        UpdateFrameRange();
    }

    // ==========================================================
    // Tính frame
    // ==========================================================

    private void UpdateFrameRange()
    {
        if (data == null || currentClip == null)
            return;

        float elapsedFrames = clipTime * data.Fps;

        float position;

        if (clipLoop)
        {
            position = currentClip.start
                + Mathf.Repeat(
                    elapsedFrames,
                    Mathf.Max(1, currentClip.Length));
        }
        else
        {
            position = currentClip.start
                + Mathf.Min(
                    elapsedFrames,
                    currentClip.Length);
        }

        FrameA = Mathf.Clamp(
            Mathf.FloorToInt(position),
            currentClip.start,
            currentClip.end);

        FrameB = Mathf.Min(
            FrameA + 1,
            currentClip.end);

        FrameLerp = FrameA == FrameB
            ? 0f
            : position - FrameA;
    }

    // ==========================================================
    // Dừng và reset
    // ==========================================================

    /// <summary>
    /// Dừng Player và bỏ clip hiện tại.
    /// </summary>
    public void Stop()
    {
        currentClip = null;
        clipTime = 0f;
        blendTimer = 0f;

        FrameA = 0;
        FrameB = 0;
        FrameLerp = 0f;
    }

    /// <summary>
    /// Đưa thời gian clip hiện tại về đầu.
    /// Không thay đổi clip và chế độ loop.
    /// </summary>
    public void Restart()
    {
        if (currentClip == null)
            return;

        clipTime = 0f;
        UpdateFrameRange();
    }
}
