using UnityEngine;

namespace PvzFx
{
    /// <summary>
    /// Gắn script này vào 1 GameObject bất kỳ. Gọi Explode(vị trí) khi Cherry Bomb nổ.
    /// (Dùng chung PvzAtlas.cs và PvzParticleSystem.cs với Potato Mine)
    /// </summary>
    public class CherryBombExplosionSpawner : MonoBehaviour
    {
        [Header("Kéo file vào đây")]
        public Texture2D atlasTexture;      // LOADINGIMAGES_PARTICLES_960_00.PNG
        public TextAsset atlasJson;         // LoadingImages_Particles_960.json

        [Header("Cài đặt")]
        public float pixelsPerUnit = 100f;
        public float effectScale = 1f;
        public string sortingLayer = "Default";
        public int sortingOrder = 50;
        [Tooltip("Để trống = tự tìm shader 'Legacy Shaders/Particles/Additive'")]
        public Material additiveMaterial;

        PvzAtlas _atlas;
        System.Collections.Generic.List<EmitterDef> _defs;

        void Awake()
        {
            _atlas = new PvzAtlas(atlasTexture, atlasJson, pixelsPerUnit);
            _defs = CherryBombFx.Create();

            if (additiveMaterial == null)
            {
                Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
                if (sh != null) additiveMaterial = new Material(sh);
                else Debug.LogWarning("Không tìm thấy shader Additive, hãy gán additiveMaterial thủ công.");
            }
        }

        /// <summary>Gọi khi Cherry Bomb nổ. worldPos = tâm vụ nổ.</summary>
        public void Explode(Vector3 worldPos)
        {
            PvzParticleEffect.Play(_defs, _atlas, worldPos, effectScale, additiveMaterial, sortingOrder, sortingLayer);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        // Test nhanh: nhấn Space để nổ tại vị trí GameObject
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Explode(transform.position);
        }
#endif
    }
}