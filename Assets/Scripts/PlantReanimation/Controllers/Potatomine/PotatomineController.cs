using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers.Chomper
{
    public class PotatomineController : PlantReanimController
    {
        public enum MineState
        {
            Buried,
            Rising,
            Armed,
            Mashed
        }

        private const string BuriedClip = "buried";
        private const string RiseClip = "rise";
        private const string ArmedClip = "armed";
        private const string BlinkClip = "blink";
        private const string MashedClip = "mashed";

        private const string BlinkTrackName = "PotatoMine_blink";

        [Header("State")]
        [SerializeField] private MineState currentState = MineState.Buried;

        public MineState CurrentState
        {
            get { return currentState; }
        }

        [Header("Arming")]
        [Tooltip("Tự trồi lên sau thời gian Arm Delay.")]
        public bool autoRise = true;

        [Min(0f)]
        [Tooltip("Thời gian nằm dưới đất trước khi trồi lên.")]
        public float armDelay = 14f;

        [Header("Blink")]
        public bool autoBlink = true;

        [Min(0.1f)]
        public float blinkIntervalMin = 3f;

        [Min(0.1f)]
        public float blinkIntervalMax = 6f;

        [Header("Test in Play Mode")]
        public bool riseNow;
        public bool blinkNow;
        public bool mashedNow;

        private float riseCountdown;
        private float blinkCountdown;

        private PlantReanimPlayer blinkPlayer;
        private PlantReanimAnimator blinkAnimator;

        public event Action BecameArmed;

        protected override void InitializePlant()
        {
            base.InitializePlant();

            if (!IsReady)
                return;

            currentState = MineState.Buried;
            riseCountdown = Mathf.Max(0f, armDelay);

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
            ResetBlinkCountdown();
        }

        protected override void Update()
        {
            if (player != null)
                player.SetSpeed(1f);

            base.Update();

            if (!IsReady)
                return;

            if (riseNow)
            {
                riseNow = false;
                Rise();
            }

            if (mashedNow)
            {
                mashedNow = false;
                PlayMashed();
            }

            if (currentState == MineState.Buried && autoRise)
            {
                riseCountdown -= Time.deltaTime;

                if (riseCountdown <= 0f)
                    Rise();
            }

            if (currentState != MineState.Armed || blinkPlayer == null)
                return;

            if (blinkNow)
            {
                blinkNow = false;
                PlayBlink();
            }

            blinkPlayer.SetSpeed(1f);
            blinkPlayer.Update(Time.deltaTime);

            if (!autoBlink || blinkPlayer.IsPlaying)
                return;

            blinkCountdown -= Time.deltaTime;

            if (blinkCountdown <= 0f)
                PlayBlink();
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

            base.OnDisable();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            PlantReanimTrack idleMarker =
                data.GetTrack("anim_idle");

            PlantReanimTrack riseMarker =
                data.GetTrack("anim_rise");

            PlantReanimTrack armedMarker =
                data.GetTrack("anim_armed");

            PlantReanimTrack blinkMarker =
                data.GetTrack("anim_blink");

            PlantReanimTrack mashedMarker =
                data.GetTrack("anim_mashed");

            int buriedStart;
            int buriedEnd;

            if (!TryGetVisibleSpan(
                    idleMarker,
                    out buriedStart,
                    out buriedEnd))
            {
                Debug.LogError(
                    "[PotatomineController] Không tìm thấy anim_idle.",
                    this);
                return;
            }

            int riseStart;
            int riseEnd;

            if (!TryGetVisibleSpan(
                    riseMarker,
                    out riseStart,
                    out riseEnd))
            {
                Debug.LogError(
                    "[PotatomineController] Không tìm thấy anim_rise.",
                    this);
                return;
            }

            int armedStart;
            int armedEnd;

            if (!TryGetVisibleSpan(
                    armedMarker,
                    out armedStart,
                    out armedEnd))
            {
                Debug.LogError(
                    "[PotatomineController] Không tìm thấy anim_armed.",
                    this);
                return;
            }

            List<string> spriteTracks = GetSpriteTrackNames();

            AddClip(
                BuriedClip,
                buriedStart,
                buriedEnd,
                true,
                spriteTracks);

            // Tên mặc định của PlantReanimController.
            AddClip(
                "full_idle",
                buriedStart,
                buriedEnd,
                true,
                spriteTracks);

            AddClip(
                RiseClip,
                riseStart,
                riseEnd,
                false,
                spriteTracks);

            AddClip(
                ArmedClip,
                armedStart,
                armedEnd,
                true,
                spriteTracks);

            int blinkStart;
            int blinkEnd;

            if (TryGetVisibleSpan(
                    blinkMarker,
                    out blinkStart,
                    out blinkEnd))
            {
                AddClip(
                    BlinkClip,
                    blinkStart,
                    blinkEnd,
                    false,
                    new List<string> { BlinkTrackName });
            }

            int mashedStart;
            int mashedEnd;

            if (TryGetVisibleSpan(
                    mashedMarker,
                    out mashedStart,
                    out mashedEnd))
            {
                AddClip(
                    MashedClip,
                    mashedStart,
                    mashedEnd,
                    false,
                    spriteTracks);
            }

            if (string.IsNullOrEmpty(initialClip) ||
                !runtimeClips.ContainsKey(initialClip))
            {
                initialClip = "full_idle";
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
                    "[PotatomineController] Clip '" + clipName +
                    "' không hợp lệ.",
                    this);
            }
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

                endFrame = i;
            }

            return startFrame >= 0 && endFrame >= startFrame;
        }

        private List<string> GetSpriteTrackNames()
        {
            List<string> names = new List<string>();

            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null ||
                    string.IsNullOrWhiteSpace(track.name))
                {
                    continue;
                }

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

        public bool Rise()
        {
            if (!IsReady || currentState != MineState.Buried)
                return false;

            if (!PlayOnce(RiseClip))
                return false;

            currentState = MineState.Rising;
            riseCountdown = 0f;
            return true;
        }

        public bool PlayBlink()
        {
            if (!IsReady ||
                currentState != MineState.Armed ||
                blinkPlayer == null ||
                blinkPlayer.IsPlaying)
            {
                return false;
            }

            PlantReanimClip clip;

            if (!runtimeClips.TryGetValue(BlinkClip, out clip))
                return false;

            blinkPlayer.PlayOnce(clip);
            ResetBlinkCountdown();
            return true;
        }

        public bool PlayMashed()
        {
            if (!IsReady ||
                !runtimeClips.ContainsKey(MashedClip))
            {
                return false;
            }

            currentState = MineState.Mashed;
            return PlayOnce(MashedClip);
        }

        public bool ReturnToBuried()
        {
            if (!IsReady || !Play(BuriedClip, true))
                return false;

            currentState = MineState.Buried;
            riseCountdown = Mathf.Max(0f, armDelay);
            return true;
        }

        protected override void HandleClipCompleted(PlantReanimClip clip)
        {
            base.HandleClipCompleted(clip);

            if (clip == null)
                return;

            if (string.Equals(
                    clip.name,
                    RiseClip,
                    StringComparison.OrdinalIgnoreCase))
            {
                currentState = MineState.Armed;
                Play(ArmedClip, true);
                ResetBlinkCountdown();

                if (BecameArmed != null)
                    BecameArmed.Invoke();
            }
        }

        private void HandleBlinkCompleted(PlantReanimClip clip)
        {
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

        [ContextMenu("Rise Potato Mine")]
        private void RiseFromContextMenu()
        {
            Rise();
        }

        [ContextMenu("Blink Potato Mine")]
        private void BlinkFromContextMenu()
        {
            PlayBlink();
        }

        [ContextMenu("Mash Potato Mine")]
        private void MashFromContextMenu()
        {
            PlayMashed();
        }
    }
}