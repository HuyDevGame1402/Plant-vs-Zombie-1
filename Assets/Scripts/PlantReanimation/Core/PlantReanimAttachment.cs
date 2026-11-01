using System;
using UnityEngine;

namespace PlantReanimation.Core
{
    [Serializable]
    public class PlantReanimAttachment
    {
        public string name;
        public string parentTrack;
        public Transform target;
        public Vector2 offset;
        public bool followRotation = true;
        public bool followScale = true;
    }
}
