using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Chomper
{
    /// <summary>
    /// Cherrybomb: idle loop và explosion chạy một lần.
    /// Có thể kích nổ bằng Inspector, Context Menu hoặc code gameplay.
    /// </summary>
    public class CherrybombController : PlantReanimController
    {
        private const string IdleMarkerName = "anim_idle";
        private const string ExplodeMarkerName = "anim_explode";

        private const string IdleClipName = "idle";
        private const string FullIdleClipName = "full_idle";
        private const string ExplodeClipName = "explode";

        [Header("Cherrybomb")]
        [Tooltip("Bật để Cherrybomb tự nổ sau Fuse Delay.")]
        public bool autoExplode;

        [Min(0f)]
        [Tooltip("Thời gian chờ trước khi tự nổ, tính bằng giây.")]
        public float fuseDelay = 1f;

        [Tooltip("Bật checkbox này trong Play Mode để thử animation nổ.")]
        public bool explodeNow;

        private float fuseTimer;
        private bool isExploding;

        /// <summary>
        /// Phát khi animation nổ chạy xong.
        /// Game có thể đăng ký để gây sát thương hoặc xóa cây.
        /// </summary>
        public event Action Exploded;

        protected override void InitializePlant()
        {
            base.InitializePlant();

            if (!IsReady)
                return;

            isExploding = false;
            fuseTimer = Mathf.Max(0f, fuseDelay);

            if (string.IsNullOrEmpty(initialClip) ||
                !runtimeClips.ContainsKey(initialClip))
            {
                initialClip = FullIdleClipName;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!IsReady || isExploding)
                return;

            if (explodeNow)
            {
                explodeNow = false;
                Explode();
                return;
            }

            if (!autoExplode)
                return;

            fuseTimer -= Time.deltaTime;

            if (fuseTimer <= 0f)
                Explode();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            PlantReanimTrack idleMarker = data.GetTrack(IdleMarkerName);
            PlantReanimTrack explodeMarker = data.GetTrack(ExplodeMarkerName);

            int idleStart;
            int idleEnd;

            if (!TryGetVisibleRange(idleMarker, out idleStart, out idleEnd))
            {
                Debug.LogError(
                    "[CherrybombController] Không tìm thấy đoạn anim_idle trong Reanim.",
                    this);
                return;
            }

            int explodeStart;
            int explodeEnd;

            if (!TryGetVisibleRange(
                    explodeMarker,
                    out explodeStart,
                    out explodeEnd))
            {
                Debug.LogError(
                    "[CherrybombController] Không tìm thấy đoạn anim_explode trong Reanim.",
                    this);
                return;
            }

            List<string> spriteTracks = GetSpriteTrackNames();

            AddClip(
                IdleClipName,
                idleStart,
                idleEnd,
                true,
                spriteTracks);

            // Alias này khớp với initialClip mặc định của PlantReanimController.
            AddClip(
                FullIdleClipName,
                idleStart,
                idleEnd,
                true,
                spriteTracks);

            AddClip(
                ExplodeClipName,
                explodeStart,
                explodeEnd,
                false,
                spriteTracks);

            if (string.IsNullOrEmpty(initialClip) ||
                !runtimeClips.ContainsKey(initialClip))
            {
                initialClip = FullIdleClipName;
            }
        }

        private void AddClip(
            string clipName,
            int startFrame,
            int endFrame,
            bool loop,
            List<string> trackNames)
        {
            PlantReanimClip clip = new PlantReanimClip
            {
                name = clipName,
                startFrame = startFrame,
                endFrame = endFrame,
                loop = loop
            };

            clip.trackNames.AddRange(trackNames);

            if (clip.IsValid(data.FrameCount))
            {
                runtimeClips[clipName] = clip;
            }
            else
            {
                Debug.LogError(
                    "[CherrybombController] Clip '" + clipName +
                    "' không hợp lệ.",
                    this);
            }
        }

        private bool TryGetVisibleRange(
            PlantReanimTrack track,
            out int startFrame,
            out int endFrame)
        {
            startFrame = -1;
            endFrame = -1;

            if (track == null || track.transforms == null)
                return false;

            // Trong marker Reanim, f = -1 đánh dấu ẩn;
            // các frame sau giữ trạng thái gần nhất.
            bool visible = true;

            for (int i = 0; i < track.transforms.Count; i++)
            {
                PlantReanimFrame frame = track.transforms[i];

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
            List<string> result = new List<string>();

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

                if (HasValidImage(track))
                    result.Add(track.name);
            }

            return result;
        }

        private bool HasValidImage(PlantReanimTrack track)
        {
            if (track.transforms == null)
                return false;

            foreach (PlantReanimFrame frame in track.transforms)
            {
                if (frame == null || !frame.i.HasValue)
                    continue;

                if (data.GetAtlasPartByIndex(frame.i.Value) != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gọi từ gameplay để bắt đầu animation nổ.
        /// Trả về false nếu cây chưa sẵn sàng hoặc đã nổ.
        /// </summary>
        public bool Explode()
        {
            if (!IsReady || isExploding)
                return false;

            if (!runtimeClips.ContainsKey(ExplodeClipName))
            {
                Debug.LogWarning(
                    "[CherrybombController] Không có clip explode.",
                    this);
                return false;
            }

            isExploding = true;
            return PlayOnce(ExplodeClipName);
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null ||
                !string.Equals(
                    clip.name,
                    ExplodeClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Exploded != null)
                Exploded.Invoke();
        }

        [ContextMenu("Test Cherrybomb Explosion")]
        private void TestExplosionFromContextMenu()
        {
            Explode();
        }
    }
}