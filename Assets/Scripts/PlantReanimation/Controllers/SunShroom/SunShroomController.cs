using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Sunshroom
{
    public class SunShroomController : PlantReanimController
    {
        [Header("Marker trong Reanim JSON")]
        public string smallIdleMarker = "anim_idle";
        public string smallSleepMarker = "anim_sleep";
        public string growMarker = "anim_grow";
        public string bigIdleMarker = "anim_bigidle";
        public string bigSleepMarker = "anim_bigsleep";

        [Header("Tên clip")]
        public string smallIdleClipName = "small_idle";
        public string smallSleepClipName = "small_sleep";
        public string growClipName = "grow";
        public string bigIdleClipName = "big_idle";
        public string bigSleepClipName = "big_sleep";

        [Header("Sau khi mọc lớn")]
        [Tooltip("Bật để cây chuyển sang ngủ lớn sau animation grow.")]
        public bool sleepAfterGrow;

        public event Action OnGrown;

        public bool IsBig { get; private set; }
        public bool IsGrowing { get; private set; }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            AddMarkerClip(smallIdleMarker, smallIdleClipName, true);
            AddMarkerClip(smallSleepMarker, smallSleepClipName, true);
            AddMarkerClip(growMarker, growClipName, false);
            AddMarkerClip(bigIdleMarker, bigIdleClipName, true);
            AddMarkerClip(bigSleepMarker, bigSleepClipName, true);

            // Clip mặc định của lớp cha là "full_idle".
            PlantReanimClip idleClip;
            if (runtimeClips.TryGetValue(smallIdleClipName, out idleClip))
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

            LogMissingClip(smallIdleClipName);
            LogMissingClip(smallSleepClipName);
            LogMissingClip(growClipName);
            LogMissingClip(bigIdleClipName);
            LogMissingClip(bigSleepClipName);
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
                    "[SunShroomController] Không tìm thấy marker '" +
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
                    "[SunShroomController] Marker '" + markerName +
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

            // Lấy mọi track có ảnh, kể cả track anim_face và track ảnh
            // anim_sleep trùng tên với marker. Loader sẽ đặt hậu tố cho track trùng.
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
                    "[SunShroomController] Clip '" + clipName +
                    "': frame " + startFrame + "-" + endFrame + ".",
                    this);
            }
            else
            {
                Debug.LogError(
                    "[SunShroomController] Clip '" + clipName +
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
                    "[SunShroomController] Không tạo được clip '" +
                    clipName + "'. Kiểm tra marker và Atlas trong profile.",
                    this);
            }
        }

        /// <summary>Phát trạng thái idle tương ứng với kích thước hiện tại.</summary>
        public bool PlayIdle()
        {
            IsGrowing = false;

            return IsBig
                ? Play(bigIdleClipName, true)
                : Play(smallIdleClipName, true);
        }

        public bool PlaySmallIdle()
        {
            IsBig = false;
            IsGrowing = false;
            return Play(smallIdleClipName, true);
        }

        public bool SleepSmall()
        {
            IsBig = false;
            IsGrowing = false;
            return Play(smallSleepClipName, true);
        }

        public bool PlayBigIdle()
        {
            IsBig = true;
            IsGrowing = false;
            return Play(bigIdleClipName, true);
        }

        public bool SleepBig()
        {
            IsBig = true;
            IsGrowing = false;
            return Play(bigSleepClipName, true);
        }

        /// <summary>
        /// Chạy animation mọc lớn một lần; sau đó chuyển sang idle lớn
        /// hoặc ngủ lớn tùy sleepAfterGrow.
        /// </summary>
        public bool GrowToBig()
        {
            if (!IsReady || IsBig || IsGrowing)
                return false;

            IsGrowing = true;

            if (!PlayOnce(growClipName))
            {
                IsGrowing = false;
                return false;
            }

            return true;
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null ||
                !IsGrowing ||
                !string.Equals(
                    clip.name,
                    growClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            IsGrowing = false;
            IsBig = true;

            if (sleepAfterGrow)
                SleepBig();
            else
                PlayBigIdle();

            OnGrown?.Invoke();
        }

        [ContextMenu("Test Sun-shroom/Small Idle")]
        private void TestSmallIdle()
        {
            PlaySmallIdle();
        }

        [ContextMenu("Test Sun-shroom/Small Sleep")]
        private void TestSmallSleep()
        {
            SleepSmall();
        }

        [ContextMenu("Test Sun-shroom/Grow To Big")]
        private void TestGrow()
        {
            GrowToBig();
        }

        [ContextMenu("Test Sun-shroom/Big Idle")]
        private void TestBigIdle()
        {
            PlayBigIdle();
        }

        [ContextMenu("Test Sun-shroom/Big Sleep")]
        private void TestBigSleep()
        {
            SleepBig();
        }
    }
}