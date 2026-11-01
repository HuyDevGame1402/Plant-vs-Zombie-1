using UnityEngine;
using PlantReanimation.Core;
namespace PlantReanimation.Utilities
{
    public static class PlantReanimMath
    {
        // Order matches the front face seen by a 2D camera at negative Z.
        public static Vector3[] CreateQuadVertices(float width, float height)
        {
            return new[] { new Vector3(0f, 0f), new Vector3(width, 0f), new Vector3(width, -height), new Vector3(0f, -height) };
        }
        public static int[] CreateQuadTriangles() { return new[] { 0, 1, 2, 0, 2, 3 }; }
        public static Vector2[] CalculateUVs(PlantAtlasPart part, Texture2D atlasTexture)
        {
            if (part == null || atlasTexture == null) return new Vector2[4];
            float u0 = part.x / atlasTexture.width, u1 = (part.x + part.width) / atlasTexture.width;
            float v0 = 1f - (part.y + part.height) / atlasTexture.height, v1 = 1f - part.y / atlasTexture.height;
            return new[] { new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0) };
        }
        public static Vector2 GetWorldSize(PlantAtlasPart part, float pixelsPerUnit, float positionScale)
        {
            return part == null ? Vector2.zero : new Vector2(part.width, part.height) / Mathf.Max(0.0001f, pixelsPerUnit) * positionScale;
        }
        public static Quaternion GetRotation(float kx, float ky) { return Quaternion.Euler(kx, ky, 0f); }
    }
}
