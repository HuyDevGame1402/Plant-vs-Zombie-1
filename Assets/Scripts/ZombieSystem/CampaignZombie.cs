using PvZ.Data;
using UnityEngine;

/// <summary>Runtime identity for one spawned zombie; the lane manager uses it for lane/global queries.</summary>
public class CampaignZombie : MonoBehaviour
{
    [SerializeField] private ZombieType zombieType;
    [SerializeField] private int laneIndex = -1;
    [SerializeField] private int waveNumber;
    [SerializeField] private bool isRegistered;

    public ZombieType ZombieType => zombieType;
    public int LaneIndex => laneIndex;
    public int WaveNumber => waveNumber;
    public bool IsRegistered => isRegistered;

    private CampaignLaneManager laneManager;

    [SerializeField] private ZombieStatsAsset zombieDatabase;
    public ZombieStatsAsset ZombieDatabase => zombieDatabase;

    public void Initialize(ZombieType zombieType, int laneIndex, int waveNumber, CampaignLaneManager manager)
    {
        if (isRegistered && laneManager != null)
            laneManager.UnregisterZombie(this);

        this.zombieType = zombieType;
        this.laneIndex = laneIndex;
        this.waveNumber = waveNumber;
        laneManager = manager;

        RegisterIfReady();

        // Tracker tự lọc: chỉ giữ zombie thuộc wave cuối.
        CampaignZombieTracker.Instance?.RegisterSpawned(this);
    }

    private void OnEnable()
    {
        RegisterIfReady();
    }

    private void OnDisable()
    {
        // Chỉ gỡ khỏi lane/board. KHÔNG tính là "chết" để tránh thắng nhầm khi tắt object.
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();

        // Phòng hờ zombie bị Destroy mà không đi qua đường chết bình thường.
        CampaignZombieTracker.Instance?.UnregisterDead(this);
    }

    private void RegisterIfReady()
    {
        if (!isActiveAndEnabled || laneManager == null || laneIndex < 0)
            return;

        laneManager.RegisterZombie(this);
        isRegistered = true;
    }

    /// <summary>
    /// ZombieHealthRuntime gọi lúc zombie thực sự chết (HP về 0 hoặc mất đầu chết ngay).
    /// Gỡ khỏi lane/board và báo tracker.
    /// </summary>
    public void NotifyDeath()
    {
        Unregister();
        CampaignZombieTracker.Instance?.UnregisterDead(this);
    }

    public void Unregister()
    {
        if (CampaignZombieBoardRegistry.Instance != null)
            CampaignZombieBoardRegistry.Instance.UnregisterZombieFromBoard(this);

        if (isRegistered && laneManager != null)
            laneManager.UnregisterZombie(this);

        isRegistered = false;
    }
}