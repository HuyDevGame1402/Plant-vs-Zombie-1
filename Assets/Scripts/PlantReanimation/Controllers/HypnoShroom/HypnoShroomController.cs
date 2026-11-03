using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.HypnoShroom
{
    public class HypnoShroomController : PlantReanimController
    {
        [Header("Marker trong Reanim JSON")]
        public string idleMarker = "anim_idle";
        public string sleepMarker = "anim_sleep";

        [Header("Tên clip")]
        public string idleClipName = "idle";
        public string sleepClipName = "sleep";

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            AddMarkerClip(idleMarker, idleClipName, true);
            AddMarkerClip(sleepMarker, sleepClipName, true);

            // Tương thích với Initial Clip mặc định của lớp cha.
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
            LogMissingClip(sleepClipName);
        }

        private void AddMarkerClip(
            string markerName,
            string clipName,
            bool loop)
        {
            PlantReanimTrack marker = data.GetTrack(markerName);

            if (marker == null || marker.transforms == null)
            {
                Debug.LogError(
                    "[HypnoShroomController] Không tìm thấy marker '" +
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
                    "[HypnoShroomController] Marker '" + markerName +
                    "' không có frame hiển thị hợp lệ.",
                    this);
                return;
            }

            PlantReanimClip clip = new PlantReanimClip
            {
                name = clipName,
                startFrame = startFrame,
                endFrame = endFrame,
                loop = loop,
                trackNames = GetSpriteTrackNames()
            };

            if (clip.IsValid(data.FrameCount))
            {
                runtimeClips[clipName] = clip;

                Debug.Log(
                    "[HypnoShroomController] Clip '" + clipName +
                    "': frame " + startFrame + "-" + endFrame + ".",
                    this);
            }
            else
            {
                Debug.LogError(
                    "[HypnoShroomController] Clip '" + clipName +
                    "' không hợp lệ. Kiểm tra Reanim JSON và Atlas trong profile.",
                    this);
            }
        }

        private List<string> GetSpriteTrackNames()
        {
            List<string> names = new List<string>();

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null ||
                    string.IsNullOrWhiteSpace(track.name) ||
                    track.transforms == null)
                {
                    continue;
                }

                foreach (PlantReanimFrame frame in track.transforms)
                {
                    if (frame != null &&
                        frame.i.HasValue &&
                        data.GetAtlasPartByIndex(frame.i.Value) != null)
                    {
                        names.Add(track.name);
                        break;
                    }
                }
            }

            return names;
        }

        private void LogMissingClip(string clipName)
        {
            if (!runtimeClips.ContainsKey(clipName))
            {
                Debug.LogError(
                    "[HypnoShroomController] Không tạo được clip '" +
                    clipName + "'. Kiểm tra marker và Atlas trong profile.",
                    this);
            }
        }

        public bool PlayIdle()
        {
            return Play(idleClipName, true);
        }

        public bool Sleep()
        {
            return Play(sleepClipName, true);
        }

        public bool WakeUp()
        {
            return PlayIdle();
        }

        [ContextMenu("Test Hypno-shroom/Play Idle")]
        private void TestIdle()
        {
            PlayIdle();
        }

        [ContextMenu("Test Hypno-shroom/Sleep")]
        private void TestSleep()
        {
            Sleep();
        }
    }
}