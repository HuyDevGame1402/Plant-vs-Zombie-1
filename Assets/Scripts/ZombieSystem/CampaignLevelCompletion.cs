using System;
using UnityEngine;

/// <summary>
/// Kết thúc level khi: đã spawn xong wave cuối VÀ mọi zombie wave cuối đã chết.
/// </summary>
public class CampaignLevelCompletion : MonoBehaviour
{
    [SerializeField] private CampaignWaveSpawner spawner;

    /// <summary>Vị trí zombie cuối cùng chết (để rơi cúp/phần thưởng như PvZ1).</summary>
    public event Action<Vector3> OnLevelCleared;

    private CampaignZombieTracker tracker;
    private bool allWavesSpawned;
    private bool completed;

    public bool IsCompleted => completed;

    private void Awake()
    {
        // Hai component cùng nằm trên một GameObject.
        tracker = GetComponent<CampaignZombieTracker>();

        if (spawner == null)
            spawner = GetComponent<CampaignWaveSpawner>();
    }

    private void OnEnable()
    {
        if (spawner == null || tracker == null)
        {
            Debug.LogError(
                $"Missing reference! Spawner: {spawner}, Tracker: {tracker}",
                this
            );
            return;
        }

        spawner.OnAllWavesSpawned += HandleAllWavesSpawned;
        tracker.OnAliveCountChanged += HandleAliveChanged;
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnAllWavesSpawned -= HandleAllWavesSpawned;

        if (tracker != null)
            tracker.OnAliveCountChanged -= HandleAliveChanged;
    }

    /// <summary>Gọi khi thua (zombie vào nhà) để không bắn OnLevelCleared nữa.</summary>
    public void Cancel()
    {
        completed = true;
    }

    private void HandleAllWavesSpawned()
    {
        allWavesSpawned = true;

        // Trường hợp wave cuối không spawn được zombie nào.
        if (tracker.AliveCount == 0)
            Complete(Vector3.zero);
    }

    private void HandleAliveChanged(int aliveCount, Vector3 lastDeathPosition)
    {
        if (allWavesSpawned && aliveCount == 0)
            Complete(lastDeathPosition);
    }

    private void Complete(Vector3 position)
    {
        if (completed)
            return;

        completed = true;
        Debug.Log("Level cleared: wave cuối đã bị tiêu diệt hoàn toàn.", this);
        OnLevelCleared?.Invoke(position);
    }
}