//using System;
//using System.Collections.Generic;

//[Serializable]
//public class AtlasData
//{
//    public int version;
//    public int content_version;
//    public List<AtlasGroup> groups;
//}

//[Serializable]
//public class AtlasGroup
//{
//    public string type;
//    public string id;
//    public List<AtlasResource> resources;
//}

//[Serializable]
//public class AtlasResource
//{
//    public string type;
//    public int slot;
//    public string id;
//    public string path;
//    public string parent;
//    public int ax, ay, aw, ah;
//}

//[Serializable]
//public class AnimData
//{
//    public int version;
//    public int frame_rate;
//    public float[] position;
//    public float[] size;
//    public List<AnimImage> image;
//}

//[Serializable]
//public class AnimImage
//{
//    public string name;
//    public int[] size;
//    public float[] transform;
//}