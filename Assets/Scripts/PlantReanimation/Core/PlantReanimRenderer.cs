
using System.Collections.Generic;
using UnityEngine;
using PlantReanimation.Utilities;

namespace PlantReanimation.Core
{
    public class PlantReanimRenderer : MonoBehaviour
    {
        private class Layer
        {
            public string name;

            public GameObject gameObject;
            public Transform transform;

            public Mesh mesh;
            public MeshRenderer meshRenderer;

            public Vector3[] vertices;
            public Vector2[] uvs;

            public PlantAtlasPart currentPart;
            public float x, y, kx, ky, sx = 1f, sy = 1f;
        }

        private readonly Dictionary<string, Layer> layers =
            new Dictionary<string, Layer>();

        private Texture2D atlasTexture;
        private Material sharedMaterial;

        private float pixelsPerUnit = 100f;
        private float positionScale = 1f;
        private float globalScale = 1f;
        private Vector2 plantScale = Vector2.one;

        private MaterialPropertyBlock propertyBlock;
        private string sortingLayerName;
        private int sortingOrderBase;
        private bool shadowConfigured;

        public int LayerCount
        {
            get { return layers.Count; }
        }

        // =========================================
        // INITIALIZE
        // =========================================

        public void Initialize(
            Texture2D texture,
            Material material,
            float pixelsPerUnit = 100f,
            float positionScale = 1f,
            string sortingLayer = "",
            int sortingOrderBase = 0,
            float globalScale = 1f,
            Vector2 plantScale = default(Vector2))
        {
            if (texture == null)
            {
                Debug.LogError(
                    "[PlantReanimRenderer] Atlas Texture is null.");

                return;
            }

            if (material == null)
            {
                Debug.LogError(
                    "[PlantReanimRenderer] Material is null.");

                return;
            }

            ClearLayers();
            shadowConfigured = false;
            atlasTexture = texture;
            sharedMaterial = material;
            sortingLayerName = sortingLayer;
            this.sortingOrderBase = sortingOrderBase;
            this.globalScale = Mathf.Max(0.0001f, globalScale);
            this.plantScale = plantScale == Vector2.zero ? Vector2.one : plantScale;

            this.pixelsPerUnit =
                Mathf.Max(0.0001f, pixelsPerUnit);

            this.positionScale = positionScale;

            propertyBlock = new MaterialPropertyBlock();

            propertyBlock.SetTexture("_MainTex", atlasTexture);
            propertyBlock.SetTexture("_BaseMap", atlasTexture);
        }

        // =========================================
        // CREATE LAYER
        // =========================================

        public void CreateLayer(
            string layerName,
            int sortingOrder)
        {
            if (string.IsNullOrEmpty(layerName))
                return;

            if (layers.ContainsKey(layerName))
                return;

            if (atlasTexture == null || sharedMaterial == null)
            {
                Debug.LogError(
                    "[PlantReanimRenderer] Call Initialize first.");

                return;
            }

            GameObject layerObject = new GameObject(layerName);

            layerObject.transform.SetParent(transform, false);

            MeshFilter meshFilter =
                layerObject.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer =
                layerObject.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();

            mesh.name = "PlantLayer_" + layerName;

            Vector3[] vertices =
                PlantReanimMath.CreateQuadVertices(1f, 1f);

            Vector2[] uvs = new Vector2[4];

            mesh.vertices = vertices;
            mesh.triangles =
                PlantReanimMath.CreateQuadTriangles();

            mesh.uv = uvs;
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;

            meshRenderer.sharedMaterial = sharedMaterial;
            if (!string.IsNullOrEmpty(sortingLayerName)) meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrderBase + sortingOrder;

            meshRenderer.SetPropertyBlock(propertyBlock);

            Layer layer = new Layer
            {
                name = layerName,
                gameObject = layerObject,
                transform = layerObject.transform,
                mesh = mesh,
                meshRenderer = meshRenderer,
                vertices = vertices,
                uvs = uvs
            };

            layers.Add(layerName, layer);
        }

        // =========================================
        // SET ATLAS IMAGE
        // =========================================

        public void SetAtlasPart(
            string layerName,
            PlantAtlasPart part)
        {
            if (part == null)
                return;

            if (!layers.TryGetValue(
                layerName, out Layer layer))
            {
                Debug.LogWarning(
                    "[PlantReanimRenderer] Layer not found: " +
                    layerName);

                return;
            }

            if (layer.currentPart == part)
                return;

            Vector2[] newUVs = PlantReanimMath.CalculateUVs(part, atlasTexture);
            for (int i = 0; i < 4; i++) layer.uvs[i] = newUVs[i];
            layer.mesh.uv = layer.uvs;
            layer.currentPart = part;
            UpdateLayerVertices(layer);
        }

        private const string ShadowLayerA = "__PlantReanimShadowA";
        private const string ShadowLayerB = "__PlantReanimShadowB";

        /// <summary>Draws two identical atlas shadows under the current idle pose.</summary>
        public void ConfigureShadow(PlantAtlasPart shadowPart, Vector2 offset, float scale)
        {
            if (shadowConfigured || shadowPart == null || atlasTexture == null)
                return;

            CreateLayer(ShadowLayerA, -1);
            CreateLayer(ShadowLayerB, -1);
            SetAtlasPart(ShadowLayerA, shadowPart);
            SetAtlasPart(ShadowLayerB, shadowPart);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;

            foreach (Layer layer in layers.Values)
            {
                if (layer.currentPart == null ||
                    layer.name == ShadowLayerA || layer.name == ShadowLayerB ||
                    !layer.gameObject.activeInHierarchy)
                    continue;

                for (int i = 0; i < layer.vertices.Length; i++)
                {
                    Vector3 vertex = layer.vertices[i];
                    minX = Mathf.Min(minX, vertex.x);
                    maxX = Mathf.Max(maxX, vertex.x);
                    minY = Mathf.Min(minY, vertex.y);
                }
            }

            if (minX > maxX)
            {
                layers[ShadowLayerA].gameObject.SetActive(false);
                layers[ShadowLayerB].gameObject.SetActive(false);
                shadowConfigured = true;
                return;
            }

            // Bounds are already in local Unity units and include Global/Plant Scale.
            // The old implementation adds shadowOffset after Plant Scale.
            float centerX = (minX + maxX) * 0.5f + offset.x * positionScale / pixelsPerUnit;
            float centerY = minY - offset.y * positionScale / pixelsPerUnit;
            float safeScale = Mathf.Max(0.0001f, scale);
            float halfWidth = shadowPart.width * globalScale * safeScale / (2f * pixelsPerUnit);
            float halfHeight = shadowPart.height * globalScale * safeScale / (2f * pixelsPerUnit);

            SetShadowQuad(layers[ShadowLayerA], centerX, centerY, halfWidth, halfHeight);
            SetShadowQuad(layers[ShadowLayerB], centerX, centerY, halfWidth, halfHeight);
            shadowConfigured = true;
        }

        private static void SetShadowQuad(Layer layer, float cx, float cy, float halfWidth, float halfHeight)
        {
            layer.vertices[0] = new Vector3(cx - halfWidth, cy + halfHeight, 0f);
            layer.vertices[1] = new Vector3(cx + halfWidth, cy + halfHeight, 0f);
            layer.vertices[2] = new Vector3(cx + halfWidth, cy - halfHeight, 0f);
            layer.vertices[3] = new Vector3(cx - halfWidth, cy - halfHeight, 0f);
            layer.mesh.vertices = layer.vertices;
            layer.mesh.RecalculateBounds();
            layer.meshRenderer.enabled = true;
        }
        // =========================================
        // SET TRANSFORM
        // =========================================

        // Matches the original Peashooter renderer's two-axis affine transform.
        public void SetLayerPose(string layerName, float x, float y, float kx, float ky, float sx, float sy)
        {
            Layer layer;
            if (!layers.TryGetValue(layerName, out layer)) return;
            layer.x = x; layer.y = y; layer.kx = kx; layer.ky = ky; layer.sx = sx; layer.sy = sy;
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;
            UpdateLayerVertices(layer);
        }

        private void UpdateLayerVertices(Layer layer)
        {
            PlantAtlasPart part = layer.currentPart;
            if (part == null) return;
            float rx = layer.kx * Mathf.Deg2Rad, ry = layer.ky * Mathf.Deg2Rad;
            float m00 = Mathf.Cos(rx) * layer.sx * globalScale;
            float m01 = -Mathf.Sin(ry) * layer.sy * globalScale;
            float m10 = Mathf.Sin(rx) * layer.sx * globalScale;
            float m11 = Mathf.Cos(ry) * layer.sy * globalScale;
            float tx = layer.x * positionScale * globalScale, ty = layer.y * positionScale * globalScale;
            float w = part.width, h = part.height;
            layer.vertices[0] = new Vector3(tx * plantScale.x / pixelsPerUnit, -ty * plantScale.y / pixelsPerUnit, 0f);
            layer.vertices[1] = new Vector3((tx + m00 * w) * plantScale.x / pixelsPerUnit, -(ty + m10 * w) * plantScale.y / pixelsPerUnit, 0f);
            layer.vertices[2] = new Vector3((tx + m00 * w + m01 * h) * plantScale.x / pixelsPerUnit, -(ty + m10 * w + m11 * h) * plantScale.y / pixelsPerUnit, 0f);
            layer.vertices[3] = new Vector3((tx + m01 * h) * plantScale.x / pixelsPerUnit, -(ty + m11 * h) * plantScale.y / pixelsPerUnit, 0f);            layer.mesh.vertices = layer.vertices;
            layer.mesh.RecalculateBounds();
        }
        public void SetLayerTransform(
            string layerName,
            Vector3 localPosition,
            float kx,
            float ky,
            Vector3 localScale)
        {
            if (!layers.TryGetValue(
                layerName, out Layer layer))
                return;

            layer.transform.localPosition = localPosition;

            layer.transform.localRotation =
                PlantReanimMath.GetRotation(kx, ky);

            layer.transform.localScale = localScale;
        }

        // =========================================
        // SET POSITION
        // =========================================

        public void SetLayerPosition(
            string layerName,
            Vector3 localPosition)
        {
            if (!layers.TryGetValue(
                layerName, out Layer layer))
                return;

            layer.transform.localPosition = localPosition;
        }

        // =========================================
        // SET SCALE
        // =========================================

        public void SetLayerScale(
            string layerName,
            Vector3 localScale)
        {
            if (!layers.TryGetValue(
                layerName, out Layer layer))
                return;

            layer.transform.localScale = localScale;
        }

        // =========================================
        // SET ROTATION
        // =========================================

        public void SetLayerRotation(
            string layerName,
            float kx,
            float ky)
        {
            if (!layers.TryGetValue(
                layerName, out Layer layer))
                return;

            layer.transform.localRotation =
                PlantReanimMath.GetRotation(kx, ky);
        }

        // =========================================
        // VISIBILITY
        // =========================================

        public void SetLayerVisible(
            string layerName,
            bool visible)
        {
            if (!layers.TryGetValue(
                layerName, out Layer layer))
                return;

            layer.gameObject.SetActive(visible);
        }

        // =========================================
        // SORTING
        // =========================================

        public void SetSortingOrder(
            string layerName,
            int sortingOrder)
        {
            if (!layers.TryGetValue(
                layerName, out Layer layer))
                return;

            layer.meshRenderer.sortingOrder =
                sortingOrder;
        }

        // =========================================
        // CLEANUP
        // =========================================

        public void ClearLayers()
        {
            foreach (Layer layer in layers.Values)
            {
                if (layer.mesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(layer.mesh);
                    else
                        DestroyImmediate(layer.mesh);
                }

                if (layer.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(layer.gameObject);
                    else
                        DestroyImmediate(layer.gameObject);
                }
            }

            layers.Clear();
        }

        private void OnDestroy()
        {
            ClearLayers();
        }
    }
}










