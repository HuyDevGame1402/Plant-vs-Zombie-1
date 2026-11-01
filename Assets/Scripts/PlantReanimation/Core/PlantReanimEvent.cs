using System;

namespace PlantReanimation.Core
{
    [Serializable]
    public class PlantReanimEvent
    {
        public string name;
        public int frame;
        public PlantReanimEvent() { }
        public PlantReanimEvent(string eventName, int eventFrame) { name = eventName; frame = eventFrame; }
    }
}
