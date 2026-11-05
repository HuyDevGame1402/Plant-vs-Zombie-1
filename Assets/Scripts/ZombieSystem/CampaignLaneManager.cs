using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores active zombies globally and by lane, and chooses lane spawn positions.</summary>
public class CampaignLaneManager : MonoBehaviour
{
    [Serializable]
    public class LaneSpawnPoint
    {
        [Tooltip("Lane index: 0 is top, 4 is bottom.")]
        public Transform spawnPoint;

        [Tooltip("Maximum world-space X offset from this lane's spawn point.")]
        [Min(0f)] public float horizontalOffset = 0.5f;
    }

    [SerializeField] private LaneSpawnPoint[] lanes = new LaneSpawnPoint[5];

    private readonly List<CampaignZombie>[] zombiesByLane = new List<CampaignZombie>[5];
    private readonly List<CampaignZombie> allZombies = new();

    private void Awake()
    {
        for (int i = 0; i < zombiesByLane.Length; i++)
            zombiesByLane[i] = new List<CampaignZombie>();
    }

    public bool TryPickSpawn(int globalLevel, System.Random random, out int laneIndex, out Vector3 spawnPosition)
    {
        laneIndex = -1;
        spawnPosition = default;

        if (lanes == null || lanes.Length != 5)
        {
            Debug.LogError("CampaignLaneManager needs exactly 5 lane entries (indices 0 through 4).", this);
            return false;
        }

        if (globalLevel < 1 || globalLevel > 50)
        {
            Debug.LogError($"Invalid global campaign level: {globalLevel}.", this);
            return false;
        }

        var candidates = new List<int>(5);
        for (int i = 0; i < lanes.Length; i++)
        {
            if (IsLaneAllowed(globalLevel, i) && lanes[i] != null && lanes[i].spawnPoint != null)
                candidates.Add(i);
        }

        if (candidates.Count == 0)
        {
            Debug.LogError($"No assigned spawn points for an allowed lane at campaign level {globalLevel}.", this);
            return false;
        }

        random ??= new System.Random();
        laneIndex = candidates[random.Next(candidates.Count)];

        LaneSpawnPoint lane = lanes[laneIndex];
        float xOffset = ((float)random.NextDouble() * 2f - 1f) * lane.horizontalOffset;
        spawnPosition = lane.spawnPoint.position + Vector3.right * xOffset;
        return true;
    }

    public IReadOnlyList<CampaignZombie> GetZombiesInLane(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
            return Array.Empty<CampaignZombie>();

        RemoveDestroyed(zombiesByLane[laneIndex]);
        return zombiesByLane[laneIndex];
    }

    public IReadOnlyList<CampaignZombie> GetAllZombies()
    {
        RemoveDestroyed(allZombies);
        return allZombies;
    }

    public bool HasZombieInLane(int laneIndex)
        => GetZombiesInLane(laneIndex).Count > 0;

    public int GetZombieCountInLane(int laneIndex)
        => GetZombiesInLane(laneIndex).Count;

    public int GetTotalZombieCount()
        => GetAllZombies().Count;

    public void RegisterZombie(CampaignZombie zombie)
    {
        if (zombie == null)
            return;

        int laneIndex = zombie.LaneIndex;
        if (!IsValidLane(laneIndex))
        {
            Debug.LogError($"Cannot register zombie with invalid lane index {laneIndex}.", zombie);
            return;
        }

        EnsureListsInitialized();
        RemoveFromLists(zombie);
        zombiesByLane[laneIndex].Add(zombie);
        allZombies.Add(zombie);
    }

    public void UnregisterZombie(CampaignZombie zombie)
    {
        if (zombie == null)
            return;

        EnsureListsInitialized();
        RemoveFromLists(zombie);
    }

    public static bool IsLaneAllowed(int globalLevel, int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= 5 || globalLevel < 1 || globalLevel > 50)
            return false;

        if (globalLevel == 1)
            return laneIndex == 2;

        if (globalLevel == 2 || globalLevel == 3)
            return laneIndex >= 1 && laneIndex <= 3;

        return true;
    }

    private bool IsValidLane(int laneIndex)
        => laneIndex >= 0 && laneIndex < zombiesByLane.Length;

    private void EnsureListsInitialized()
    {
        for (int i = 0; i < zombiesByLane.Length; i++)
        {
            if (zombiesByLane[i] == null)
                zombiesByLane[i] = new List<CampaignZombie>();
        }
    }

    private void RemoveFromLists(CampaignZombie zombie)
    {
        for (int i = 0; i < zombiesByLane.Length; i++)
            zombiesByLane[i]?.Remove(zombie);
        allZombies.Remove(zombie);
    }

    private static void RemoveDestroyed(List<CampaignZombie> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
                list.RemoveAt(i);
        }
    }
}
