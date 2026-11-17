using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chỉ theo dõi zombie thuộc WAVE CUỐI (kể cả zombie được triệu hồi thêm mang số wave cuối).
/// Zombie của các wave trước không được đếm.
/// </summary>
public class CampaignZombieTracker : MonoBehaviour
{
    public static CampaignZombieTracker Instance { get; private set; }

    private readonly HashSet<CampaignZombie> finalWaveAlive = new();
    private int finalWaveNumber;

    public int FinalWaveNumber => finalWaveNumber;
    public int AliveCount => finalWaveAlive.Count;

    /// <summary>(số zombie wave cuối còn sống, vị trí zombie vừa chết)</summary>
    public event Action<int, Vector3> OnAliveCountChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Spawner gọi lúc bắt đầu level. finalWave = tổng số wave (số wave tính từ 1).</summary>
    public void Configure(int finalWave)
    {
        finalWaveNumber = finalWave;
        finalWaveAlive.Clear();
    }

    /// <summary>CampaignZombie.Initialize gọi. Zombie không thuộc wave cuối sẽ bị bỏ qua.</summary>
    public void RegisterSpawned(CampaignZombie zombie)
    {
        if (zombie == null || finalWaveNumber <= 0)
            return;

        if (zombie.WaveNumber != finalWaveNumber)
            return;

        finalWaveAlive.Add(zombie);
    }

    /// <summary>Gọi khi zombie chết. Gọi nhiều lần vẫn an toàn.</summary>
    public void UnregisterDead(CampaignZombie zombie)
    {
        if (zombie == null)
            return;

        if (finalWaveAlive.Remove(zombie))
            OnAliveCountChanged?.Invoke(finalWaveAlive.Count, zombie.transform.position);
    }
}