
using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// Chứa cấu hình animation và rendering
/// dành riêng cho từng loại Zombie.
///
/// Mỗi loại Zombie có thể sử dụng một Profile riêng,
/// nhưng vẫn dùng chung hệ thống Parser, Cache,
/// AnimationPlayer và PartRenderer.
/// </summary>
[CreateAssetMenu(
    fileName = "ZombieAnimationProfile",
    menuName = "PvZ/Zombie Animation Profile"
)]
public class ZombieAnimationProfile : ScriptableObject
{
    [Header("Reanim Assets")]

    [Tooltip("File JSON chứa thông tin Atlas.")]
    [SerializeField]
    private TextAsset atlasJson;

    [Tooltip("Ảnh PNG chứa texture Atlas.")]
    [SerializeField]
    private Texture2D atlasTexture;

    [Tooltip("File JSON chứa dữ liệu animation Reanim.")]
    [SerializeField]
    private TextAsset reanimJson;


    [Header("Rendering")]

    [Tooltip("Material sử dụng để render các bộ phận Zombie.")]
    [SerializeField]
    private Material sharedMaterial;

    [Tooltip("Số pixel tương ứng với một Unity Unit.")]
    [SerializeField]
    private float pixelPerUnit = 100f;

    [Tooltip("Tỷ lệ scale tổng thể của Zombie.")]
    [SerializeField]
    private Vector2 globalScale = Vector2.one;

    [Tooltip("Có căn chỉnh vị trí Zombie theo chân hay không.")]
    [SerializeField]
    private bool anchorToFeet = true;

    [Tooltip("Offset điều chỉnh vị trí pivot.")]
    [SerializeField]
    private Vector2 pivotOffset = Vector2.zero;


    [Header("Shadow")]

    [Tooltip("ID hình ảnh bóng trong Atlas.")]
    [SerializeField]
    private string shadowImageId;

    [Tooltip("Offset vị trí bóng.")]
    [SerializeField]
    private Vector2 shadowOffset = Vector2.zero;

    [Header("Extensions")]

    [SerializeField]
    private List<ZombieAnimationExtension> extensions =
    new List<ZombieAnimationExtension>();


    // =====================================================
    // PUBLIC GETTERS
    // =====================================================

    public TextAsset AtlasJson => atlasJson;

    public Texture2D AtlasTexture => atlasTexture;

    public TextAsset ReanimJson => reanimJson;

    public Material SharedMaterial => sharedMaterial;

    public float PixelPerUnit => pixelPerUnit;

    public Vector2 GlobalScale => globalScale;

    public bool AnchorToFeet => anchorToFeet;

    public Vector2 PivotOffset => pivotOffset;

    public string ShadowImageId => shadowImageId;

    public Vector2 ShadowOffset => shadowOffset;


    // =====================================================
    // LOAD REANIM DATA
    // =====================================================

    /// <summary>
    /// Lấy ReanimData thông qua ReanimDataCache.
    /// Nếu chưa có dữ liệu trong Cache thì tự động Parse.
    /// </summary>
    public bool TryGetData(out ReanimData data)
    {
        data = null;

        // Kiểm tra Atlas JSON
        if (atlasJson == null)
        {
            Debug.LogError(
                $"[{name}] Atlas JSON chưa được gán!",
                this
            );

            return false;
        }

        // Kiểm tra Atlas PNG
        if (atlasTexture == null)
        {
            Debug.LogError(
                $"[{name}] Atlas PNG chưa được gán!",
                this
            );

            return false;
        }

        // Kiểm tra Reanim JSON
        if (reanimJson == null)
        {
            Debug.LogError(
                $"[{name}] Reanim JSON chưa được gán!",
                this
            );

            return false;
        }

        // Kiểm tra Material
        if (sharedMaterial == null)
        {
            Debug.LogError(
                $"[{name}] Shared Material chưa được gán!",
                this
            );

            return false;
        }

        // Kiểm tra Pixel Per Unit
        if (pixelPerUnit <= 0f)
        {
            Debug.LogError(
                $"[{name}] Pixel Per Unit phải lớn hơn 0!",
                this
            );

            return false;
        }

        // Parse hoặc lấy dữ liệu đã có trong Cache
        return ReanimDataCache.GetOrLoad(
            atlasJson,
            reanimJson,
            out data
        );
    }

    /// <summary>
    /// Tổng hợp vai trò của một bộ phận từ tất cả Extension.
    /// </summary>
    public ZombiePartRole GetPartRoles(string trackName)
    {
        ZombiePartRole result = ZombiePartRole.None;

        foreach (ZombieAnimationExtension extension in extensions)
        {
            if (extension == null)
                continue;

            result |= extension.GetPartRoles(trackName);
        }

        return result;
    }

    /// <summary>
    /// Tìm clip theo hành động.
    /// Extension nằm sau trong danh sách có độ ưu tiên cao hơn.
    /// </summary>
    public bool TryGetAnimationClip(
        string actionName,
        out string clipName)
    {
        clipName = null;

        for (int i = extensions.Count - 1; i >= 0; i--)
        {
            ZombieAnimationExtension extension = extensions[i];

            if (extension == null)
                continue;

            if (extension.TryGetAnimationClip(actionName, out clipName))
                return true;
        }

        return false;
    }
}
