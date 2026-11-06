using System;
using System.Collections.Generic;
using PlantReanimation.Configuration;
using PlantReanimation.Utilities;
using UnityEngine;

namespace PlantReanimation.Core
{
    public class PlantReanimAnimator : IDisposable
    {
        private PlantReanimData data;
        private PlantReanimPlayer player;
        private PlantReanimRenderer renderer;
        private Func<int, PlantAtlasPart> atlasIndexResolver;
        private IList<PlantReanimLayerConfig> layerConfigs;
        private bool initialized, disposed;
        private readonly Dictionary<string, PlantReanimFrame[]> resolvedTracks = new Dictionary<string, PlantReanimFrame[]>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Optional per-track translation in source Reanim coordinates.</summary>
        public Func<string, PlantReanimPlaybackState, Vector2> PositionOffsetProvider { get; set; }
        public bool IsInitialized { get { return initialized; } }

        // ---------- Blend giữa các clip ----------
        private struct Pose { public float x, y, kx, ky, sx, sy; }
        private readonly Dictionary<string, Pose> blendFrom = new Dictionary<string, Pose>(StringComparer.OrdinalIgnoreCase);
        private PlantReanimClip lastClip;
        private bool blendPending;
        private float blendStartTime = -1f;

        /// <summary>Thời gian blend (giây). 0 = tắt.</summary>
        public float BlendSeconds { get; set; }

        /// <summary>Gọi trước khi Play để blend từ pose hiện tại sang clip mới.</summary>
        public void BeginBlend() { blendPending = true; }

        private void CaptureBlendFrom()
        {
            blendFrom.Clear();
            foreach (PlantReanimTrack t in data.tracks)
            {
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                Pose p;
                if (renderer.TryGetLayerPose(t.name, out p.x, out p.y, out p.kx, out p.ky, out p.sx, out p.sy))
                    blendFrom[t.name] = p;
            }
            blendStartTime = Time.time;
        }

        public void Initialize(PlantReanimData reanimData, PlantReanimPlayer animationPlayer, PlantReanimRenderer animationRenderer, Func<int, PlantAtlasPart> indexResolver = null, IList<PlantReanimLayerConfig> layerConfigs = null)
        {
            if (reanimData == null) throw new ArgumentNullException(nameof(reanimData));
            if (animationPlayer == null) throw new ArgumentNullException(nameof(animationPlayer));
            if (animationRenderer == null) throw new ArgumentNullException(nameof(animationRenderer));
            Dispose();
            data = reanimData; player = animationPlayer; renderer = animationRenderer;
            atlasIndexResolver = indexResolver; this.layerConfigs = layerConfigs;
            disposed = false;
            BuildResolvedFrames(); CreateAllLayers();
            player.OnFrameUpdated += OnFrameUpdated;
            initialized = true;
        }

        private void BuildResolvedFrames()
        {
            resolvedTracks.Clear();
            if (data.tracks == null) return;
            foreach (PlantReanimTrack track in data.tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.name) || track.transforms == null) continue;
                PlantReanimFrame[] frames = new PlantReanimFrame[track.transforms.Count];
                PlantReanimFrame previous = null;
                for (int i = 0; i < frames.Length; i++)
                {
                    PlantReanimFrame source = track.transforms[i];
                    PlantReanimFrame resolved = new PlantReanimFrame
                    {
                        x = source != null ? source.x ?? (previous != null ? previous.x : 0f) : previous != null ? previous.x : 0f,
                        y = source != null ? source.y ?? (previous != null ? previous.y : 0f) : previous != null ? previous.y : 0f,
                        kx = source != null ? source.kx ?? (previous != null ? previous.kx : 0f) : previous != null ? previous.kx : 0f,
                        ky = source != null ? source.ky ?? (previous != null ? previous.ky : 0f) : previous != null ? previous.ky : 0f,
                        sx = source != null ? source.sx ?? (previous != null ? previous.sx : 1f) : previous != null ? previous.sx : 1f,
                        sy = source != null ? source.sy ?? (previous != null ? previous.sy : 1f) : previous != null ? previous.sy : 1f,
                        f = source != null ? source.f ?? (previous != null ? previous.f : 0) : previous != null ? previous.f : 0,
                        i = source != null ? source.i ?? (previous != null ? previous.i : (int?)null) : previous != null ? previous.i : null
                    };
                    frames[i] = resolved; previous = resolved;
                }
                resolvedTracks[track.name] = frames;
            }
        }

        private void CreateAllLayers()
        {
            for (int i = 0; i < data.tracks.Count; i++)
            {
                PlantReanimTrack track = data.tracks[i];
                if (track == null || string.IsNullOrEmpty(track.name)) continue;
                PlantReanimLayerConfig config = GetLayerConfig(track.name);
                renderer.CreateLayer(track.name, config != null ? config.sortingOrder : PlantReanimSorting.GetOrder(i));
                if (config != null && !config.enabled) renderer.SetLayerVisible(track.name, false);
            }
        }

        private void OnFrameUpdated(PlantReanimPlaybackState state)
        {
            if (!initialized || disposed || state.clip == null) return;
            if (BlendSeconds > 0f && (blendPending || state.clip != lastClip))
            {
                CaptureBlendFrom();
                blendPending = false;
            }
            lastClip = state.clip;
            if (state.clip.trackNames == null || state.clip.trackNames.Count == 0)
            {
                foreach (PlantReanimTrack track in data.tracks) if (track != null) ApplyTrack(track, state);
                return;
            }
            foreach (string name in state.clip.trackNames)
            {
                PlantReanimTrack track = data.GetTrack(name);
                if (track != null) ApplyTrack(track, state);
            }
        }

        private void ApplyTrack(PlantReanimTrack track, PlantReanimPlaybackState state)
        {
            PlantReanimFrame a = GetFrame(track.name, state.currentFrame);
            PlantReanimFrame b = GetFrame(track.name, state.nextFrame);
            if (a == null && b == null) return;
            if (a == null) a = b; if (b == null) b = a;
            float t = Mathf.Clamp01(state.interpolation);
            float x = Lerp(a.x, b.x, 0f, t);
            float y = Lerp(a.y, b.y, 0f, t);
            if (PositionOffsetProvider != null)
            {
                Vector2 offset = PositionOffsetProvider(track.name, state);
                x += offset.x;
                y += offset.y;
            }
            float kx = Mathf.Lerp(a.kx ?? 0f, b.kx ?? a.kx ?? 0f, t);
            float ky = Mathf.Lerp(a.ky ?? 0f, b.ky ?? a.ky ?? 0f, t);
            PlantReanimLayerConfig config = GetLayerConfig(track.name);
            float sx = Lerp(a.sx, b.sx, 1f, t) * (config != null ? config.localScale.x : 1f);
            float sy = Lerp(a.sy, b.sy, 1f, t) * (config != null ? config.localScale.y : 1f);
            if (BlendSeconds > 0f && blendStartTime >= 0f)
            {
                float w = Mathf.Clamp01((Time.time - blendStartTime) / BlendSeconds);
                Pose from;
                if (w < 1f && blendFrom.TryGetValue(track.name, out from))
                {
                    w = w * w * (3f - 2f * w);
                    x = Mathf.Lerp(from.x, x, w);
                    y = Mathf.Lerp(from.y, y, w);
                    kx = Mathf.Lerp(from.kx, kx, w);
                    ky = Mathf.Lerp(from.ky, ky, w);
                    sx = Mathf.Lerp(from.sx, sx, w);
                    sy = Mathf.Lerp(from.sy, sy, w);
                }
            }
            int? imageIndex = ResolveDiscrete(a.i, b.i, t);
            if (imageIndex.HasValue)
            {
                PlantAtlasPart part = atlasIndexResolver != null ? atlasIndexResolver(imageIndex.Value) : data.GetAtlasPartByIndex(imageIndex.Value);
                if (part != null) renderer.SetAtlasPart(track.name, part);
            }
            renderer.SetLayerPose(track.name, x, y, kx, ky, sx, sy);
            int? visibility = ResolveDiscrete(a.f, b.f, t);
            if (visibility.HasValue) renderer.SetLayerVisible(track.name, visibility.Value != -1 && (config == null || config.enabled));
        }
        private PlantReanimFrame GetFrame(string trackName, int index)
        {
            PlantReanimFrame[] frames;
            return resolvedTracks.TryGetValue(trackName, out frames) && index >= 0 && index < frames.Length ? frames[index] : null;
        }
        private static float Lerp(float? a, float? b, float fallback, float t) { float from = a ?? fallback; return Mathf.Lerp(from, b ?? from, t); }
        private static int? ResolveDiscrete(int? a, int? b, float t) { return a ?? b; }
        private PlantReanimLayerConfig GetLayerConfig(string name)
        {
            if (layerConfigs == null) return null;
            foreach (PlantReanimLayerConfig config in layerConfigs) if (config != null && string.Equals(config.trackName, name, StringComparison.OrdinalIgnoreCase)) return config;
            return null;
        }
        public void Refresh() { if (player != null && player.CurrentClip != null) player.Seek(player.ElapsedTime); }
        public void Dispose()
        {
            if (disposed) return;
            if (player != null) player.OnFrameUpdated -= OnFrameUpdated;
            blendFrom.Clear(); lastClip = null; blendPending = false; blendStartTime = -1f; BlendSeconds = 0f;
            resolvedTracks.Clear(); data = null; player = null; renderer = null; atlasIndexResolver = null; layerConfigs = null; PositionOffsetProvider = null;
            initialized = false; disposed = true;
        }
    }
}