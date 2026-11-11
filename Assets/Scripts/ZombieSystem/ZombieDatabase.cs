using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ZombieDatabase",
    menuName = "PvZ/Campaign/Zombie Database"
)]
public class ZombieDatabase : ScriptableObject
{
    [Header("Zombie Definitions")]
    [SerializeField]
    private List<ZombieDefinition> zombies = new();

    private Dictionary<ZombieType, ZombieDefinition> lookup;

    public IReadOnlyList<ZombieDefinition> Zombies => zombies;

    private void OnEnable() => BuildLookup();
    private void OnValidate() => BuildLookup();

    private void BuildLookup()
    {
        lookup = new Dictionary<ZombieType, ZombieDefinition>();
        if (zombies == null)
            return;

        foreach (ZombieDefinition zombie in zombies)
        {
            if (zombie == null)
                continue;

            if (lookup.ContainsKey(zombie.zombieType))
            {
                Debug.LogWarning($"Duplicate ZombieType: {zombie.zombieType}", this);
                continue;
            }

            lookup.Add(zombie.zombieType, zombie);
        }
    }

    /// <summary>Không log lỗi khi thiếu — dùng cho thuật toán wave.</summary>
    public bool TryGetZombie(ZombieType type, out ZombieDefinition definition)
    {
        if (lookup == null)
            BuildLookup();

        return lookup.TryGetValue(type, out definition) && definition != null;
    }

    public ZombieDefinition GetZombie(ZombieType type)
    {
        if (TryGetZombie(type, out ZombieDefinition definition))
            return definition;

        Debug.LogError($"Zombie not found: {type}", this);
        return null;
    }
}