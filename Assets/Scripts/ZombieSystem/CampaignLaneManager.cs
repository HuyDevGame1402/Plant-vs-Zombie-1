using System;
using System.Collections.Generic;
using UnityEngine;
using PvZ.Data;

/// <summary>
/// Lưu zombie đang sống theo lane, và chọn lane/vị trí spawn theo luật của
/// Board::RowCanHaveZombieType + Board::PickRowForNewZombie.
/// </summary>
public class CampaignLaneManager : MonoBehaviour
{
    [Serializable]
    public class LaneSpawnPoint
    {
        [Tooltip("Lane index: 0 là trên cùng. Level hồ bơi (21-40) dùng 6 lane.")]
        public Transform spawnPoint;

        [Tooltip("Độ lệch X tối đa (world space) so với spawn point.")]
        [Min(0f)] public float horizontalOffset = 0.5f;
    }

    [Tooltip("5 phần tử cho level thường, 6 phần tử nếu có level hồ bơi/sương mù (21-40).")]
    [SerializeField] private LaneSpawnPoint[] lanes = new LaneSpawnPoint[5];

    [Tooltip("Các lane là hồ bơi ở level 21-40 (game gốc: hàng 2 và 3, đếm từ 0).")]
    [SerializeField] private int[] poolLanes = { 2, 3 };

    [Header("Spacing")]
    [Tooltip("Khoảng cách X tối thiểu giữa 2 zombie spawn cùng lane trong cùng 1 wave.")]
    [SerializeField, Min(0f)] private float minSpacing = 0.6f;

    private readonly List<float>[] usedXThisWave = new List<float>[CampaignRules.MaxLanes];

    private readonly List<CampaignZombie>[] zombiesByLane = new List<CampaignZombie>[CampaignRules.MaxLanes];
    private readonly List<CampaignZombie> allZombies = new();

    // Trạng thái chọn lane
    private readonly int[] pickAge = new int[CampaignRules.MaxLanes];
    private readonly int[] lastMoweredWave = new int[CampaignRules.MaxLanes];
    private readonly bool[] laneHasIce = new bool[CampaignRules.MaxLanes];
    private bool stateInitialized;

    private void Awake() => InitState();

    private void InitState()
    {
        if (stateInitialized)
            return;

        for (int i = 0; i < zombiesByLane.Length; i++)
        {
            zombiesByLane[i] ??= new List<CampaignZombie>();
            pickAge[i] = 99;
            lastMoweredWave[i] = -100; // giống mWaveRowGotLawnMowered
            laneHasIce[i] = false;
        }

        stateInitialized = true;
    }

    // ------------------------------------------------------------------ spawn picking

    /// <param name="waveIndex">Chỉ số wave bắt đầu từ 0 (wave 1 => 0).</param>
    public bool TryPickSpawn(
        int globalLevel,
        ZombieType type,
        int waveIndex,
        System.Random random,
        out int laneIndex,
        out Vector3 spawnPosition)
    {
        InitState();
        laneIndex = -1;
        spawnPosition = default;

        if (lanes == null || lanes.Length < 5 || lanes.Length > CampaignRules.MaxLanes)
        {
            Debug.LogError("CampaignLaneManager cần 5 hoặc 6 lane entries.", this);
            return false;
        }

        if (globalLevel < 1 || globalLevel > 50)
        {
            Debug.LogError($"Invalid global campaign level: {globalLevel}.", this);
            return false;
        }

        random ??= new System.Random();

        var weights = new float[lanes.Length];
        float total = 0f;

        for (int i = 0; i < lanes.Length; i++)
        {
            if (!RowCanHaveZombieType(globalLevel, i, type, waveIndex))
                continue;

            int wavesMowered = waveIndex - lastMoweredWave[i];
            float w = wavesMowered <= 1 ? 0.01f : (wavesMowered <= 2 ? 0.5f : 1f);

            // Làm mượt: tránh chọn lại cùng 1 lane liên tục (xấp xỉ PvzpPickFromSmoothArray).
            int age = pickAge[i];
            float smooth = age == 0 ? 0.2f : (age == 1 ? 0.5f : (age == 2 ? 0.8f : 1f));

            weights[i] = w * smooth;
            total += weights[i];
        }

        if (total <= 0f)
        {
            // Không lane nào hợp lệ theo luật -> chọn bừa 1 lane đang hoạt động để không mất zombie.
            var fallback = new List<int>();
            for (int i = 0; i < lanes.Length; i++)
                if (IsRowActive(globalLevel, i))
                    fallback.Add(i);

            if (fallback.Count == 0)
            {
                Debug.LogError($"Không có lane nào có spawn point ở level {globalLevel}.", this);
                return false;
            }

            Debug.LogWarning($"Không có lane hợp lệ cho {type} (wave {waveIndex + 1}); dùng lane ngẫu nhiên.", this);
            laneIndex = fallback[random.Next(fallback.Count)];
        }
        else
        {
            double roll = random.NextDouble() * total;
            laneIndex = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                laneIndex = i;
                roll -= weights[i];
                if (roll < 0) break;
            }
        }

        for (int i = 0; i < pickAge.Length; i++)
            pickAge[i]++;
        pickAge[laneIndex] = 0;

        LaneSpawnPoint lane = lanes[laneIndex];
        Vector3 basePos = lane.spawnPoint.position;
        float x = PickSpacedX(laneIndex, basePos.x, lane.horizontalOffset, random);
        spawnPosition = new Vector3(x, basePos.y, basePos.z);
        return true;
    }

    /// <summary>Gọi đầu mỗi wave để xoá danh sách vị trí đã dùng trong từng lane.</summary>
    public void BeginSpawnWave()
    {
        for (int i = 0; i < usedXThisWave.Length; i++)
            usedXThisWave[i]?.Clear();
    }

    /// <summary>
    /// Thử vài vị trí ngẫu nhiên trong ±range, lấy vị trí xa các zombie đã spawn cùng lane nhất.
    /// Nếu vẫn gần hơn minSpacing thì xếp tiếp về bên PHẢI (phía sau) zombie ngoài cùng.
    /// </summary>
    private float PickSpacedX(int laneIndex, float baseX, float range, System.Random random)
    {
        List<float> used = usedXThisWave[laneIndex] ??= new List<float>();

        float best = baseX;
        float bestDist = -1f;
        for (int k = 0; k < 8; k++)
        {
            float candidate = baseX + ((float)random.NextDouble() * 2f - 1f) * range;
            float d = MinDistance(used, candidate);
            if (d > bestDist)
            {
                bestDist = d;
                best = candidate;
            }
        }

        if (used.Count > 0 && bestDist < minSpacing)
        {
            float maxX = float.MinValue;
            foreach (float u in used)
                if (u > maxX) maxX = u;
            best = maxX + minSpacing;
        }

        used.Add(best);
        return best;
    }

    private static float MinDistance(List<float> used, float x)
    {
        float min = float.MaxValue;
        foreach (float u in used)
        {
            float d = Mathf.Abs(u - x);
            if (d < min) min = d;
        }
        return min;
    }

    /// <summary>Port của Board::RowCanHaveZombieType (phần dùng được trong Adventure).</summary>
    public bool RowCanHaveZombieType(int globalLevel, int row, ZombieType type, int waveIndex)
    {
        if (!IsRowActive(globalLevel, row))
            return false;

        bool poolRow = IsPoolLane(globalLevel, row);

        if (poolRow)
        {
            if (!CampaignRules.CanGoInPool(type) && type != ZombieType.Balloon)
                return false;
            if (waveIndex < 5 && !CampaignRules.IsPoolOnly(type))
                return false;
        }
        else if (CampaignRules.IsPoolOnly(type))
        {
            return false;
        }

        if (type == ZombieType.BobsledTeam && !laneHasIce[row])
            return false;

        if (row == 0 && type == ZombieType.Gargantuar)
            return false;

        if (type == ZombieType.Dancing && !CampaignRules.IsPoolStage(globalLevel))
            return IsRowActive(globalLevel, row - 1) && IsRowActive(globalLevel, row + 1);

        return true;
    }

    private bool IsRowActive(int globalLevel, int row)
        => row >= 0
        && lanes != null
        && row < lanes.Length
        && IsLaneAllowed(globalLevel, row)
        && lanes[row] != null
        && lanes[row].spawnPoint != null;

    public bool IsPoolLane(int globalLevel, int row)
    {
        if (!CampaignRules.IsPoolStage(globalLevel) || poolLanes == null)
            return false;

        for (int i = 0; i < poolLanes.Length; i++)
            if (poolLanes[i] == row)
                return true;
        return false;
    }

    public static bool IsLaneAllowed(int globalLevel, int laneIndex)
    {
        if (globalLevel < 1 || globalLevel > 50)
            return false;
        if (laneIndex < 0 || laneIndex >= CampaignRules.LaneCountForLevel(globalLevel))
            return false;

        if (globalLevel == 1)
            return laneIndex == 2;

        if (globalLevel == 2 || globalLevel == 3)
            return laneIndex >= 1 && laneIndex <= 3;

        return true;
    }

    // ------------------------------------------------------------------ hooks cho gameplay

    /// <summary>Gọi khi Zomboni để lại vệt băng (true) / băng tan (false).</summary>
    public void SetLaneHasIce(int laneIndex, bool hasIce)
    {
        InitState();
        if (IsValidLane(laneIndex))
            laneHasIce[laneIndex] = hasIce;
    }

    /// <summary>Board::CanAddBobSled (đơn giản hoá: có bất kỳ lane nào có băng).</summary>
    public bool CanAddBobsled()
    {
        InitState();
        for (int i = 0; i < laneHasIce.Length; i++)
            if (laneHasIce[i])
                return true;
        return false;
    }

    /// <summary>Gọi khi xe cắt cỏ của lane bị kích hoạt, để lane đó bớt bị spawn dồn.</summary>
    public void NotifyLaneMowered(int laneIndex, int waveIndex)
    {
        InitState();
        if (IsValidLane(laneIndex))
            lastMoweredWave[laneIndex] = waveIndex;
    }

    /// <summary>
    /// Board::TotalZombiesHealthInWave. waveNumber bắt đầu từ 1.
    /// Bỏ qua Bungee; giáp kiểu "shield" (Newspaper, ScreenDoor) tính 20%.
    /// </summary>
    public float GetWaveThreatHealth(int waveNumber)
    {
        float total = 0f;
        IReadOnlyList<CampaignZombie> zombies = GetAllZombies();

        for (int i = 0; i < zombies.Count; i++)
        {
            CampaignZombie z = zombies[i];
            if (z == null || z.WaveNumber != waveNumber || z.ZombieType == ZombieType.Bungee)
                continue;

            ZombieHealthRuntime hp = z.GetComponent<ZombieHealthRuntime>();
            if (hp == null)
                hp = z.GetComponentInChildren<ZombieHealthRuntime>();

            total += hp != null ? hp.GetThreatHealth() : 270f;
        }

        return total;
    }

    // ------------------------------------------------------------------ registry

    public IReadOnlyList<CampaignZombie> GetZombiesInLane(int laneIndex)
    {
        InitState();
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

        InitState();
        int laneIndex = zombie.LaneIndex;
        if (!IsValidLane(laneIndex))
        {
            Debug.LogError($"Cannot register zombie with invalid lane index {laneIndex}.", zombie);
            return;
        }

        RemoveFromLists(zombie);
        zombiesByLane[laneIndex].Add(zombie);
        allZombies.Add(zombie);
    }

    public void UnregisterZombie(CampaignZombie zombie)
    {
        if (zombie == null)
            return;

        InitState();
        RemoveFromLists(zombie);
    }

    private bool IsValidLane(int laneIndex)
        => laneIndex >= 0 && laneIndex < zombiesByLane.Length;

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