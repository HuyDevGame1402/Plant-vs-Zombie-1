using System;
using System.Collections.Generic;
using PlantReanimation.Configuration;
using PlantReanimation.Core;
using UnityEngine;

namespace PlantReanimation.Controllers
{
    [DisallowMultipleComponent]
    public class PlantReanimController : MonoBehaviour
    {
        public PlantReanimProfile profile;
        public string initialClip = "full_idle";
        public string sortingLayerName = "";
        public int sortingOrder;
        public bool playOnEnable = true;

        [Tooltip("Thời gian blend khi chuyển clip (giây). 0 = tắt.")]
        [Min(0f)] public float clipBlendSeconds = 0.15f;

        protected PlantReanimData data;
        protected PlantReanimPlayer player;
        protected PlantReanimAnimator animator;
        protected PlantReanimRenderer plantRenderer;

        protected readonly Dictionary<string, PlantReanimClip> runtimeClips =
            new Dictionary<string, PlantReanimClip>(StringComparer.OrdinalIgnoreCase);

        private Material generatedMaterial;

        public event Action<PlantReanimClip> ClipCompleted;

        public bool IsReady { get; private set; }
        public PlantReanimPlayer Player { get { return player; } }

        protected virtual void OnEnable()
        {
            InitializePlant();
        }

        protected virtual void Update()
        {
            if (IsReady && player != null)
                player.Update(Time.deltaTime);
        }

        protected virtual void OnDisable()
        {
            ReleasePlant();
        }

        protected virtual void InitializePlant()
        {
            if (IsReady)
                return;

            if (profile == null || !profile.ValidateProfile())
                return;

            try
            {
                data = PlantReanimCache.Get(
                    profile.atlasJson,
                    profile.reanimJson);

                plantRenderer = GetComponent<PlantReanimRenderer>();
                if (plantRenderer == null)
                    plantRenderer = gameObject.AddComponent<PlantReanimRenderer>();

                Material material = profile.material;
                if (material == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                        throw new InvalidOperationException(
                            "Không tìm thấy shader Sprites/Default.");

                    generatedMaterial = new Material(shader)
                    {
                        name = "PlantReanim_RuntimeMaterial"
                    };

                    material = generatedMaterial;
                }

                // Truyền đủ Global Scale và Plant Scale vào renderer.
                plantRenderer.Initialize(
                    profile.atlasTexture,
                    material,
                    profile.pixelsPerUnit,
                    profile.positionScale,
                    sortingLayerName,
                    sortingOrder,
                    profile.globalScale,
                    profile.plantScale);

                player = new PlantReanimPlayer();
                player.Initialize(data.fps);

                animator = new PlantReanimAnimator();
                animator.Initialize(
                    data,
                    player,
                    plantRenderer,
                    data.GetAtlasPartByIndex,
                    profile.layers);

                animator.BlendSeconds = clipBlendSeconds;

                BuildClips();

                player.OnCompleted += HandleClipCompleted;
                IsReady = true;

                if (playOnEnable && !string.IsNullOrEmpty(initialClip))
                    Play(initialClip, true);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[PlantReanimController] Khởi tạo thất bại: " + e,
                    this);

                ReleasePlant();
            }
        }

        protected virtual void BuildClips()
        {
            runtimeClips.Clear();

            if (profile.clips == null)
                return;

            foreach (PlantReanimClipConfig config in profile.clips)
            {
                if (config == null)
                    continue;

                PlantReanimClip clip = config.CreateRuntimeClip();
                if (clip == null)
                    continue;

                if (clip.IsValid(data.FrameCount))
                    runtimeClips[clip.name] = clip;
                else
                    Debug.LogError(
                        "Clip '" + clip.name +
                        "' có frame range vượt timeline hoặc thiếu track.",
                        this);
            }
        }

        public bool Play(string clipName, bool? loop = null)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(clipName))
                return false;

            PlantReanimClip clip;
            if (!runtimeClips.TryGetValue(clipName, out clip))
            {
                Debug.LogWarning(
                    "Không có clip '" + clipName + "' trong profile.",
                    this);

                return false;
            }

            player.Play(clip, loop ?? clip.loop);
            EnsureShadow();
            return true;
        }

        private void EnsureShadow()
        {
            if (profile == null || !profile.showShadow || plantRenderer == null || data == null)
                return;

            PlantAtlasPart shadowPart = data.GetAtlasPart("IMAGE_PLANTSHADOW");
            if (shadowPart == null)
            {
                Debug.LogWarning("[PlantReanimController] Atlas không có IMAGE_PLANTSHADOW.", this);
                return;
            }

            plantRenderer.ConfigureShadow(shadowPart, profile.shadowOffset, profile.shadowScale);
        }
        public bool PlayOnce(string clipName)
        {
            return Play(clipName, false);
        }

        public void Pause()
        {
            if (player != null)
                player.Pause();
        }

        public void Resume()
        {
            if (player != null)
                player.Resume();
        }

        public void SetSpeed(float speed)
        {
            if (player != null)
                player.SetSpeed(speed);
        }

        protected virtual void HandleClipCompleted(PlantReanimClip clip)
        {
            if (ClipCompleted != null)
                ClipCompleted(clip);
        }

        protected virtual void ReleasePlant()
        {
            IsReady = false;

            if (player != null)
                player.OnCompleted -= HandleClipCompleted;

            if (animator != null)
                animator.Dispose();

            if (plantRenderer != null)
                plantRenderer.ClearLayers();

            if (player != null)
                player.Stop();

            player = null;
            animator = null;
            plantRenderer = null;
            data = null;
            runtimeClips.Clear();

            if (generatedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedMaterial);
                else
                    DestroyImmediate(generatedMaterial);

                generatedMaterial = null;
            }
        }
    }
}