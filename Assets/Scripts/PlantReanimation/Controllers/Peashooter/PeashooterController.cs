using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using PlantReanimation.Controllers;
using UnityEngine;

namespace PlantReanimation.Controllers.Peashooter
{
    /// <summary>
    /// Peashooter playback.
    /// Body keeps looping idle tracks while a second player
    /// runs the head shooting tracks.
    /// </summary>
    public class PeashooterController : PlantReanimController
    {
        private const string BodyIdleClipName = "__body_idle";

        public string idleClipName = "full_idle";
        public string shootClipName = "shooting";

        [Min(0f)] public float animationSpeed = 1f;
        [Min(0f)] public float shootSpeed = 4f;
        [Min(0)] public int fireFrame = 12;

        [Header("Test bắn trong Inspector")]
        [Tooltip("Bật trong Play Mode để tự bắn theo chu kỳ.")]
        public bool autoShootTest;

        [Min(0.1f)] public float autoShootInterval = 2f;

        [Tooltip("Bật trong Play Mode để kích hoạt một lần bắn; controller tự tắt lại.")]
        public bool shootNow;

        [Header("Peashooter V2")]
        [Tooltip("Số lần phát lại trọn clip shooting trong một burst. Repeater = 2.")]
        [Min(1)] public int shotsPerBurst = 2;

        public bool logFireEvent;

        // Called whenever one projectile is fired.
        public event Action OnFire;

        // Called when the entire shooting burst finishes.
        public event Action OnShootFinished;

        // NEW: Called when the complete attack animation finishes.
        public event Action OnAttackCompleted;

        public bool IsShooting { get { return firePending; } }

        public int CurrentShotIndex { get; private set; }

        private PlantReanimPlayer actionPlayer;
        private PlantReanimAnimator actionAnimator;

        private Vector2[] headAttachOffsets;

        private bool firePending;
        private int shotsRemaining;
        private int firedCount;

        private bool projectileFiredThisCycle;

        private float autoShootCountdown;
        private bool wasAutoShootTest;

        protected override void InitializePlant()
        {
            base.InitializePlant();

            if (!IsReady || actionPlayer != null)
                return;

            actionPlayer = new PlantReanimPlayer();
            actionPlayer.Initialize(data.fps);

            actionAnimator = new PlantReanimAnimator();

            actionAnimator.Initialize(
                data,
                actionPlayer,
                plantRenderer,
                data.GetAtlasPartByIndex,
                profile.layers
            );

            actionAnimator.PositionOffsetProvider = GetHeadAttachOffset;
            actionAnimator.BlendSeconds = 0.12f;

            actionPlayer.OnFrameUpdated += HandleActionFrameUpdated;
            actionPlayer.OnCompleted += HandleActionCompleted;
        }

        protected override void Update()
        {
            if (player != null)
                player.SetSpeed(Mathf.Max(0f, animationSpeed));

            base.Update();

            if (actionPlayer != null)
            {
                actionPlayer.SetSpeed(
                    Mathf.Max(
                        0f,
                        animationSpeed * (IsShooting ? shootSpeed : 1f)
                    )
                );

                actionPlayer.Update(Time.deltaTime);
            }

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

            if (autoShootCountdown <= 0f)
            {
                if (!Shoot())
                    autoShootCountdown = 0.1f;
            }
        }

        protected override void OnDisable()
        {
            if (actionPlayer != null)
            {
                actionPlayer.OnFrameUpdated -= HandleActionFrameUpdated;
                actionPlayer.OnCompleted -= HandleActionCompleted;
                actionPlayer.Stop();
            }

            if (actionAnimator != null)
                actionAnimator.Dispose();

            actionPlayer = null;
            actionAnimator = null;

            headAttachOffsets = null;

            firePending = false;
            shotsRemaining = 0;
            firedCount = 0;

            CurrentShotIndex = 0;
            projectileFiredThisCycle = false;

            autoShootCountdown = 0f;
            wasAutoShootTest = false;

            base.OnDisable();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            int idleStart, idleEnd;
            int bodyStart, bodyEnd;
            int headIdleStart, headIdleEnd;
            int shootStart, shootEnd;

            if (!TryGetMarkerRange("anim_full_idle", out idleStart, out idleEnd))
            {
                Debug.LogError("Không tìm thấy marker anim_full_idle trong reanim.", this);
                return;
            }

            if (!TryGetMarkerRange("anim_idle", out bodyStart, out bodyEnd))
            {
                Debug.LogError("Không tìm thấy marker anim_idle để phân loại track thân.", this);
                return;
            }

            if (!TryGetMarkerRange("anim_head_idle", out headIdleStart, out headIdleEnd))
            {
                Debug.LogWarning(
                    "Không tìm thấy marker anim_head_idle; đầu sẽ không được căn theo nhịp lắc của thân.",
                    this
                );

                headIdleStart = headIdleEnd = -1;
            }

            if (!TryGetMarkerRange("anim_shooting", out shootStart, out shootEnd))
            {
                Debug.LogError("Không tìm thấy marker anim_shooting trong reanim.", this);
                return;
            }

            List<string> allSpriteTracks = GetSpriteTrackNames();

            List<string> bodyTracks = new List<string>();
            List<string> headTracks = new List<string>();

            int bodySampleFrame = bodyStart;

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null ||
                    string.IsNullOrEmpty(track.name) ||
                    !HasImage(track))
                    continue;

                if (IsVisibleAt(track, bodySampleFrame))
                    bodyTracks.Add(track.name);
                else
                    headTracks.Add(track.name);
            }

            AddClip(idleClipName, idleStart, idleEnd, true, allSpriteTracks);

            AddClip(
                BodyIdleClipName,
                idleStart,
                idleEnd,
                true,
                bodyTracks
            );

            AddClip(
                shootClipName,
                shootStart,
                shootEnd,
                false,
                headTracks
            );

            BuildHeadAttachOffsets(
                idleStart,
                idleEnd,
                headIdleStart,
                headIdleEnd
            );

            if (bodyTracks.Count == 0)
                Debug.LogError("Không phân loại được track thân của Peashooter.", this);

            if (headTracks.Count == 0)
                Debug.LogError("Không phân loại được track đầu của Peashooter.", this);
        }

        private void AddClip(
            string clipName,
            int start,
            int end,
            bool loop,
            List<string> tracks)
        {
            PlantReanimClip clip = new PlantReanimClip
            {
                name = clipName,
                startFrame = start,
                endFrame = end,
                loop = loop
            };

            clip.trackNames.AddRange(tracks);

            if (clip.IsValid(data.FrameCount))
                runtimeClips[clip.name] = clip;
            else
                Debug.LogError(
                    "Clip '" + clipName + "' không hợp lệ hoặc không có track ảnh.",
                    this
                );
        }

        private bool TryGetMarkerRange(
            string markerName,
            out int start,
            out int end)
        {
            start = -1;
            end = -1;

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
                    if (start < 0)
                        start = i;

                    end = i;
                }
                else if (start >= 0)
                {
                    break;
                }
            }

            return start >= 0 && end >= start;
        }

        private List<string> GetSpriteTrackNames()
        {
            List<string> names = new List<string>();

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track != null &&
                    !string.IsNullOrEmpty(track.name) &&
                    HasImage(track))
                {
                    names.Add(track.name);
                }
            }

            return names;
        }

        private bool HasImage(PlantReanimTrack track)
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

        private bool IsVisibleAt(PlantReanimTrack track, int frameIndex)
        {
            if (track.transforms == null ||
                frameIndex < 0 ||
                frameIndex >= track.transforms.Count)
                return false;

            bool visible = true;

            for (int i = 0; i <= frameIndex; i++)
            {
                PlantReanimFrame frame = track.transforms[i];

                if (frame != null && frame.f.HasValue)
                    visible = frame.f.Value != -1;
            }

            return visible;
        }

        private void BuildHeadAttachOffsets(
            int fullIdleStart,
            int fullIdleEnd,
            int headIdleStart,
            int headIdleEnd)
        {
            headAttachOffsets = null;

            if (headIdleStart < 0 || headIdleEnd < headIdleStart)
                return;

            PlantReanimTrack faceTrack = data.GetTrack("anim_face");

            int frameCount = Mathf.Min(
                fullIdleEnd - fullIdleStart + 1,
                headIdleEnd - headIdleStart + 1
            );

            if (faceTrack == null ||
                faceTrack.transforms == null ||
                frameCount <= 0)
                return;

            headAttachOffsets = new Vector2[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                Vector2 fullPose = GetTrackPosition(
                    faceTrack,
                    fullIdleStart + i
                );

                Vector2 headPose = GetTrackPosition(
                    faceTrack,
                    headIdleStart + i
                );

                headAttachOffsets[i] = fullPose - headPose;
            }
        }

        private static Vector2 GetTrackPosition(
            PlantReanimTrack track,
            int frameIndex)
        {
            float x = 0f;
            float y = 0f;

            int last = Mathf.Min(
                frameIndex,
                track.transforms.Count - 1
            );

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

        private Vector2 GetHeadAttachOffset(
            string trackName,
            PlantReanimPlaybackState state)
        {
            if (headAttachOffsets == null ||
                headAttachOffsets.Length == 0 ||
                !string.Equals(
                    state.clip != null ? state.clip.name : null,
                    shootClipName,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                player == null ||
                data == null ||
                data.fps <= 0f)
            {
                return Vector2.zero;
            }

            float frame = Mathf.Repeat(
                player.ElapsedTime * data.fps,
                headAttachOffsets.Length
            );

            int a = Mathf.FloorToInt(frame);
            int b = (a + 1) % headAttachOffsets.Length;

            return Vector2.Lerp(
                headAttachOffsets[a],
                headAttachOffsets[b],
                frame - a
            );
        }

        public bool Shoot()
        {
            if (!IsReady ||
                player == null ||
                actionPlayer == null ||
                firePending ||
                actionPlayer.IsPlayingClip(shootClipName))
            {
                return false;
            }

            PlantReanimClip bodyClip;
            PlantReanimClip shootingClip;

            if (!runtimeClips.TryGetValue(BodyIdleClipName, out bodyClip) ||
                !runtimeClips.TryGetValue(shootClipName, out shootingClip))
            {
                return false;
            }

            // Preserve body sway.
            float bodyElapsed = player.ElapsedTime;

            if (!Play(BodyIdleClipName, true))
                return false;

            player.Seek(bodyElapsed);

            firePending = true;

            shotsRemaining = Mathf.Max(1, shotsPerBurst);
            firedCount = 0;

            CurrentShotIndex = 0;
            projectileFiredThisCycle = false;

            actionPlayer.SetSpeed(
                Mathf.Max(0f, animationSpeed * shootSpeed)
            );

            actionAnimator.BeginBlend();
            actionPlayer.PlayOnce(shootingClip);

            return true;
        }

        private void HandleActionFrameUpdated(
            PlantReanimPlaybackState state)
        {
            if (!firePending ||
                state.clip == null ||
                !string.Equals(
                    state.clip.name,
                    shootClipName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return;
            }

            int elapsedFrame =
                state.currentFrame - state.clip.startFrame;

            if (!projectileFiredThisCycle &&
                elapsedFrame >= fireFrame)
            {
                FireNextProjectile();
            }
        }

        private void HandleActionCompleted(PlantReanimClip clip)
        {
            if (clip == null ||
                !string.Equals(
                    clip.name,
                    shootClipName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return;
            }

            // Fallback: ensure one projectile per completed animation cycle.
            if (firePending && !projectileFiredThisCycle)
                FireNextProjectile();

            // Another shooting cycle is required.
            if (shotsRemaining > 0)
            {
                projectileFiredThisCycle = false;

                PlantReanimClip shootingClip;

                if (runtimeClips.TryGetValue(
                    shootClipName,
                    out shootingClip))
                {
                    actionPlayer.PlayOnce(shootingClip);
                }

                return;
            }

            // All projectiles and attack animations are completed.
            CompleteAttack();
        }

        private void CompleteAttack()
        {
            firePending = false;

            actionPlayer.SetSpeed(
                Mathf.Max(0f, animationSpeed)
            );

            float bodyElapsed =
                player != null ? player.ElapsedTime : 0f;

            Play(idleClipName, true);

            if (player != null)
                player.Seek(bodyElapsed);

            if (autoShootTest)
            {
                autoShootCountdown = Mathf.Max(
                    0.1f,
                    autoShootInterval
                );
            }

            // NEW: callback after the entire attack animation finishes.
            OnAttackCompleted?.Invoke();

            // Preserve the old callback.
            OnShootFinished?.Invoke();
        }

        private void FireNextProjectile()
        {
            projectileFiredThisCycle = true;

            shotsRemaining--;

            CurrentShotIndex = firedCount;

            firedCount++;

            if (logFireEvent)
            {
                Debug.Log(
                    "Peashooter Repeater: OnFire viên " +
                    firedCount + "/" +
                    Mathf.Max(1, shotsPerBurst),
                    this
                );
            }

            OnFire?.Invoke();
        }

        [ContextMenu("Shoot Peashooter")]
        private void ShootFromContextMenu()
        {
            Shoot();
        }

        public void PlayAnimationIdle()
        {
            Play(idleClipName, true);
        }
    }
}