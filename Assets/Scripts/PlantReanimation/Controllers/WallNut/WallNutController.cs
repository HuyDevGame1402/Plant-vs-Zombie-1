using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.WallNut
{
    public class WallNutController : PlantReanimController
    {
        public enum DamageStage
        {
            Intact,
            Cracked1,
            Cracked2
        }

        private const string IdleMarker = "anim_idle";
        private const string IdleClip = "idle";
        private const string FullIdleClip = "full_idle";
        private const string FaceTrack = "anim_face";

        private static readonly string[] BlinkMarkers =
        {
            "anim_blink_twitch",
            "anim_blink_twice",
            "anim_blink_thrice"
        };

        [Header("Animation")]
        [Min(0f)] public float animationSpeed = 1f;

        [Header("Damage state")]
        [Tooltip("Chọn trực tiếp để thử 3 trạng thái trong Inspector.")]
        public DamageStage damageStage = DamageStage.Intact;

        [Header("Blink")]
        public bool autoBlink = true;
        [Min(0.1f)] public float blinkIntervalMin = 2.5f;
        [Min(0.1f)] public float blinkIntervalMax = 5f;

        [Tooltip("Bật trong Play Mode để thử chớp mắt.")]
        public bool blinkNow;

        private PlantReanimPlayer blinkPlayer;
        private PlantReanimAnimator blinkAnimator;
        private float blinkCountdown;
        private string activeBlinkTrack;
        private bool warnedMissingDamageSprite;

        protected override void InitializePlant()
        {
            base.InitializePlant();

            if (!IsReady || blinkPlayer != null)
                return;

            blinkPlayer = new PlantReanimPlayer();
            blinkPlayer.Initialize(data.fps);

            blinkAnimator = new PlantReanimAnimator();
            blinkAnimator.Initialize(
                data,
                blinkPlayer,
                plantRenderer,
                data.GetAtlasPartByIndex,
                profile.layers);

            blinkPlayer.OnCompleted += HandleBlinkCompleted;

            ApplyDamageStage();
            ResetBlinkCountdown();
        }

        protected override void Update()
        {
            if (player != null)
                player.SetSpeed(Mathf.Max(0f, animationSpeed));

            base.Update();

            if (!IsReady)
                return;

            if (blinkPlayer != null)
            {
                blinkPlayer.SetSpeed(Mathf.Max(0f, animationSpeed));

                if (blinkNow)
                {
                    blinkNow = false;
                    PlayBlinkSequence();
                }

                blinkPlayer.Update(Time.deltaTime);
            }

            // Chạy sau animator để ảnh body không bị Reanim ghi đè lại thành ảnh nguyên vẹn.
            ApplyDamageStage();

            if (!autoBlink ||
                blinkPlayer == null ||
                blinkPlayer.IsPlaying)
            {
                return;
            }

            blinkCountdown -= Time.deltaTime;

            if (blinkCountdown <= 0f)
                PlayBlinkSequence();
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
            activeBlinkTrack = null;
            blinkCountdown = 0f;

            base.OnDisable();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            PlantReanimTrack idleMarker = data.GetTrack(IdleMarker);

            int idleStart;
            int idleEnd;

            if (!TryGetVisibleSpan(idleMarker, out idleStart, out idleEnd))
            {
                Debug.LogError(
                    "[WallNutController] Không tìm thấy marker anim_idle.",
                    this);
                return;
            }

            List<string> idleTracks = GetIdleSpriteTracks();

            AddClip(IdleClip, idleStart, idleEnd, true, idleTracks);
            AddClip(FullIdleClip, idleStart, idleEnd, true, idleTracks);

            foreach (string markerName in BlinkMarkers)
            {
                PlantReanimTrack marker = data.GetTrack(markerName);

                int blinkStart;
                int blinkEnd;

                if (!TryGetVisibleSpan(marker, out blinkStart, out blinkEnd))
                {
                    Debug.LogWarning(
                        "[WallNutController] Không tìm thấy marker " +
                        markerName + ".",
                        this);
                    continue;
                }

                AddClip(
                    markerName,
                    blinkStart,
                    blinkEnd,
                    false,
                    new List<string> { markerName });
            }

            if (string.IsNullOrEmpty(initialClip) ||
                !runtimeClips.ContainsKey(initialClip))
            {
                initialClip = FullIdleClip;
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
                runtimeClips[clipName] = clip;
            else
                Debug.LogError(
                    "[WallNutController] Clip '" + clipName +
                    "' không hợp lệ.",
                    this);
        }

        private bool TryGetVisibleSpan(
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

                if (frame != null && frame.f.HasValue)
                    visible = frame.f.Value != -1;

                if (!visible)
                    continue;

                if (startFrame < 0)
                    startFrame = i;

                // Giữ các khoảng cách ẩn ở giữa clip blink_twice/thrice.
                endFrame = i;
            }

            return startFrame >= 0 && endFrame >= startFrame;
        }

        private List<string> GetIdleSpriteTracks()
        {
            List<string> result = new List<string>();

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null ||
                    string.IsNullOrWhiteSpace(track.name) ||
                    IsBlinkTrack(track.name))
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

        private static bool IsBlinkTrack(string trackName)
        {
            foreach (string blinkMarker in BlinkMarkers)
            {
                if (string.Equals(
                    trackName,
                    blinkMarker,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyDamageStage()
        {
            if (!IsReady || plantRenderer == null || data == null)
                return;

            string atlasImageId;

            switch (damageStage)
            {
                case DamageStage.Cracked1:
                    atlasImageId = "IMAGE_REANIM_WALLNUT_CRACKED1";
                    break;

                case DamageStage.Cracked2:
                    atlasImageId = "IMAGE_REANIM_WALLNUT_CRACKED2";
                    break;

                default:
                    atlasImageId = "IMAGE_REANIM_WALLNUT_BODY";
                    break;
            }

            PlantAtlasPart part = data.GetAtlasPart(atlasImageId);

            if (part == null)
            {
                if (!warnedMissingDamageSprite)
                {
                    Debug.LogError(
                        "[WallNutController] Không tìm thấy atlas image " +
                        atlasImageId + ".",
                        this);
                    warnedMissingDamageSprite = true;
                }

                return;
            }

            plantRenderer.SetAtlasPart(FaceTrack, part);
        }
        /// <summary>
        /// Gọi từ hệ thống máu để tự chọn trạng thái nứt:
        /// trên 2/3 máu = nguyên vẹn,
        /// 1/3–2/3 máu = nứt lần 1,
        /// dưới 1/3 máu = nứt lần 2.
        /// </summary>
        public void SetHealth(float currentHealth, float maxHealth)
        {
            if (maxHealth <= 0f)
                return;

            float healthRatio = Mathf.Clamp01(currentHealth / maxHealth);

            if (healthRatio <= 1f / 3f)
                SetDamageStage(DamageStage.Cracked2);
            else if (healthRatio <= 2f / 3f)
                SetDamageStage(DamageStage.Cracked1);
            else
                SetDamageStage(DamageStage.Intact);
        }

        public void SetDamageStage(DamageStage newStage)
        {
            damageStage = newStage;
            ApplyDamageStage();
        }

        public bool PlayBlinkSequence()
        {
            if (!IsReady ||
                blinkPlayer == null ||
                blinkPlayer.IsPlaying)
            {
                return false;
            }

            List<string> available = new List<string>();

            foreach (string markerName in BlinkMarkers)
            {
                if (runtimeClips.ContainsKey(markerName))
                    available.Add(markerName);
            }

            if (available.Count == 0)
                return false;

            activeBlinkTrack =
                available[UnityEngine.Random.Range(0, available.Count)];

            blinkPlayer.PlayOnce(runtimeClips[activeBlinkTrack]);
            ResetBlinkCountdown();

            return true;
        }

        private void HandleBlinkCompleted(PlantReanimClip clip)
        {
            if (!string.IsNullOrEmpty(activeBlinkTrack) &&
                plantRenderer != null)
            {
                plantRenderer.SetLayerVisible(activeBlinkTrack, false);
            }

            activeBlinkTrack = null;
            ResetBlinkCountdown();
        }

        private void ResetBlinkCountdown()
        {
            float min = Mathf.Max(0.1f, blinkIntervalMin);
            float max = Mathf.Max(min, blinkIntervalMax);

            blinkCountdown = UnityEngine.Random.Range(min, max);
        }

        [ContextMenu("Blink Wall-nut")]
        private void BlinkFromContextMenu()
        {
            PlayBlinkSequence();
        }
    }
}