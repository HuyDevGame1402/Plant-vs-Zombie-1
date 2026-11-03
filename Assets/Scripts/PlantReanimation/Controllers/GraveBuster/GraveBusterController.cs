using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.GraveBuster
{
    public class GraveBusterController : PlantReanimController
    {
        [Header("Marker trong Reanim JSON")]
        public string landMarker = "anim_land";
        public string idleMarker = "anim_idle";

        [Header("Tên clip")]
        public string landClipName = "land";
        public string idleClipName = "idle";

        private bool returnToIdleAfterLand;

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            int landStart;
            int landMarkerEnd;
            int idleStart;
            int idleEnd;

            if (!TryGetMarkerRange(landMarker, out landStart, out landMarkerEnd))
            {
                Debug.LogError(
                    "[GraveBusterController] Không tìm thấy marker '" +
                    landMarker + "'.",
                    this);
                return;
            }

            if (!TryGetMarkerRange(idleMarker, out idleStart, out idleEnd))
            {
                Debug.LogError(
                    "[GraveBusterController] Không tìm thấy marker '" +
                    idleMarker + "'.",
                    this);
                return;
            }

            // Marker land kết thúc ở frame 6, nhưng idle chỉ bắt đầu ở frame 11.
            // Giữ các frame 7-10 trong clip land để phát trọn đoạn chuyển tiếp.
            int landEnd = Mathf.Max(landMarkerEnd, idleStart - 1);
            landEnd = Mathf.Min(landEnd, data.FrameCount - 1);

            List<string> spriteTracks = GetSpriteTrackNames();

            AddClip(landClipName, landStart, landEnd, false, spriteTracks);
            AddClip(idleClipName, idleStart, idleEnd, true, spriteTracks);

            // Tương thích với Initial Clip mặc định của PlantReanimController.
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
        }

        private bool TryGetMarkerRange(
            string markerName,
            out int startFrame,
            out int endFrame)
        {
            startFrame = -1;
            endFrame = -1;

            PlantReanimTrack marker = data.GetTrack(markerName);

            if (marker == null || marker.transforms == null)
                return false;

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

            return startFrame >= 0 && endFrame >= startFrame;
        }

        private List<string> GetSpriteTrackNames()
        {
            List<string> names = new List<string>();

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track != null &&
                    !string.IsNullOrWhiteSpace(track.name) &&
                    HasAtlasImage(track))
                {
                    names.Add(track.name);
                }
            }

            return names;
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

        private void AddClip(
            string clipName,
            int startFrame,
            int endFrame,
            bool loop,
            List<string> spriteTracks)
        {
            PlantReanimClip clip = new PlantReanimClip
            {
                name = clipName,
                startFrame = startFrame,
                endFrame = endFrame,
                loop = loop,
                trackNames = new List<string>(spriteTracks)
            };

            if (clip.IsValid(data.FrameCount))
            {
                runtimeClips[clipName] = clip;

                Debug.Log(
                    "[GraveBusterController] Clip '" + clipName +
                    "': frame " + startFrame + "-" + endFrame + ".",
                    this);
            }
            else
            {
                Debug.LogError(
                    "[GraveBusterController] Clip '" + clipName +
                    "' không hợp lệ.",
                    this);
            }
        }

        /// <summary>Chạy idle lặp vô hạn.</summary>
        public bool PlayIdle()
        {
            returnToIdleAfterLand = false;
            return Play(idleClipName, true);
        }

        /// <summary>Chạy đoạn chui xuống đất một lần, rồi chuyển sang idle.</summary>
        public bool PlayLand()
        {
            returnToIdleAfterLand = true;

            if (!PlayOnce(landClipName))
            {
                returnToIdleAfterLand = false;
                return false;
            }

            return true;
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null ||
                !returnToIdleAfterLand ||
                !string.Equals(
                    clip.name,
                    landClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            returnToIdleAfterLand = false;
            PlayIdle();
        }

        protected override void OnDisable()
        {
            returnToIdleAfterLand = false;
            base.OnDisable();
        }

        [ContextMenu("Test Grave Buster/Play Land Then Idle")]
        private void TestLand()
        {
            PlayLand();
        }

        [ContextMenu("Test Grave Buster/Play Idle")]
        private void TestIdle()
        {
            PlayIdle();
        }
    }
}