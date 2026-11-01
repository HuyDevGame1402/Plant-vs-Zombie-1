using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum ZombiePartRole
{
    None = 0,

    Head = 1 << 0,
    DroppedArm = 1 << 1,
    UpperArm = 1 << 2,
    Body = 1 << 3,

    // Bộ phận được nâng lên trên đầu khi zombie ăn.
    RaiseWhenEating = 1 << 4,

    // Vật đang cầm (cờ, cán cờ, bàn tay cầm cờ...).
    // Rơi ra cùng cánh tay khi mất tay, hoặc rơi riêng khi zombie chết.
    HeldItem = 1 << 5,

    // Bộ phận bị thay thế khi đang cầm vật (bàn tay thường của zombie cầm cờ).
    // Bị ẩn lúc còn cầm vật, hiện lại khi vật rơi.
    HeldItemReplaces = 1 << 6
}

[CreateAssetMenu(
    fileName = "ZombieAnimationExtension",
    menuName = "PvZ/Zombie Animation Extension"
)]
public class ZombieAnimationExtension : ScriptableObject
{
    [Serializable]
    public class PartRule
    {
        [Tooltip("Tên track trong Reanim JSON.")]
        public string trackName;

        [Tooltip("Vai trò đặc biệt của bộ phận này.")]
        public ZombiePartRole roles;
    }

    [Serializable]
    public class AnimationRule
    {
        [Tooltip("Tên hành động do hệ thống sử dụng. Ví dụ: idle, walk, eat.")]
        public string actionName;

        [Tooltip("Tên clip thực tế trong Reanim JSON.")]
        public string clipName;
    }

    [Header("Special Parts")]

    [SerializeField]
    private List<PartRule> partRules = new List<PartRule>();

    [Header("Special Animations")]

    [SerializeField]
    private List<AnimationRule> animationRules =
        new List<AnimationRule>();

    public IReadOnlyList<PartRule> PartRules => partRules;

    public IReadOnlyList<AnimationRule> AnimationRules => animationRules;

    /// <summary>
    /// Lấy các vai trò đặc biệt của một track.
    /// </summary>
    public ZombiePartRole GetPartRoles(string trackName)
    {
        if (string.IsNullOrEmpty(trackName))
            return ZombiePartRole.None;

        ZombiePartRole result = ZombiePartRole.None;

        foreach (PartRule rule in partRules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.trackName))
                continue;

            if (string.Equals(
                rule.trackName,
                trackName,
                StringComparison.OrdinalIgnoreCase))
            {
                result |= rule.roles;
            }
        }

        return result;
    }

    /// <summary>
    /// Tìm tên clip tương ứng với một hành động.
    /// </summary>
    public bool TryGetAnimationClip(
        string actionName,
        out string clipName)
    {
        clipName = null;

        if (string.IsNullOrEmpty(actionName))
            return false;

        foreach (AnimationRule rule in animationRules)
        {
            if (rule == null ||
                string.IsNullOrEmpty(rule.actionName) ||
                string.IsNullOrEmpty(rule.clipName))
                continue;

            if (string.Equals(
                rule.actionName,
                actionName,
                StringComparison.OrdinalIgnoreCase))
            {
                clipName = rule.clipName;
                return true;
            }
        }

        return false;
    }
}