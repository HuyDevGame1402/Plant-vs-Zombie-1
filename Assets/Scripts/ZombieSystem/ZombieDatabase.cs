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

    public ZombieDefinition GetZombie(ZombieType type)
    {
        if (lookup == null)
            BuildLookup();

        if (lookup != null && lookup.TryGetValue(type, out ZombieDefinition definition))
            return definition;

        Debug.LogError($"Zombie not found: {type}", this);
        return null;
    }
}
