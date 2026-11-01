using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flag Zombie = zombie thường + tay cong cầm cờ + cán cờ/lá cờ (animation riêng).
///
///  - Cầm cờ: hiện Zombie_innerarm_screendoor (tay áo nâu cong) + Zombie_flaghand + cờ,
///    ẩn tay trong thẳng (anim_innerarm1/2/3).
///  - Mất tay ngoài (LoseArm): chỉ tay ngoài rụng, tay cầm cờ ở lại, lá cờ chuyển sang cờ rách.
///  - Chết: chỉ cờ (cán + vải) rời ra; tay cong và bàn tay cầm cờ bị ẩn.
///  - Chết hiếm: dùng clip superlongdeath sau khi đầu và cờ bật ra.
/// </summary>
public class FlagZombieReanim : BaseZombieReanim
{
    [Header("Flag")]

    [Tooltip("Track của zombie mà cán cờ bám theo.")]
    [SerializeField] private string flagAttachTrack = "Zombie_flaghand";

    [Tooltip("Clip của file lá cờ. File này không có clip riêng nên dùng clip 'default' (chạy hết các frame).")]
    [SerializeField] private string flagClip = ReanimDataParser.DefaultClipName;

    [Tooltip("Track lá cờ (vải) trong file lá cờ. Sẽ đổi sang ảnh cờ rách khi mất tay.")]
    [SerializeField] private string flagClothTrack = "Zombie_flag";

    [Tooltip("Ảnh lá cờ rách.")]
    [SerializeField] private string tatteredFlagImageId = "IMAGE_REANIM_ZOMBIE_FLAG3";

    [Header("Cánh tay cầm cờ")]

    [Tooltip("Tay áo nâu cong (chỉ hiện khi cầm cờ).")]
    [SerializeField] private string bentArmTrack = "Zombie_innerarm_screendoor";
    [SerializeField] private string bentArmHandTrack = "Zombie_innerarm_screendoor_hand";

    [SerializeField] private string armUpperTrack = "anim_innerarm1";
    [SerializeField] private string armLowerTrack = "anim_innerarm2";
    [SerializeField] private string armHandTrack = "anim_innerarm3";

    [Header("Chết hiếm")]
    [Tooltip("Clip dài trong zombie.reanim.compiled.reanim.json.")]
    [SerializeField] private string rareDeathClip = "superlongdeath";
    [Range(0f, 1f)]
    [SerializeField] private float rareHeadlessDeathChance = 0.05f;
    [SerializeField] private string flagPoleTrack = "Zombie_flagpole";

    private static readonly HashSet<string> flagZombieImageIds =
        new HashSet<string>(CommonZombieImageIds, StringComparer.OrdinalIgnoreCase)
    {
        "IMAGE_REANIM_ZOMBIE_FLAGHAND",
        "IMAGE_REANIM_ZOMBIE_FLAGPOLE",
        "IMAGE_REANIM_ZOMBIE_FLAG1",
        "IMAGE_REANIM_ZOMBIE_FLAG3",
        "IMAGE_REANIM_ZOMBIE_INNERARM_SCREENDOOR"
    };

    private ReanimAnimationPlayer flagPlayer;

    protected override HashSet<string> AllowedImageIds
    {
        get { return flagZombieImageIds; }
    }

    protected override ZombiePartRole GetDefaultRoles(string trackName)
    {
        switch (trackName.ToLowerInvariant())
        {
            // Không cho tay cong / bàn tay cờ vào HeldItem: khi chết chỉ
            // hai track overlay của lá cờ được tạo debris.
            case "zombie_innerarm_screendoor":
            case "zombie_flaghand":
                return ZombiePartRole.None;

            case "zombie_flagpole":
            case "zombie_flag":
                return ZombiePartRole.None;

            // Tay trong thẳng: ẩn khi còn cầm cờ.
            case "anim_innerarm1":
            case "anim_innerarm2":
            case "anim_innerarm3":
                return ZombiePartRole.HeldItemReplaces;

            default:
                return base.GetDefaultRoles(trackName);
        }
    }

    protected override void OnRendererReady()
    {
        FlagZombieAnimationProfile flagProfile =
            Profile as FlagZombieAnimationProfile;

        if (flagProfile == null)
        {
            Debug.LogError(
                "FlagZombieReanim: Profile phải là FlagZombieAnimationProfile.",
                this);
            return;
        }

        ReanimData flagData;

        if (!flagProfile.TryGetFlagData(out flagData))
        {
            Debug.LogError(
                "FlagZombieReanim: Không thể load dữ liệu lá cờ.",
                this);
            return;
        }

        flagPlayer = new ReanimAnimationPlayer();
        flagPlayer.BlendDuration = 0f;

        if (!flagPlayer.Initialize(flagData) ||
            !flagPlayer.Play(flagClip, true))
        {
            Debug.LogError(
                "FlagZombieReanim: Không phát được animation lá cờ (clip '" +
                flagClip + "').",
                this);
            flagPlayer = null;
            return;
        }

        // Toạ độ cán cờ được thiết kế ở tư thế idle frame đầu của zombie.
        if (!PartRenderer.AddOverlay(
            flagData,
            flagPlayer,
            flagAttachTrack,
            ReferenceFrame))
        {
            Debug.LogError(
                "FlagZombieReanim: Không gắn được lá cờ vào track '" +
                flagAttachTrack + "'.",
                this);
            flagPlayer = null;
            return;
        }

        AddTickPlayer(flagPlayer);
    }

    // =========================================================
    // MẤT TAY: lá cờ hỏng đi, tay cầm cờ ở lại
    // =========================================================

    protected override void OnArmLostHook()
    {
        if (!PartRenderer.SetPartImageOverride(flagClothTrack, tatteredFlagImageId))
        {
            Debug.LogWarning(
                "FlagZombieReanim: Không đổi được lá cờ rách. Kiểm tra 'Flag Cloth Track' ('" +
                flagClothTrack + "') có đúng tên track lá cờ trong file lá cờ không, " +
                "và ảnh '" + tatteredFlagImageId + "' có trong atlas không.",
                this);
        }
    }

    // =========================================================
    // CHẾT
    // =========================================================

    protected override void HandleHeldItemsOnDeath(bool headless)
    {
        DropFlagOnly();
    }

    /// <summary>Headless death hiếm: đầu và cờ bật ra, sau đó phát superlongdeath.</summary>
    public override void DieHeadless()
    {
        if (!IsReady || IsDeadOrDying)
            return;

        if (rareDeathChanceRoll() && StartRareHeadlessDeath())
            return;

        base.DieHeadless();
    }

    /// <summary>Gọi kiểu chết mất đầu thường, bỏ qua tỉ lệ animation hiếm.</summary>
    public void DieHeadlessStandard()
    {
        if (!IsReady || IsDeadOrDying)
            return;

        base.DieHeadless();
    }

    /// <summary>Dùng để xem trực tiếp kiểu chết hiếm trong lúc chỉnh scene.</summary>
    public void DieHeadlessRare()
    {
        if (!IsReady || IsDeadOrDying)
            return;

        if (!StartRareHeadlessDeath())
            base.DieHeadless();
    }

    private bool StartRareHeadlessDeath()
    {
        if (Data == null || !Data.Clips.ContainsKey(rareDeathClip))
            return false;

        DetachHead();
        DropFlagOnly();

        // Bỏ tay cong và bàn tay cờ. Hiện tay trong thẳng để clip
        // superlongdeath tự diễn đoạn loạng choạng/giơ tay.
        PartRenderer.SetPartHidden(bentArmTrack, true);
        PartRenderer.SetPartHidden(bentArmHandTrack, true);
        PartRenderer.SetPartHidden(flagAttachTrack, true);
        PartRenderer.SetPartHidden(armUpperTrack, false);
        PartRenderer.SetPartHidden(armLowerTrack, false);
        PartRenderer.SetPartHidden(armHandTrack, false);

        return PlayCustomDeathAnimation(rareDeathClip);
    }

    private bool rareDeathChanceRoll()
    {
        return UnityEngine.Random.value < Mathf.Clamp01(rareHeadlessDeathChance);
    }

    private void DropFlagOnly()
    {
        List<string> flagParts = new List<string>
        {
            flagPoleTrack,
            flagClothTrack
        };

        SpawnDebris(
            flagParts,
            new Vector3(
                -UnityEngine.Random.Range(0.6f, 1.3f),
                UnityEngine.Random.Range(3.0f, 4.0f),
                0f),
            UnityEngine.Random.Range(-420f, 420f),
            1.3f,
            0.5f);

        // Các track này không được đưa vào debris, vì vậy tay không thể bay theo.
        PartRenderer.SetPartHidden(flagPoleTrack, true);
        PartRenderer.SetPartHidden(flagClothTrack, true);
        PartRenderer.SetPartHidden(bentArmTrack, true);
        PartRenderer.SetPartHidden(bentArmHandTrack, true);
        PartRenderer.SetPartHidden(flagAttachTrack, true);

        // Tay thẳng thay thế bàn tay cầm cờ trong các kiểu chết.
        PartRenderer.SetPartHidden(armUpperTrack, false);
        PartRenderer.SetPartHidden(armLowerTrack, false);
        PartRenderer.SetPartHidden(armHandTrack, false);
    }

}
