using System;
using System.Collections.Generic;
using PlantReanimation.Core;
using PlantReanimation.Controllers;
using UnityEngine;

namespace PlantReanimation.Controllers.Peashooter
{
    /// <summary>
    /// Peashooter playback. The body keeps looping its idle tracks while a second
    /// player runs the head shooting tracks, matching the original component.
    /// </summary>
    public class PeashooterController : PlantReanimController
    {
        private const string BodyIdleClipName = "__body_idle";

        public string idleClipName = "full_idle";
        public string shootClipName = "shooting";
        public float shootSpeed = 3f;
        [Min(0)] public int fireFrame = 12;

        public event Action OnFire;

        private PlantReanimPlayer actionPlayer;
        private PlantReanimAnimator actionAnimator;
        private bool firePending;

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
                profile.layers);

            actionPlayer.OnFrameUpdated += HandleActionFrameUpdated;
            actionPlayer.OnCompleted += HandleActionCompleted;
        }

        protected override void Update()
        {
            base.Update();
            if (actionPlayer != null)
                actionPlayer.Update(Time.deltaTime);
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
            firePending = false;
            base.OnDisable();
        }

        protected override void BuildClips()
        {
            runtimeClips.Clear();

            int idleStart, idleEnd;
            int bodyStart, bodyEnd;
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
                if (track == null || string.IsNullOrEmpty(track.name) || !HasImage(track))
                    continue;

                if (IsVisibleAt(track, bodySampleFrame))
                    bodyTracks.Add(track.name);
                else
                    headTracks.Add(track.name);
            }

            AddClip(idleClipName, idleStart, idleEnd, true, allSpriteTracks);
            AddClip(BodyIdleClipName, idleStart, idleEnd, true, bodyTracks);
            AddClip(shootClipName, shootStart, shootEnd, false, headTracks);

            if (bodyTracks.Count == 0)
                Debug.LogError("Không phân loại được track thân của Peashooter.", this);
            if (headTracks.Count == 0)
                Debug.LogError("Không phân loại được track đầu của Peashooter.", this);
        }

        private void AddClip(string clipName, int start, int end, bool loop, List<string> tracks)
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
                Debug.LogError("Clip '" + clipName + "' không hợp lệ hoặc không có track ảnh.", this);
        }

        private bool TryGetMarkerRange(string markerName, out int start, out int end)
        {
            start = -1;
            end = -1;

            PlantReanimTrack marker = data.GetTrack(markerName);
            if (marker == null || marker.transforms == null)
                return false;

            // Marker frames are sparse. Its f value remains active until changed.
            bool visible = true;
            for (int i = 0; i < marker.transforms.Count; i++)
            {
                PlantReanimFrame frame = marker.transforms[i];
                if (frame != null && frame.f.HasValue)
                    visible = frame.f.Value != -1;

                if (visible)
                {
                    if (start < 0) start = i;
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
                if (track != null && !string.IsNullOrEmpty(track.name) && HasImage(track))
                    names.Add(track.name);
            }
            return names;
        }

        private bool HasImage(PlantReanimTrack track)
        {
            if (track.transforms == null)
                return false;

            foreach (PlantReanimFrame frame in track.transforms)
            {
                if (frame != null && frame.i.HasValue && data.GetAtlasPartByIndex(frame.i.Value) != null)
                    return true;
            }
            return false;
        }

        private bool IsVisibleAt(PlantReanimTrack track, int frameIndex)
        {
            if (track.transforms == null || frameIndex < 0 || frameIndex >= track.transforms.Count)
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

        public bool Shoot()
        {
            if (!IsReady || player == null || actionPlayer == null || actionPlayer.IsPlayingClip(shootClipName))
                return false;

            PlantReanimClip bodyClip;
            PlantReanimClip shootingClip;
            if (!runtimeClips.TryGetValue(BodyIdleClipName, out bodyClip) ||
                !runtimeClips.TryGetValue(shootClipName, out shootingClip))
                return false;

            // Keep body sway running while the action player animates only head tracks.
            float bodyElapsed = player.ElapsedTime;
            if (!Play(BodyIdleClipName, true))
                return false;
            player.Seek(bodyElapsed);

            firePending = true;
            actionPlayer.SetSpeed(Mathf.Max(0f, shootSpeed));
            actionPlayer.PlayOnce(shootingClip);
            return true;
        }

        private void HandleActionFrameUpdated(PlantReanimPlaybackState state)
        {
            if (!firePending || state.clip == null ||
                !string.Equals(state.clip.name, shootClipName, StringComparison.OrdinalIgnoreCase))
                return;

            if (state.currentFrame - state.clip.startFrame >= fireFrame)
                Fire();
        }

        private void HandleActionCompleted(PlantReanimClip clip)
        {
            if (clip == null || !string.Equals(clip.name, shootClipName, StringComparison.OrdinalIgnoreCase))
                return;

            if (firePending)
                Fire();

            actionPlayer.SetSpeed(1f);
            Play(idleClipName, true);
        }

        private void Fire()
        {
            firePending = false;
            if (OnFire != null)
                OnFire();
        }

        [ContextMenu("Shoot Peashooter")]
        private void ShootFromContextMenu()
        {
            Shoot();
        }
    }
}
