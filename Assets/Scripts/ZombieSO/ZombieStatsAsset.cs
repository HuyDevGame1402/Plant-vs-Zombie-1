using System;
using UnityEngine;

namespace PvZ.Data
{
    /// <summary>Runtime gameplay data for one zombie type.</summary>
    [CreateAssetMenu(menuName = "PvZ/Zombie Stats", fileName = "ZombieStats")]
    public sealed class ZombieStatsAsset : ScriptableObject
    {
        [Header("Identity")]
        public int id;
        public string enumName;
        public string displayName;

        [Header("Health and armor")]
        public int bodyHealth;
        public ZombieArmor[] armor = Array.Empty<ZombieArmor>();
        public ZombieHealthModifier[] healthModifiers = Array.Empty<ZombieHealthModifier>();

        [Header("Attacks")]
        public ZombieAttack[] attacks = Array.Empty<ZombieAttack>();

        [Header("Movement")]
        public ZombieMovementState[] movementStates = Array.Empty<ZombieMovementState>();
        [Tooltip("Multiplier applied to movement speed while chilled, when that state supports it.")]
        public float chilledSpeedMultiplier = 1f;

        [Header("Damage state thresholds")]
        public bool canLoseBodyParts;
        [Range(0f, 1f)] public float armDropHealthRatio = 0.6667f;
        [Range(0f, 1f)] public float headDropHealthRatio = 0.3333f;
    }

    [Serializable]
    public sealed class ZombieArmor
    {
        public string slot;
        public string displayName;
        public int health;
        [Range(0f, 1f)] public float firstDamageStageHealthRatio = 2f / 3f;
        [Range(0f, 1f)] public float secondDamageStageHealthRatio = 1f / 3f;
        public bool dropsAtZeroHealth = true;
    }

    [Serializable]
    public sealed class ZombieHealthModifier
    {
        [TextArea(1, 3)] public string condition;
        [Tooltip("Multiplier from the source data. A zero value means the source describes a special integer-divide rule; inspect the condition before applying.")]
        public float multiplier = 1f;
        public int divisor = 1;
    }

    [Serializable]
    public sealed class ZombieAttack
    {
        public string target;
        public string attackType;
        public float damage;
        public bool hasNumericDamage;
        public string damageUnit;
        public int intervalTicks;
        public bool hasInterval;
        public int chilledIntervalTicks;
        public string condition;
    }

    [Serializable]
    public sealed class ZombieMovementState
    {
        public string state;
        public float minSpeed;
        public float maxSpeed;
        public float fixedSpeed;
        public bool hasSpeedRange;
        public bool hasFixedSpeed;
        public string speedUnit;
    }
}
