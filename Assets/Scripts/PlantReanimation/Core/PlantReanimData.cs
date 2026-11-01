using System;
using System.Collections.Generic;

namespace PlantReanimation.Core
{
    [Serializable]
    public class PlantAtlasPart
    {
        public string id;
        public float x, y, width, height;
    }

    [Serializable]
    public class PlantReanimFrame
    {
        public float? x, y, kx, ky, sx, sy;
        public int? f, i;
    }

    [Serializable]
    public class PlantReanimTrack
    {
        public string name;
        public List<PlantReanimFrame> transforms = new List<PlantReanimFrame>();
    }

    [Serializable]
    public class PlantReanimData
    {
        public float fps;
        public int atlasIndexOffset;
        public Dictionary<string, PlantAtlasPart> atlasParts = new Dictionary<string, PlantAtlasPart>(StringComparer.OrdinalIgnoreCase);
        public List<PlantAtlasPart> atlasPartList = new List<PlantAtlasPart>();
        public List<PlantReanimTrack> tracks = new List<PlantReanimTrack>();
        [NonSerialized] private Dictionary<string, PlantReanimTrack> trackLookup;

        public void BuildTrackLookup()
        {
            trackLookup = new Dictionary<string, PlantReanimTrack>(StringComparer.OrdinalIgnoreCase);
            if (tracks == null) return;
            foreach (PlantReanimTrack track in tracks)
                if (track != null && !string.IsNullOrWhiteSpace(track.name)) trackLookup[track.name] = track;
        }

        public PlantReanimTrack GetTrack(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName)) return null;
            if (trackLookup == null) BuildTrackLookup();
            PlantReanimTrack track;
            return trackLookup.TryGetValue(trackName, out track) ? track : null;
        }

        public PlantAtlasPart GetAtlasPart(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || atlasParts == null) return null;
            PlantAtlasPart part;
            return atlasParts.TryGetValue(id.Split('|')[0].Trim(), out part) ? part : null;
        }

        public PlantAtlasPart GetAtlasPartByIndex(int reanimIndex)
        {
            int index = reanimIndex - atlasIndexOffset;
            return atlasPartList != null && index >= 0 && index < atlasPartList.Count ? atlasPartList[index] : null;
        }

        public int FrameCount
        {
            get { int count = 0; if (tracks != null) foreach (var t in tracks) if (t != null && t.transforms != null) count = Math.Max(count, t.transforms.Count); return count; }
        }
    }
}
