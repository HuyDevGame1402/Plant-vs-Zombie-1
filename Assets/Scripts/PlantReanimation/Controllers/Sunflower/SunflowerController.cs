using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using PlantReanimation.Controllers;
using UnityEngine;

namespace PlantReanimation.Controllers.Sunflower
{
    /// <summary>
    /// Sunflower: loops its idle animation while the separate blink track
    /// follows the head's movement.
    /// </summary>
    public class SunflowerController : PlantReanimController
    {
        private const string IdleClipName = "idle";
        private const string FullIdleClipName = "full_idle";
        private const string BlinkClipName = "blink";
        private const string IdleTrackName = "anim_idle";
        private const string BlinkTrackName = "anim_blink";

        [Header("Animation")]
        [Min(0f)] public float animationSpeed = 1f;

        [Header("Blink")]
        public bool autoBlink = true;
        [Min(0.1f)] public float blinkIntervalMin = 3f;
        [Min(0.1f)] public float blinkIntervalMax = 6f;

        [Tooltip("Bật trong Play Mode để chớp mắt một lần.")]
        public bool blinkNow;

        [Header("Blink alignment")]
        [Tooltip("Offset bổ sung nếu mắt vẫn lệch. Tính theo đơn vị Reanim.")]
        public Vector2 blinkPositionOffset = Vector2.zero;

        private PlantReanimPlayer blinkPlayer;
        private PlantReanimAnimator blinkAnimator;
        private Vector2[] idleHeadPositions;
        private float blinkCountdown;

        protected override void InitializePlant()
        {
            base.InitializePlant();

            if (!IsReady || blinkPlayer != null)
                return;

            PlantReanimClip blinkClip;
            if (!runtimeClips.TryGetValue(BlinkClipName, out blinkClip))
            {
                Debug.LogWarning(
                    "[SunflowerController] Không tìm thấy clip blink trong Reanim.",
                    this);
                return;
            }

            blinkPlayer = new PlantReanimPlayer();
            blinkPlayer.Initialize(data.fps);

            blinkAnimator = new PlantReanimAnimator();
            blinkAnimator.Initialize(
                data,
                blinkPlayer,
                plantRenderer,
                data.GetAtlasPartByIndex,
                profile.layers);

            // Cho track mắt đi theo độ dịch chuyển của đầu trong idle.
            blinkAnimator.PositionOffsetProvider = GetBlinkFollowOffset;
            blinkPlayer.OnCompleted += HandleBlinkCompleted;

            ResetBlinkCountdown();
        }

        protected override void Update()
        {
            if (player != null)
                player.SetSpeed(Mathf.Max(0f, animationSpeed));

            base.Update();

            if (blinkPlayer != null)
            {
                blinkPlayer.SetSpeed(Mathf.Max(0f, animationSpeed));
                blinkPlayer.Update(Time.deltaTime);
            }

            if (blinkNow)
            {
                blinkNow = false;
                Blink();
            }

            if (!autoBlink ||
                blinkPlayer == null ||
                blinkPlayer.IsPlayingClip(BlinkClipName))
                return;

            blinkCountdown -= Time.deltaTime;

            if (blinkCountdown <= 0f)
                Blink();
        }

        protected override void OnDisable()
        {
            if (blinkPlayer != null)
            {
                blinkPlayer.OnCompleted -= HandleBlinkCompleted;
                blinkPlayer.Stop();
            }

            if (blinkAnimator != null)
                blinkAnimator.Dispose();

            blinkPlayer = null;
            blinkAnimator = null;
            idleHeadPositions = null;
            blinkCountdown = 0f;

            base.OnDisable();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            PlantReanimTrack idleTrack = data.GetTrack(IdleTrackName);
            PlantReanimTrack blinkTrack = data.GetTrack(BlinkTrackName);

            int idleStart;
            int idleEnd;

            if (!TryGetVisibleRange(idleTrack, out idleStart, out idleEnd))
            {
                Debug.LogError(
                    "[SunflowerController] Không tìm thấy đoạn idle của track anim_idle.",
                    this);
                return;
            }

            int blinkStart;
            int blinkEnd;

            if (!TryGetVisibleRange(blinkTrack, out blinkStart, out blinkEnd))
            {
                Debug.LogError(
                    "[SunflowerController] Không tìm thấy đoạn blink của track anim_blink.",
                    this);
                return;
            }

            CacheIdleHeadPositions(idleTrack, idleStart, idleEnd);

            List<string> spriteTracks = GetSpriteTrackNames();

            // PlantReanimController mặc định tìm clip "full_idle".
            AddClip(
                IdleClipName,
                idleStart,
                idleEnd,
                true,
                spriteTracks);

            AddClip(
                FullIdleClipName,
                idleStart,
                idleEnd,
                true,
                spriteTracks);

            // Chỉ player riêng của blink điều khiển track mắt.
            AddClip(
                BlinkClipName,
                blinkStart,
                blinkEnd,
                false,
                new List<string> { BlinkTrackName });

            if (!runtimeClips.ContainsKey(initialClip))
                initialClip = FullIdleClipName;
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
                    "[SunflowerController] Clip '" + clipName +
                    "' không hợp lệ hoặc thiếu sprite track.",
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

            bool visible = true;

            for (int i = 0; i < track.transforms.Count; i++)
            {
                PlantReanimFrame frame = track.transforms[i];

                // Thuộc tính f được giữ tới frame kế tiếp có f mới.
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
                if (track == null || string.IsNullOrWhiteSpace(track.name))
                    continue;

                if (HasValidImage(track))
                    names.Add(track.name);
            }

            return names;
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

        private void CacheIdleHeadPositions(
            PlantReanimTrack headTrack,
            int idleStart,
            int idleEnd)
        {
            idleHeadPositions = null;

            if (headTrack == null ||
                headTrack.transforms == null ||
                idleEnd < idleStart)
                return;

            int count = idleEnd - idleStart + 1;
            idleHeadPositions = new Vector2[count];

            for (int i = 0; i < count; i++)
                idleHeadPositions[i] = GetTrackPosition(headTrack, idleStart + i);
        }

        private static Vector2 GetTrackPosition(
            PlantReanimTrack track,
            int frameIndex)
        {
            float x = 0f;
            float y = 0f;
            int last = Mathf.Min(frameIndex, track.transforms.Count - 1);

            // Tọa độ Reanim là dữ liệu thưa, nên giữ giá trị gần nhất.
            for (int i = 0; i <= last; i++)
            {
                PlantReanimFrame frame = track.transforms[i];
                if (frame == null)
                    continue;

                if (frame.x.HasValue)
                    x = frame.x.Value;

                if (frame.y.HasValue)
                    y = frame.y.Value;
            }

            return new Vector2(x, y);
        }

        private Vector2 GetBlinkFollowOffset(
            string trackName,
            PlantReanimPlaybackState state)
        {
            if (!string.Equals(
                    trackName,
                    BlinkTrackName,
                    StringComparison.OrdinalIgnoreCase) ||
                idleHeadPositions == null ||
                idleHeadPositions.Length == 0 ||
                player == null ||
                data == null ||
                data.fps <= 0f)
            {
                return Vector2.zero;
            }

            // Lấy vị trí đầu hiện tại trong chu kỳ idle.
            float frame = Mathf.Repeat(
                player.ElapsedTime * data.fps,
                idleHeadPositions.Length);

            int frameA = Mathf.FloorToInt(frame);
            int frameB = (frameA + 1) % idleHeadPositions.Length;

            Vector2 currentHeadPosition = Vector2.Lerp(
                idleHeadPositions[frameA],
                idleHeadPositions[frameB],
                frame - frameA);

            // Bù chuyển động của đầu và cộng thêm offset chỉnh tay.
            Vector2 headMovement =
                currentHeadPosition - idleHeadPositions[0];

            return headMovement + blinkPositionOffset;
        }

        public bool Blink()
        {
            if (!IsReady ||
                blinkPlayer == null ||
                blinkPlayer.IsPlayingClip(BlinkClipName))
            {
                return false;
            }

            PlantReanimClip clip;
            if (!runtimeClips.TryGetValue(BlinkClipName, out clip))
                return false;

            blinkPlayer.SetSpeed(Mathf.Max(0f, animationSpeed));
            blinkPlayer.PlayOnce(clip);
            ResetBlinkCountdown();

            return true;
        }

        private void HandleBlinkCompleted(PlantReanimClip clip)
        {
            if (clip == null ||
                !string.Equals(
                    clip.name,
                    BlinkClipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (plantRenderer != null)
                plantRenderer.SetLayerVisible(BlinkTrackName, false);

            ResetBlinkCountdown();
        }

        private void ResetBlinkCountdown()
        {
            float min = Mathf.Max(0.1f, blinkIntervalMin);
            float max = Mathf.Max(min, blinkIntervalMax);

            blinkCountdown = UnityEngine.Random.Range(min, max);
        }

        [ContextMenu("Blink Sunflower")]
        private void BlinkFromContextMenu()
        {
            Blink();
        }
    }
}