
using System;
using System.Collections.Generic;
using UnityEngine;
using PlantReanimation.Core;

namespace PlantReanimation.Configuration
{
    [Serializable]
    public class PlantReanimClipConfig
    {
        [Header("Clip")]
        public string clipName;

        [Min(0)]
        public int startFrame;

        [Min(0)]
        public int endFrame;

        public bool loop = true;

        [Header("Tracks")]
        [Tooltip("Danh sách track được sử dụng trong clip này.")]
        public List<string> trackNames = new List<string>();

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(clipName)
                    && startFrame >= 0
                    && endFrame >= startFrame
                    && trackNames != null
                    && trackNames.Count > 0;
            }
        }

        public PlantReanimClip CreateRuntimeClip()
        {
            if (!IsValid)
            {
                Debug.LogError(
                    $"PlantReanimClipConfig '{clipName}' không hợp lệ."
                );

                return null;
            }

            PlantReanimClip clip = new PlantReanimClip
            {
                name = clipName,
                startFrame = startFrame,
                endFrame = endFrame,
                loop = loop
            };

            clip.trackNames.AddRange(trackNames);

            return clip;
        }
    }
}
