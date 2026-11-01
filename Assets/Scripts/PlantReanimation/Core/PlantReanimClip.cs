using System;
using System.Collections.Generic;

namespace PlantReanimation.Core
{
    [Serializable]
    public class PlantReanimClip
    {
        public string name;
        public int startFrame;
        public int endFrame;
        public List<string> trackNames = new List<string>();
        public bool loop = true;
        public int FrameCount { get { return Math.Max(0, endFrame - startFrame + 1); } }
        public float Duration(float fps) { return fps > 0f ? Math.Max(0, FrameCount - 1) / fps : 0f; }
        public bool IsValid(int totalFrames) { return !string.IsNullOrWhiteSpace(name) && startFrame >= 0 && endFrame >= startFrame && endFrame < totalFrames && trackNames != null && trackNames.Count > 0; }
    }
}
