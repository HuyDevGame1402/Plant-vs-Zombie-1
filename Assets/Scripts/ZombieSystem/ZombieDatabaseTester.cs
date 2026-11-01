
using UnityEngine;

public class ZombieDatabaseTester : MonoBehaviour
{
    [SerializeField]
    private ZombieDatabase zombieDatabase;

    private void Start()
    {
        ZombieDefinition normal =
            zombieDatabase.GetZombie(ZombieType.Normal);

        if (normal != null)
        {
            Debug.Log(
                $"Zombie: {normal.zombieType}, " +
                $"Value: {normal.zombieValue}, " +
                $"Weight: {normal.pickWeight}, " +
                $"Starting Level: {normal.startingLevel}"
            );
        }
    }
}
