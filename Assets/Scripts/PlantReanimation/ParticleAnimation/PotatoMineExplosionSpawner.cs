using UnityEngine;

namespace PvzFx
{
    /// <summary>
    /// Gắn script này vào 1 GameObject bất kỳ. Gọi Explode(vị trí) khi Potato Mine nổ.
    /// </summary>
    public class PotatoMineExplosionSpawner : MonoBehaviour
    {
        [Header("Kéo file vào đây")]
        public Texture2D atlasTexture;      // LOADINGIMAGES_PARTICLES_960_00.PNG
        public TextAsset atlasJson;         // LoadingImages_Particles_960.json

        [Header("Cài đặt")]
        public float pixelsPerUnit = 100f;  // PPU dùng khi cắt sprite
        public float effectScale = 1f;      // phóng to/thu nhỏ cả hiệu ứng cho khớp với game của bạn
        public bool includeBlastMark = false;
        public string sortingLayer = "Default";
        public int sortingOrder = 50;
        [Tooltip("Để trống = tự tìm shader 'Legacy Shaders/Particles/Additive'")]
        public Material additiveMaterial;

        PvzAtlas _atlas;
        System.Collections.Generic.List<EmitterDef> _defs;

        void Awake()
        {
            _atlas = new PvzAtlas(atlasTexture, atlasJson, pixelsPerUnit);
            _defs = PotatoMineFx.Create(includeBlastMark);

            if (additiveMaterial == null)
            {
                Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
                if (sh != null) additiveMaterial = new Material(sh);
                else Debug.LogWarning("Không tìm thấy shader Additive, hãy gán additiveMaterial thủ công.");
            }
        }

        /// <summary>Gọi hàm này khi Potato Mine nổ. worldPos = tâm vụ nổ (mặt đất, chỗ củ khoai).</summary>
        public void Explode(Vector3 worldPos)
        {
            PvzParticleEffect.Play(_defs, _atlas, worldPos, effectScale, additiveMaterial, sortingOrder, sortingLayer);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        // Test nhanh: nhấn Space để nổ tại gốc toạ độ
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Explode(transform.position);
        }
#endif
    }
}