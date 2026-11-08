using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health component shared by all Plant MonoBehaviours.
/// Add it to the same GameObject as a Plant component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Plant))]
public sealed class PlantHealth : MonoBehaviour
{
    [Serializable]
    public sealed class HealthChangedEvent : UnityEvent<int, int> { }

    [Header("Runtime health (read only)")]
    [SerializeField] private int currentHealth;
    [SerializeField] private int maxHealth;
    [SerializeField] private bool isDead;

    [Header("Events")]
    [Tooltip("Invoked once when health reaches zero. Configure callbacks here in the Inspector.")]
    public UnityEvent onDeath = new UnityEvent();

    [Tooltip("Invoked after health changes. Arguments: current health, maximum health.")]
    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();

    /// <summary>C# event alternative for listeners registered from code.</summary>
    public event Action<PlantHealth> Died;

    /// <summary>C# event alternative; arguments are current health and maximum health.</summary>
    public event Action<int, int> HealthChanged;

    public int CurrentHealth { get { EnsureInitialized(); return currentHealth; } }
    public int MaxHealth { get { EnsureInitialized(); return maxHealth; } }
    public bool IsDead { get { EnsureInitialized(); return isDead; } }
    public float HealthNormalized { get { EnsureInitialized(); return maxHealth <= 0 ? 0f : (float)currentHealth / maxHealth; } }

    private bool initialized;
    private Plant plant;

    private void Awake()
    {
        plant = GetComponent<Plant>();
    }

    private void Start()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Initializes from PlantData. Safe to call again; it won't reset health after initialization.
    /// </summary>
    public void Initialize()
    {
        if (initialized) return;
        if (plant == null) plant = GetComponent<Plant>();
        if (plant == null || plant.plantData == null)
        {
            Debug.LogError("PlantHealth requires a Plant component with PlantData assigned.", this);
            maxHealth = 1;
            currentHealth = 1;
            initialized = true;
            return;
        }

        maxHealth = Mathf.Max(1, plant.plantData.maxHpAtPlanting);
        currentHealth = Mathf.Clamp(plant.plantData.initialHp, 0, maxHealth);
        isDead = currentHealth <= 0;
        initialized = true;

        if (isDead) Die();
        else NotifyHealthChanged();
    }

    /// <summary>Apply non-negative damage. Damage after death is ignored.</summary>
    public void TakeDamage(int damage)
    {
        EnsureInitialized();
        if (isDead || damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        NotifyHealthChanged();
        if (currentHealth == 0) Die();
    }

    /// <summary>Restore health up to the maximum. Healing a dead plant is ignored.</summary>
    public void Heal(int amount)
    {
        EnsureInitialized();
        if (isDead || amount <= 0) return;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (currentHealth != previousHealth) NotifyHealthChanged();
    }

    /// <summary>Set health directly, clamped to [0, MaxHealth]. Setting zero kills the plant.</summary>
    public void SetHealth(int value)
    {
        EnsureInitialized();
        if (isDead) return;

        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        NotifyHealthChanged();
        if (currentHealth == 0) Die();
    }

    private void EnsureInitialized()
    {
        if (!initialized) Initialize();
    }

    private void NotifyHealthChanged()
    {
        onHealthChanged.Invoke(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead && currentHealth > 0) return;
        isDead = true;
        currentHealth = 0;
        onDeath.Invoke();
        Died?.Invoke(this);
    }

    public bool IsAlive()
    {
        EnsureInitialized();
        return !isDead && currentHealth > 0;
    }
}
