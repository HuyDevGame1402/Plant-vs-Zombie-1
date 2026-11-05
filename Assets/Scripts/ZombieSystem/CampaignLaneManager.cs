using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns five lane spawn anchors and chooses a valid lane and horizontal spawn position.</summary>
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
}
