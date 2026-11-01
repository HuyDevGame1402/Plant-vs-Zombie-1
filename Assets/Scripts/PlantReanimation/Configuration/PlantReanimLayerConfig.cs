
using System;
using UnityEngine;

namespace PlantReanimation.Configuration
{
    [Serializable]
    public class PlantReanimLayerConfig
    {
        [Header("Track")]
        public string trackName;

        [Tooltip("Thứ tự render của layer. Giá trị lớn hơn sẽ được render phía trên.")]
        public int sortingOrder;

        [Header("Rendering")]
        public bool enabled = true;

        [Tooltip("Scale riêng của layer.")]
        public Vector2 localScale = Vector2.one;

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(trackName);
            }
        }
    }
}
