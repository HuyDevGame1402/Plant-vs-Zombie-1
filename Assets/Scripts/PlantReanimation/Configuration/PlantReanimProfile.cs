
using System.Collections.Generic;
using UnityEngine;
using PlantReanimation.Core;

namespace PlantReanimation.Configuration
{
    [CreateAssetMenu(
        fileName = "PlantReanimProfile",
        menuName = "Plant Reanimation/Profile"
    )]
    public class PlantReanimProfile : ScriptableObject
    {
        [Header("Data")]
        public TextAsset atlasJson;
        public TextAsset reanimJson;

        [Header("Rendering")]
        public Texture2D atlasTexture;
        public Material material;

        [Min(0.01f)]
        public float pixelsPerUnit = 100f;

        [Min(0.0001f)]
        public float positionScale = 2f;

        [Header("Plant Scale")]
        public Vector2 plantScale = Vector2.one;

        [Header("Bóng")]
        public bool showShadow = true;
        public Vector2 shadowOffset = new Vector2(0f, -3f);
        [Min(0.01f)] public float shadowScale = 1.2f;

        [Min(0.0001f)]
        public float globalScale = 1f;

        [Header("Animation Clips")]
        public List<PlantReanimClipConfig> clips =
            new List<PlantReanimClipConfig>();

        [Header("Layers")]
        public List<PlantReanimLayerConfig> layers =
            new List<PlantReanimLayerConfig>();


        [Header("Vị trí neo")]
        [Tooltip("Tọa độ Reanim tại điểm đặt cây, thường là giữa đáy cây.")]
        public Vector2 reanimOrigin = Vector2.zero;

        // =====================================================
        // VALIDATION
        // =====================================================

        public bool ValidateProfile()
        {
            bool valid = true;

            if (atlasJson == null)
            {
                Debug.LogError(
                    $"[{name}] Chưa gán Atlas JSON.",
                    this
                );

                valid = false;
            }

            if (reanimJson == null)
            {
                Debug.LogError(
                    $"[{name}] Chưa gán Reanim JSON.",
                    this
                );

                valid = false;
            }

            if (atlasTexture == null)
            {
                Debug.LogError(
                    $"[{name}] Chưa gán Atlas Texture.",
                    this
                );

                valid = false;
            }

            if (pixelsPerUnit <= 0f)
            {
                Debug.LogError(
                    $"[{name}] Pixels Per Unit phải lớn hơn 0.",
                    this
                );

                valid = false;
            }

            if (positionScale <= 0f || globalScale <= 0f)
            {
                Debug.LogError(
                    $"[{name}] Scale phải lớn hơn 0.",
                    this
                );

                valid = false;
            }

            return valid;
        }

        // =====================================================
        // CLIP LOOKUP
        // =====================================================

        public PlantReanimClipConfig GetClipConfig(
            string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return null;

            foreach (PlantReanimClipConfig clip in clips)
            {
                if (clip != null &&
                    clip.clipName == clipName)
                {
                    return clip;
                }
            }

            return null;
        }

        // =====================================================
        // LAYER LOOKUP
        // =====================================================

        public PlantReanimLayerConfig GetLayerConfig(
            string trackName)
        {
            if (string.IsNullOrEmpty(trackName))
                return null;

            foreach (PlantReanimLayerConfig layer in layers)
            {
                if (layer != null &&
                    layer.trackName == trackName)
                {
                    return layer;
                }
            }

            return null;
        }

        // =====================================================
        // CREATE RUNTIME CLIP
        // =====================================================

        public PlantReanimClip CreateRuntimeClip(
            string clipName)
        {
            PlantReanimClipConfig config =
                GetClipConfig(clipName);

            if (config == null)
            {
                Debug.LogError(
                    $"[{name}] Không tìm thấy clip '{clipName}'.",
                    this
                );

                return null;
            }

            return config.CreateRuntimeClip();
        }
    }
}





