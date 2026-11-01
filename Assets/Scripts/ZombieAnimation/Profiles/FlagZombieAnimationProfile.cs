using UnityEngine;

/// <summary>
/// Profile của Flag Zombie.
/// Dùng giống ZombieAnimationProfile, thêm 1 ô cho file Reanim của lá cờ.
///
///  - Reanim Json (của lớp cha)  = file reanim của cả zombie (zombie.reanim...), giống Normal Zombie.
///  - Flag Reanim Json (ô mới)    = zombie_flagpole.reanim... (chỉ chứa cán cờ + lá cờ).
/// </summary>
[CreateAssetMenu(
    fileName = "FlagZombieAnimationProfile",
    menuName = "PvZ/Flag Zombie Animation Profile"
)]
public class FlagZombieAnimationProfile : ZombieAnimationProfile
{
    [Header("Flag")]

    [Tooltip("File Reanim JSON của cán cờ + lá cờ (zombie_flagpole.reanim.compiled).")]
    [SerializeField]
    private TextAsset flagReanimJson;

    public TextAsset FlagReanimJson => flagReanimJson;

    /// <summary>
    /// Lấy ReanimData của lá cờ (dùng chung Atlas JSON với zombie).
    /// </summary>
    public bool TryGetFlagData(out ReanimData data)
    {
        data = null;

        if (flagReanimJson == null)
        {
            Debug.LogError(
                $"[{name}] Flag Reanim Json chưa được gán!",
                this
            );

            return false;
        }

        return ReanimDataCache.GetOrLoad(
            AtlasJson,
            flagReanimJson,
            out data
        );
    }
}