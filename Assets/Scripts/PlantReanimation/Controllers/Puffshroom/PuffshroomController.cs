using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Puffshroom
{
    public class PuffshroomController : PlantReanimController
    {
        private const string SleepClipName = "sleep";

        [Header("Tên marker trong Reanim JSON")]
        public string idleMarker = "anim_idle";
        public string shootMarker = "anim_shooting";
        public string sleepMarker = "anim_sleep";

        [Header("Tên clip")]
        public string idleClipName = "idle";
        public string shootClipName = "shooting";
        public string sleepClipName = "sleep";

        [Header("Tốc độ animation")]
        [Min(0f)] public float animationSpeed = 1f;
        [Min(0f)] public float shootSpeed = 1f;

        [Header("Sự kiện bắn")]
        [Tooltip("Frame tính từ đầu clip shooting. Chỉnh giá trị này nếu đạn xuất hiện lệch nhịp.")]
        [Min(0)] public int fireFrame = 5;
        public bool logFireEvent;

        [Header("Test trong Inspector")]
        [Tooltip("Bật để tự bắn theo chu kỳ trong Play Mode.")]
        public bool autoShootTest;
        [Min(0.1f)] public float autoShootInterval = 2f;
        [Tooltip("Bật trong Play Mode để bắn một lần; script tự tắt lại.")]
        public bool shootNow;

        public event Action OnFire;
        public event Action OnShootFinished;

        public bool IsShooting { get; private set; }

        private float autoShootCountdown;
        private bool wasAutoShootTest;
        private bool fireEventSent;
        private bool subscribedToFrameUpdates;

        protected override void InitializePlant()
        {
            base.InitializePlant();

            if (IsReady && player != null && !subscribedToFrameUpdates)
            {
                player.OnFrameUpdated += HandleFrameUpdated;
                subscribedToFrameUpdates = true;
            }
        }

        protected override void Update()
        {
            if (player != null)
            {
                float speed = IsShooting
                    ? animationSpeed * shootSpeed
                    : animationSpeed;

                player.SetSpeed(Mathf.Max(0f, speed));
            }

            base.Update();

            if (shootNow)
            {
                shootNow = false;
                Shoot();
            }

            if (!autoShootTest)
            {
                autoShootCountdown = 0f;
                wasAutoShootTest = false;
                return;
            }

            if (!wasAutoShootTest)
            {
                autoShootCountdown = Mathf.Max(0.1f, autoShootInterval);
                wasAutoShootTest = true;
            }

            if (IsShooting)
                return;

            autoShootCountdown -= Time.deltaTime;

            if (autoShootCountdown <= 0f && !Shoot())
                autoShootCountdown = 0.1f;
        }

        protected override void OnDisable()
        {
            if (player != null && subscribedToFrameUpdates)
                player.OnFrameUpdated -= HandleFrameUpdated;

            subscribedToFrameUpdates = false;
            IsShooting = false;
            fireEventSent = false;
            autoShootCountdown = 0f;
            wasAutoShootTest = false;

            base.OnDisable();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            AddMarkerClip(idleMarker, idleClipName, true);
            AddMarkerClip(shootMarker, shootClipName, false);
            AddMarkerClip(sleepMarker, SleepClipName, true);

            // PlantReanimController mặc định khởi chạy "full_idle".
            // Alias này cho phép giữ nguyên giá trị mặc định trong Inspector.
            PlantReanimClip idleClip;
            if (runtimeClips.TryGetValue(idleClipName, out idleClip))
            {
                runtimeClips["full_idle"] = new PlantReanimClip
                {
                    name = "full_idle",
                    startFrame = idleClip.startFrame,
                    endFrame = idleClip.endFrame,
                    loop = true,
                    trackNames = new List<string>(idleClip.trackNames)
                };
            }

            LogMissingClip(idleClipName);
            LogMissingClip(shootClipName);
            LogMissingClip(SleepClipName);
        }

        private void AddMarkerClip(string markerName, string clipName, bool loop)
        {
            if (data == null || data.tracks == null)
                return;

            PlantReanimTrack marker = data.GetTrack(markerName);

            if (marker == null || marker.transforms == null)
            {
                Debug.LogError(
                    "[PuffshroomController] Không tìm thấy marker '" +
                    markerName + "' trong Reanim JSON.",
                    this);
                return;
            }

            int startFrame = -1;
            int endFrame = -1;
            bool visible = true;

            for (int i = 0; i < marker.transforms.Count; i++)
            {
                PlantReanimFrame frame = marker.transforms[i];

                // f = -1 là ẩn marker; f = 0 là marker đang hoạt động.
                if (frame != null && frame.f.HasValue)
                    visible = frame.f.Value != -1;

                if (visible)
                {
                    if (startFrame < 0)
                        startFrame = i;

                    endFrame = i;
                }
                else if (startFrame >= 0)
                {
                    break;
                }
            }

            if (startFrame < 0 || endFrame < startFrame)
            {
                Debug.LogError(
                    "[PuffshroomController] Marker '" + markerName +
                    "' không có frame hiển thị hợp lệ.",
                    this);
                return;
            }

            PlantReanimClip clip = new PlantReanimClip
            {
                name = clipName,
                startFrame = startFrame,
                endFrame = endFrame,
                loop = loop
            };

            // Không loại track chỉ vì tên bắt đầu bằng "anim_":
            // anim_face của Puff-shroom là một track hình ảnh thực.
            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track != null &&
                    !string.IsNullOrWhiteSpace(track.name) &&
                    HasAtlasImage(track))
                {
                    clip.trackNames.Add(track.name);
                }
            }

            if (clip.IsValid(data.FrameCount))
            {
                runtimeClips[clipName] = clip;

                Debug.Log(
                    "[PuffshroomController] Clip '" + clipName +
                    "': frame " + startFrame + "-" + endFrame + ".",
                    this);
            }
            else
            {
                Debug.LogError(
                    "[PuffshroomController] Clip '" + clipName +
                    "' không hợp lệ. Kiểm tra Reanim JSON và atlas trong profile.",
                    this);
            }
        }

        private bool HasAtlasImage(PlantReanimTrack track)
        {
            if (track.transforms == null)
                return false;

            foreach (PlantReanimFrame frame in track.transforms)
            {
                if (frame != null &&
                    frame.i.HasValue &&
                    data.GetAtlasPartByIndex(frame.i.Value) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogMissingClip(string clipName)
        {
            if (!runtimeClips.ContainsKey(clipName))
            {
                Debug.LogError(
                    "[PuffshroomController] Không tạo được clip '" +
                    clipName + "'. Kiểm tra marker và atlas trong profile.",
                    this);
            }
        }

        private void HandleFrameUpdated(PlantReanimPlaybackState state)
        {
            if (!IsShooting ||
                fireEventSent ||
                state.clip == null ||
                !string.Equals(
                    state.clip.name,
                    shootClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int elapsedFrame = state.currentFrame - state.clip.startFrame;

            if (elapsedFrame >= fireFrame)
                Fire();
        }

        private void Fire()
        {
            if (fireEventSent)
                return;

            fireEventSent = true;

            if (logFireEvent)
                Debug.Log("[PuffshroomController] Puff-shroom bắn.", this);

            OnFire?.Invoke();
        }

        public bool Shoot()
        {
            if (!IsReady || player == null || IsShooting)
                return false;

            if (!runtimeClips.ContainsKey(shootClipName))
                return false;

            IsShooting = true;
            fireEventSent = false;

            if (!PlayOnce(shootClipName))
            {
                IsShooting = false;
                return false;
            }

            return true;
        }

        /// <summary>Chạy idle lặp vô hạn.</summary>
        public bool PlayIdle()
        {
            IsShooting = false;
            fireEventSent = false;
            return Play(idleClipName, true);
        }

        /// <summary>Chạy trạng thái ngủ lặp vô hạn.</summary>
        public bool Sleep()
        {
            IsShooting = false;
            fireEventSent = false;
            return Play(SleepClipName, true);
        }

        /// <summary>Đánh thức nấm và chuyển về idle.</summary>
        public bool WakeUp()
        {
            return PlayIdle();
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null ||
                !string.Equals(
                    clip.name,
                    shootClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Nếu fireFrame đặt sau frame cuối, vẫn phát event đúng một lần.
            Fire();

            IsShooting = false;
            player.SetSpeed(Mathf.Max(0f, animationSpeed));

            if (autoShootTest)
                autoShootCountdown = Mathf.Max(0.1f, autoShootInterval);

            PlayIdle();
            OnShootFinished?.Invoke();
        }

        [ContextMenu("Test Puffshroom/Shoot")]
        private void TestShoot()
        {
            Shoot();
        }

        [ContextMenu("Test Puffshroom/Play Idle")]
        private void TestIdle()
        {
            PlayIdle();
        }

        [ContextMenu("Test Puffshroom/Sleep")]
        private void TestSleep()
        {
            Sleep();
        }
    }
}