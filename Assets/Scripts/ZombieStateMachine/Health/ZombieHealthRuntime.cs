using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace PvZ.Data
{
    /// <summary>
    /// Runtime health handler for one zombie instance. Put this component on the zombie GameObject
    /// and assign its ZombieStatsAsset. Damage is routed through armor first, then body health.
    /// </summary>
    public sealed class ZombieHealthRuntime : MonoBehaviour
    {
        [Header("Zombie data")]
        [SerializeField] private ZombieStatsAsset stats;

        [Header("Armor events: argument is armor array index")]
        [SerializeField] private UnityEvent<int> onArmorFirstDamageStage = new UnityEvent<int>();
        [SerializeField] private UnityEvent<int> onArmorSecondDamageStage = new UnityEvent<int>();
        [SerializeField] private UnityEvent<int> onArmorBroken = new UnityEvent<int>();

        [Header("Body events")]
        [SerializeField] private UnityEvent onArmDropped = new UnityEvent();
        [SerializeField] private UnityEvent onHeadDropped = new UnityEvent();
        [SerializeField] private UnityEvent onDeath = new UnityEvent();

        [SerializeField] private int currentBodyHealth;
        [SerializeField] private int[] currentArmorHealth = Array.Empty<int>();

        private bool armDropped;
        private bool headDropped;
        private bool dead;
        private bool[] armorStage1Triggered = Array.Empty<bool>();
        private bool[] armorStage2Triggered = Array.Empty<bool>();
        private bool[] armorBrokenTriggered = Array.Empty<bool>();

        public ZombieStatsAsset Stats => stats;
        public int CurrentBodyHealth => currentBodyHealth;
        public int MaxBodyHealth => stats != null ? stats.bodyHealth : 0;
        public bool IsDead => dead;
        public int ArmorCount => currentArmorHealth.Length;

        private void Awake() => Initialize();

        /// <summary>Sets this instance back to the health values stored in its stats asset.</summary>
        public void Initialize()
        {
            if (stats == null)
            {
                stats = transform.GetComponent<CampaignZombie>().ZombieDatabase;
            }

            currentBodyHealth = Mathf.Max(0, stats.bodyHealth);
            int count = stats.armor != null ? stats.armor.Length : 0;
            currentArmorHealth = new int[count];
            armorStage1Triggered = new bool[count];
            armorStage2Triggered = new bool[count];
            armorBrokenTriggered = new bool[count];

            for (int i = 0; i < count; i++)
                currentArmorHealth[i] = Mathf.Max(0, stats.armor[i] != null ? stats.armor[i].health : 0);

            armDropped = false;
            headDropped = false;
            dead = currentBodyHealth <= 0;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                ApplyDamage(20);

                Debug.Log($"Zombie HP: {currentBodyHealth}/{MaxBodyHealth}");
            }
        }

        /// <summary>
        /// Applies non-negative damage. Returns the amount that reached body health.
        /// Armor absorbs damage in array order; any overflow continues into the body in this hit.
        /// </summary>
        public int ApplyDamage(int damage)
        {
            if (stats == null || dead || damage <= 0) return 0;

            int remainingDamage = damage;
            for (int i = 0; i < currentArmorHealth.Length && remainingDamage > 0; i++)
            {
                if (currentArmorHealth[i] <= 0) continue;

                int armorBefore = currentArmorHealth[i];
                int absorbed = Mathf.Min(armorBefore, remainingDamage);
                currentArmorHealth[i] -= absorbed;
                remainingDamage -= absorbed;
                CheckArmorTransitions(i, armorBefore, currentArmorHealth[i]);
            }

            int bodyDamage = Mathf.Min(currentBodyHealth, remainingDamage);
            int bodyBefore = currentBodyHealth;
            currentBodyHealth -= bodyDamage;
            CheckBodyTransitions(bodyBefore, currentBodyHealth);

            if (currentBodyHealth <= 0 && !dead)
            {
                currentBodyHealth = 0;
                dead = true;
                onDeath?.Invoke();
            }

            return bodyDamage;
        }

        /// <summary>Returns current health for the armor slot at the given index.</summary>
        public int GetArmorHealth(int armorIndex)
        {
            if (armorIndex < 0 || armorIndex >= currentArmorHealth.Length) return 0;
            return currentArmorHealth[armorIndex];
        }

        private void CheckArmorTransitions(int index, int before, int after)
        {
            ZombieArmor armor = stats.armor[index];
            if (armor == null || armor.health <= 0) return;

            float beforeRatio = (float)before / armor.health;
            float afterRatio = (float)after / armor.health;

            if (!armorStage1Triggered[index] && beforeRatio >= armor.firstDamageStageHealthRatio && afterRatio < armor.firstDamageStageHealthRatio)
            {
                armorStage1Triggered[index] = true;
                onArmorFirstDamageStage?.Invoke(index);
            }

            if (!armorStage2Triggered[index] && beforeRatio >= armor.secondDamageStageHealthRatio && afterRatio < armor.secondDamageStageHealthRatio)
            {
                armorStage2Triggered[index] = true;
                onArmorSecondDamageStage?.Invoke(index);
            }

            if (after <= 0 && !armorBrokenTriggered[index])
            {
                armorBrokenTriggered[index] = true;
                onArmorBroken?.Invoke(index);
            }
        }

        private void CheckBodyTransitions(int before, int after)
        {
            if (stats.bodyHealth <= 0 || after >= before) return;
            float beforeRatio = (float)before / stats.bodyHealth;
            float afterRatio = (float)after / stats.bodyHealth;

            if (stats.canLoseBodyParts && !armDropped &&
                beforeRatio >= stats.armDropHealthRatio && afterRatio < stats.armDropHealthRatio)
            {
                armDropped = true;
                onArmDropped?.Invoke();
            }

            // Head-drop animation/state is a separate event from HP reaching zero.
            // The death event fires only when body health actually reaches zero.
            if (stats.canLoseBodyParts && !headDropped &&
                beforeRatio >= stats.headDropHealthRatio && afterRatio < stats.headDropHealthRatio)
            {
                headDropped = true;
                onHeadDropped?.Invoke();
            }
        }

        public bool HasHead()
        {
            return currentBodyHealth > stats.bodyHealth * stats.headDropHealthRatio;
        }
    }
}
