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
            public bool hasPose;
        }

        private const string ShadowLayerA = "__PlantReanimShadowA";
        private const string ShadowLayerB = "__PlantReanimShadowB";

        private readonly Dictionary<string, Layer> layers =
            new Dictionary<string, Layer>();

        [Header("Điểm đặt cây")]
        [Tooltip(
            "Tự căn gốc GameObject vào giữa đáy hình cây ở pose đầu tiên. " +
            "Bật mục này để đặt cây theo vị trí trồng.")]
        public bool autoAnchorToBottomCenter = true;

        [Tooltip(
            "Bù thêm vị trí sau khi tự căn, tính theo đơn vị Unity local. " +
            "Để (0, 0) nếu không cần chỉnh thêm.")]
        public Vector2 anchorAdjustment = Vector2.zero;

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

        private bool anchorResolved;
        private Vector2 automaticAnchorOffset;

        public int LayerCount
        {
            get { return layers.Count; }
        }

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
                    "[PlantReanimRenderer] Atlas Texture is null.",
                    this);
                return;
            }

            if (material == null)
            {
                Debug.LogError(
                    "[PlantReanimRenderer] Material is null.",
                    this);
                return;
            }

            ClearLayers();

            shadowConfigured = false;
            anchorResolved = !autoAnchorToBottomCenter;
            automaticAnchorOffset = Vector2.zero;

            atlasTexture = texture;
            sharedMaterial = material;
            sortingLayerName = sortingLayer;
            this.sortingOrderBase = sortingOrderBase;
            this.globalScale = Mathf.Max(0.0001f, globalScale);
            this.plantScale =
                plantScale == Vector2.zero ? Vector2.one : plantScale;
            this.pixelsPerUnit = Mathf.Max(0.0001f, pixelsPerUnit);
            this.positionScale = positionScale;

            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetTexture("_MainTex", atlasTexture);
            propertyBlock.SetTexture("_BaseMap", atlasTexture);
        }

        private void LateUpdate()
        {
            ResolveAnchorFromFirstVisiblePose();
        }

        private void ResolveAnchorFromFirstVisiblePose()
        {
            if (!autoAnchorToBottomCenter || anchorResolved)
                return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            bool foundVisibleBody = false;

            foreach (Layer layer in layers.Values)
            {
                if (layer == null ||
                    layer.currentPart == null ||
                    layer.gameObject == null ||
                    !layer.gameObject.activeInHierarchy ||
                    IsShadowLayer(layer.name))
                {
                    continue;
                }

                for (int i = 0; i < layer.vertices.Length; i++)
                {
                    Vector3 vertex = layer.vertices[i];
                    minX = Mathf.Min(minX, vertex.x);
                    maxX = Mathf.Max(maxX, vertex.x);
                    minY = Mathf.Min(minY, vertex.y);
                    foundVisibleBody = true;
                }
            }

            // Chưa có pose hiển thị; thử lại ở LateUpdate tiếp theo.
            if (!foundVisibleBody || minX > maxX)
                return;

            // Tọa độ Unity: y nhỏ hơn là thấp hơn trên màn hình.
            automaticAnchorOffset = new Vector2(
                (minX + maxX) * 0.5f,
                minY);

            anchorResolved = true;

            foreach (Layer layer in layers.Values)
            {
                if (layer == null || layer.currentPart == null)
                    continue;

                if (IsShadowLayer(layer.name))
                {
                    TranslateLayerVertices(
                        layer,
                        -automaticAnchorOffset.x - anchorAdjustment.x,
                        -automaticAnchorOffset.y - anchorAdjustment.y);
                }
                else
                {
                    UpdateLayerVertices(layer);
                }
            }
        }

        private static bool IsShadowLayer(string layerName)
        {
            return layerName == ShadowLayerA ||
                   layerName == ShadowLayerB;
        }

        public void CreateLayer(string layerName, int sortingOrder)
        {
            if (string.IsNullOrEmpty(layerName) ||
                layers.ContainsKey(layerName))
            {
                return;
            }

            if (atlasTexture == null || sharedMaterial == null)
            {
                Debug.LogError(
                    "[PlantReanimRenderer] Call Initialize first.",
                    this);
                return;
            }

            GameObject layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(transform, false);

            MeshFilter meshFilter = layerObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = layerObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh
            {
                name = "PlantLayer_" + layerName
            };

            Vector3[] vertices =
                PlantReanimMath.CreateQuadVertices(1f, 1f);
            Vector2[] uvs = new Vector2[4];

            mesh.vertices = vertices;
            mesh.triangles = PlantReanimMath.CreateQuadTriangles();
            mesh.uv = uvs;
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = sharedMaterial;

            if (!string.IsNullOrEmpty(sortingLayerName))
                meshRenderer.sortingLayerName = sortingLayerName;

            meshRenderer.sortingOrder = sortingOrderBase + sortingOrder;
            meshRenderer.SetPropertyBlock(propertyBlock);

            layers.Add(layerName, new Layer
            {
                name = layerName,
                gameObject = layerObject,
                transform = layerObject.transform,
                mesh = mesh,
                meshRenderer = meshRenderer,
                vertices = vertices,
                uvs = uvs
            });
        }

        public void SetAtlasPart(string layerName, PlantAtlasPart part)
        {
            if (part == null)
                return;

            Layer layer;
            if (!layers.TryGetValue(layerName, out layer))
            {
                Debug.LogWarning(
                    "[PlantReanimRenderer] Layer not found: " + layerName,
                    this);
                return;
            }

            if (layer.currentPart == part)
                return;

            Vector2[] newUvs =
                PlantReanimMath.CalculateUVs(part, atlasTexture);

            for (int i = 0; i < 4; i++)
                layer.uvs[i] = newUvs[i];

            layer.mesh.uv = layer.uvs;
            layer.currentPart = part;
            UpdateLayerVertices(layer);
        }

        public void ConfigureShadow(
            PlantAtlasPart shadowPart,
            Vector2 offset,
            float scale)
        {
            if (shadowConfigured ||
                shadowPart == null ||
                atlasTexture == null)
            {
                return;
            }

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
                    IsShadowLayer(layer.name) ||
                    !layer.gameObject.activeInHierarchy)
                {
                    continue;
                }

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

            float centerX =
                (minX + maxX) * 0.5f +
                offset.x * positionScale / pixelsPerUnit;

            float centerY =
                minY -
                offset.y * positionScale / pixelsPerUnit;

            float safeScale = Mathf.Max(0.0001f, scale);
            float halfWidth =
                shadowPart.width * globalScale * safeScale /
                (2f * pixelsPerUnit);
            float halfHeight =
                shadowPart.height * globalScale * safeScale /
                (2f * pixelsPerUnit);

            SetShadowQuad(
                layers[ShadowLayerA],
                centerX,
                centerY,
                halfWidth,
                halfHeight);

            SetShadowQuad(
                layers[ShadowLayerB],
                centerX,
                centerY,
                halfWidth,
                halfHeight);

            shadowConfigured = true;
        }

        private static void SetShadowQuad(
            Layer layer,
            float centerX,
            float centerY,
            float halfWidth,
            float halfHeight)
        {
            layer.vertices[0] =
                new Vector3(centerX - halfWidth, centerY + halfHeight, 0f);
            layer.vertices[1] =
                new Vector3(centerX + halfWidth, centerY + halfHeight, 0f);
            layer.vertices[2] =
                new Vector3(centerX + halfWidth, centerY - halfHeight, 0f);
            layer.vertices[3] =
                new Vector3(centerX - halfWidth, centerY - halfHeight, 0f);

            layer.mesh.vertices = layer.vertices;
            layer.mesh.RecalculateBounds();
            layer.meshRenderer.enabled = true;
        }

        public void SetLayerPose(
            string layerName,
            float x,
            float y,
            float kx,
            float ky,
            float sx,
            float sy)
        {
            Layer layer;
            if (!layers.TryGetValue(layerName, out layer))
                return;

            layer.x = x;
            layer.y = y;
            layer.kx = kx;
            layer.ky = ky;
            layer.sx = sx;
            layer.sy = sy;
            layer.hasPose = true;

            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            UpdateLayerVertices(layer);
        }

        public bool TryGetLayerPose(
            string layerName,
            out float x,
            out float y,
            out float kx,
            out float ky,
            out float sx,
            out float sy)
        {
            Layer layer;
            if (layers.TryGetValue(layerName, out layer) && layer.hasPose)
            {
                x = layer.x;
                y = layer.y;
                kx = layer.kx;
                ky = layer.ky;
                sx = layer.sx;
                sy = layer.sy;
                return true;
            }

            x = y = kx = ky = 0f;
            sx = sy = 1f;
            return false;
        }

        private void UpdateLayerVertices(Layer layer)
        {
            PlantAtlasPart part = layer.currentPart;
            if (part == null)
                return;

            float rx = layer.kx * Mathf.Deg2Rad;
            float ry = layer.ky * Mathf.Deg2Rad;

            float m00 = Mathf.Cos(rx) * layer.sx * globalScale;
            float m01 = -Mathf.Sin(ry) * layer.sy * globalScale;
            float m10 = Mathf.Sin(rx) * layer.sx * globalScale;
            float m11 = Mathf.Cos(ry) * layer.sy * globalScale;

            float tx = layer.x * positionScale * globalScale;
            float ty = layer.y * positionScale * globalScale;
            float width = part.width;
            float height = part.height;

            layer.vertices[0] = new Vector3(
                tx * plantScale.x / pixelsPerUnit,
                -ty * plantScale.y / pixelsPerUnit,
                0f);

            layer.vertices[1] = new Vector3(
                (tx + m00 * width) * plantScale.x / pixelsPerUnit,
                -(ty + m10 * width) * plantScale.y / pixelsPerUnit,
                0f);

            layer.vertices[2] = new Vector3(
                (tx + m00 * width + m01 * height) *
                    plantScale.x / pixelsPerUnit,
                -(ty + m10 * width + m11 * height) *
                    plantScale.y / pixelsPerUnit,
                0f);

            layer.vertices[3] = new Vector3(
                (tx + m01 * height) * plantScale.x / pixelsPerUnit,
                -(ty + m11 * height) * plantScale.y / pixelsPerUnit,
                0f);

            // Căn tự động sau khi đã biết điểm đáy ở pose đầu tiên.
            // Nếu tự căn tắt, chỉ áp dụng phần bù chỉnh tay.
            if (anchorResolved)
            {
                float offsetX =
                    automaticAnchorOffset.x + anchorAdjustment.x;
                float offsetY =
                    automaticAnchorOffset.y + anchorAdjustment.y;

                for (int i = 0; i < layer.vertices.Length; i++)
                {
                    layer.vertices[i].x -= offsetX;
                    layer.vertices[i].y -= offsetY;
                }
            }

            layer.mesh.vertices = layer.vertices;
            layer.mesh.RecalculateBounds();
        }

        private static void TranslateLayerVertices(
            Layer layer,
            float deltaX,
            float deltaY)
        {
            for (int i = 0; i < layer.vertices.Length; i++)
            {
                layer.vertices[i].x += deltaX;
                layer.vertices[i].y += deltaY;
            }

            layer.mesh.vertices = layer.vertices;
            layer.mesh.RecalculateBounds();
        }

        public void SetLayerTransform(
            string layerName,
            Vector3 localPosition,
            float kx,
            float ky,
            Vector3 localScale)
        {
            Layer layer;
            if (!layers.TryGetValue(layerName, out layer))
                return;

            layer.transform.localPosition = localPosition;
            layer.transform.localRotation =
                PlantReanimMath.GetRotation(kx, ky);
            layer.transform.localScale = localScale;
        }

        public void SetLayerPosition(
            string layerName,
            Vector3 localPosition)
        {
            Layer layer;
            if (layers.TryGetValue(layerName, out layer))
                layer.transform.localPosition = localPosition;
        }

        public void SetLayerScale(
            string layerName,
            Vector3 localScale)
        {
            Layer layer;
            if (layers.TryGetValue(layerName, out layer))
                layer.transform.localScale = localScale;
        }

        public void SetLayerRotation(
            string layerName,
            float kx,
            float ky)
        {
            Layer layer;
            if (layers.TryGetValue(layerName, out layer))
            {
                layer.transform.localRotation =
                    PlantReanimMath.GetRotation(kx, ky);
            }
        }

        public void SetLayerVisible(string layerName, bool visible)
        {
            Layer layer;
            if (layers.TryGetValue(layerName, out layer))
                layer.gameObject.SetActive(visible);
        }

        public void SetSortingOrder(
            string layerName,
            int sortingOrder)
        {
            Layer layer;
            if (layers.TryGetValue(layerName, out layer))
                layer.meshRenderer.sortingOrder = sortingOrder;
        }

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