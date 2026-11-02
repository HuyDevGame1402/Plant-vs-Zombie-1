using System;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Chomper
{
    /// <summary>
    /// Điều khiển các trạng thái trong chomper.reanim.compiled.reanim.json:
    /// idle -> bite -> chew -> swallow.
    /// </summary>
    public class ChomperController : PlantReanimController
    {
        [Header("Tên marker trong Reanim JSON")]
        public string idleMarker = "anim_idle";
        public string biteMarker = "anim_bite";
        public string chewMarker = "anim_chew";
        public string swallowMarker = "anim_swallow";

        [Header("Tên clip")]
        public string idleClipName = "idle";
        public string biteClipName = "bite";
        public string chewClipName = "chew";
        public string swallowClipName = "swallow";

        private bool eatSequenceRunning;
        private bool returnToIdleWhenActionCompletes;

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            AddMarkerClip(idleMarker, idleClipName, true);
            AddMarkerClip(biteMarker, biteClipName, false);
            AddMarkerClip(chewMarker, chewClipName, false);
            AddMarkerClip(swallowMarker, swallowClipName, false);

            // PlantReanimController mặc định khởi chạy clip "full_idle".
            // Tạo alias để giữ nguyên giá trị mặc định đó trong Inspector.
            PlantReanimClip idleClip;
            if (runtimeClips.TryGetValue(idleClipName, out idleClip))
            {
                runtimeClips["full_idle"] = new PlantReanimClip
                {
                    name = "full_idle",
                    startFrame = idleClip.startFrame,
                    endFrame = idleClip.endFrame,
                    loop = true,
                    trackNames = new System.Collections.Generic.List<string>(
                        idleClip.trackNames)
                };
            }

            LogMissingClip(idleClipName);
            LogMissingClip(biteClipName);
            LogMissingClip(chewClipName);
            LogMissingClip(swallowClipName);
        }

        private void AddMarkerClip(string markerName, string clipName, bool loop)
        {
            if (data == null || data.tracks == null)
                return;

            PlantReanimTrack marker = data.GetTrack(markerName);
            if (marker == null || marker.transforms == null)
            {
                Debug.LogError(
                    "[ChomperController] Không tìm thấy marker '" +
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

                // Marker bắt đầu hiện nếu f khác -1.
                // Nếu f bị bỏ trống thì giữ trạng thái frame trước.
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
                    "[ChomperController] Marker '" + markerName +
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

            // Tất cả track hình ảnh cần được cập nhật trong từng clip.
            // Bỏ qua marker anim_* vì chúng chỉ đánh dấu vùng animation.
            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null ||
                    string.IsNullOrWhiteSpace(track.name) ||
                    track.name.StartsWith(
                        "anim_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (HasAtlasImage(track))
                    clip.trackNames.Add(track.name);
            }

            if (clip.IsValid(data.FrameCount))
            {
                runtimeClips[clipName] = clip;
                Debug.Log(
                    "[ChomperController] Clip '" + clipName +
                    "': frame " + startFrame + "-" + endFrame + ".",
                    this);
            }
            else
            {
                Debug.LogError(
                    "[ChomperController] Clip '" + clipName +
                    "' không hợp lệ. Hãy kiểm tra atlas và Reanim JSON.",
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
                    "[ChomperController] Không tạo được clip '" +
                    clipName + "'. Kiểm tra marker và atlas trong profile.",
                    this);
            }
        }

        /// <summary>Chạy idle lặp vô hạn.</summary>
        public bool PlayIdle()
        {
            eatSequenceRunning = false;
            returnToIdleWhenActionCompletes = false;
            return Play(idleClipName, true);
        }

        /// <summary>Chỉ phát animation cắn một lần rồi trở về idle.</summary>
        public bool PlayBite()
        {
            return PlaySingleAction(biteClipName);
        }

        /// <summary>Chỉ phát animation nhai một lần rồi trở về idle.</summary>
        public bool PlayChew()
        {
            return PlaySingleAction(chewClipName);
        }

        /// <summary>Chỉ phát animation nuốt một lần rồi trở về idle.</summary>
        public bool PlaySwallow()
        {
            return PlaySingleAction(swallowClipName);
        }

        /// <summary>
        /// Chạy trọn chuỗi cắn, nhai, nuốt rồi quay về idle.
        /// Gọi hàm này khi Chomper bắt đầu ăn zombie.
        /// </summary>
        public bool EatZombie()
        {
            eatSequenceRunning = true;
            returnToIdleWhenActionCompletes = false;

            if (!PlayOnce(biteClipName))
            {
                eatSequenceRunning = false;
                return false;
            }

            return true;
        }

        private bool PlaySingleAction(string clipName)
        {
            eatSequenceRunning = false;
            returnToIdleWhenActionCompletes = true;

            if (!PlayOnce(clipName))
            {
                returnToIdleWhenActionCompletes = false;
                return false;
            }

            return true;
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null)
                return;

            if (eatSequenceRunning)
            {
                if (string.Equals(
                    clip.name, biteClipName, StringComparison.OrdinalIgnoreCase))
                {
                    PlayOnce(chewClipName);
                    return;
                }

                if (string.Equals(
                    clip.name, chewClipName, StringComparison.OrdinalIgnoreCase))
                {
                    PlayOnce(swallowClipName);
                    return;
                }

                if (string.Equals(
                    clip.name, swallowClipName, StringComparison.OrdinalIgnoreCase))
                {
                    eatSequenceRunning = false;
                    Play(idleClipName, true);
                    return;
                }
            }

            if (returnToIdleWhenActionCompletes &&
                (string.Equals(
                    clip.name, biteClipName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    clip.name, chewClipName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    clip.name, swallowClipName, StringComparison.OrdinalIgnoreCase)))
            {
                returnToIdleWhenActionCompletes = false;
                Play(idleClipName, true);
            }
        }

        [ContextMenu("Test Chomper/Play Idle")]
        private void TestIdle()
        {
            PlayIdle();
        }

        [ContextMenu("Test Chomper/Play Bite")]
        private void TestBite()
        {
            PlayBite();
        }

        [ContextMenu("Test Chomper/Play Chew")]
        private void TestChew()
        {
            PlayChew();
        }

        [ContextMenu("Test Chomper/Play Swallow")]
        private void TestSwallow()
        {
            PlaySwallow();
        }

        [ContextMenu("Test Chomper/Eat Sequence")]
        private void TestEatSequence()
        {
            EatZombie();
        }
    }
}