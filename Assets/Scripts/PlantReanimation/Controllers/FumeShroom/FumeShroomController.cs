using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Fumeshroom
{
    public class FumeShroomController : PlantReanimController
    {
        [Header("Marker trong Reanim JSON")]
        public string idleMarker = "anim_idle";
        public string shootingMarker = "anim_shooting";
        public string sleepMarker = "anim_sleep";

        [Header("Tên clip")]
        public string idleClipName = "idle";
        public string shootingClipName = "shooting";
        public string sleepClipName = "sleep";

        [Header("Tốc độ")]
        [Min(0f)] public float animationSpeed = 1f;
        [Min(0f)] public float shootingSpeed = 1f;

        [Header("Sự kiện bắn")]
        [Tooltip("Frame tính từ đầu clip shooting; chỉnh nếu cần khớp thời điểm tạo projectile.")]
        [Min(0)] public int fireFrame = 8;
        public bool logFireEvent;

        [Header("Test trong Inspector")]
        [Tooltip("Bật để tự bắn lặp lại trong Play Mode.")]
        public bool autoShootTest;
        [Min(0.1f)] public float autoShootInterval = 2f;
        [Tooltip("Bật trong Play Mode để thử bắn một lần; script tự tắt lại.")]
        public bool shootNow;

        public event Action OnFire;
        public event Action OnShootFinished;

        public bool IsShooting { get; private set; }

        private bool fireEventSent;
        private bool subscribedToFrameUpdates;
        private float autoShootCountdown;
        private bool wasAutoShootTest;

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
                    ? animationSpeed * shootingSpeed
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
            AddMarkerClip(shootingMarker, shootingClipName, false);
            AddMarkerClip(sleepMarker, sleepClipName, true);

            // Giữ tương thích với Initial Clip mặc định của lớp cha.
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
            LogMissingClip(shootingClipName);
            LogMissingClip(sleepClipName);
        }

        private void AddMarkerClip(
            string markerName,
            string clipName,
            bool loop)
        {
            if (data == null || data.tracks == null)
                return;

            PlantReanimTrack marker = data.GetTrack(markerName);

            if (marker == null || marker.transforms == null)
            {
                Debug.LogError(
                    "[FumeShroomController] Không tìm thấy marker '" +
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
                    "[FumeShroomController] Marker '" + markerName +
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

            // Bao gồm cả các track anim_* nếu chúng có sprite thật,
            // chẳng hạn anim_face và anim_blink.
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
                    "[FumeShroomController] Clip '" + clipName +
                    "': frame " + startFrame + "-" + endFrame + ".",
                    this);
            }
            else
            {
                Debug.LogError(
                    "[FumeShroomController] Clip '" + clipName +
                    "' không hợp lệ. Kiểm tra Reanim JSON và Atlas trong profile.",
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
                    "[FumeShroomController] Không tạo được clip '" +
                    clipName + "'. Kiểm tra marker và Atlas trong profile.",
                    this);
            }
        }

        public bool PlayIdle()
        {
            IsShooting = false;
            fireEventSent = false;
            return Play(idleClipName, true);
        }

        public bool Sleep()
        {
            IsShooting = false;
            fireEventSent = false;
            return Play(sleepClipName, true);
        }

        public bool WakeUp()
        {
            return PlayIdle();
        }

        public bool Shoot()
        {
            if (!IsReady || player == null || IsShooting)
                return false;

            if (!runtimeClips.ContainsKey(shootingClipName))
                return false;

            IsShooting = true;
            fireEventSent = false;

            if (!PlayOnce(shootingClipName))
            {
                IsShooting = false;
                return false;
            }

            return true;
        }

        private void HandleFrameUpdated(PlantReanimPlaybackState state)
        {
            if (!IsShooting ||
                fireEventSent ||
                state.clip == null ||
                !string.Equals(
                    state.clip.name,
                    shootingClipName,
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
                Debug.Log("[FumeShroomController] Fume-shroom bắn.", this);

            OnFire?.Invoke();
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null ||
                !string.Equals(
                    clip.name,
                    shootingClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Đảm bảo event vẫn được gọi nếu fireFrame đặt sau frame cuối.
            Fire();

            IsShooting = false;
            player.SetSpeed(Mathf.Max(0f, animationSpeed));
            PlayIdle();

            OnShootFinished?.Invoke();
        }

        [ContextMenu("Test Fume-shroom/Shoot")]
        private void TestShoot()
        {
            Shoot();
        }

        [ContextMenu("Test Fume-shroom/Play Idle")]
        private void TestIdle()
        {
            PlayIdle();
        }

        [ContextMenu("Test Fume-shroom/Sleep")]
        private void TestSleep()
        {
            Sleep();
        }
    }
}