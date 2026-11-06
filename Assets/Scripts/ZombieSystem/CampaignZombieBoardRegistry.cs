
using System;
using System.Collections.Generic;
using UnityEngine;

public class CampaignZombieBoardRegistry : MonoBehaviour
{
    public static CampaignZombieBoardRegistry Instance { get; private set; }

    [Header("Registry Settings")]
    [SerializeField, Min(1)] private int laneCount = 5;
    [SerializeField] private string zombieTag = "Zombie";

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool logLaneDetails = true;
    [SerializeField] private bool logSubscription = true;
    [SerializeField] private bool logCleanup = true;

    // Lane index -> zombies currently inside the lawn.
    private readonly Dictionary<int, List<GameObject>> zombiesByLane = new();

    // Lane index -> events.
    private readonly Dictionary<int, LaneStateEvents> laneEvents = new();

    // Zombie -> Lane index.
    private readonly Dictionary<GameObject, int> laneByZombie = new();

    private sealed class LaneStateEvents
    {
        public event Action Started;
        public event Action Stopped;

        public int StartedSubscriberCount =>
            Started?.GetInvocationList().Length ?? 0;

        public int StoppedSubscriberCount =>
            Stopped?.GetInvocationList().Length ?? 0;

        public void Subscribe(Action onStarted, Action onStopped)
        {
            Started += onStarted;
            Stopped += onStopped;
        }

        public void Unsubscribe(Action onStarted, Action onStopped)
        {
            Started -= onStarted;
            Stopped -= onStopped;
        }

        public void RaiseStarted()
        {
            Started?.Invoke();
        }

        public void RaiseStopped()
        {
            Stopped?.Invoke();
        }
    }

    // =========================================================
    // DEBUG HELPERS
    // =========================================================

    private void Log(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ZombieBoardRegistry] {message}", context ?? this);
    }

    private void LogWarning(string message, UnityEngine.Object context = null)
    {
        Debug.LogWarning($"[ZombieBoardRegistry] {message}", context ?? this);
    }

    private void LogError(string message, UnityEngine.Object context = null)
    {
        Debug.LogError($"[ZombieBoardRegistry] {message}", context ?? this);
    }

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Awake()
    {
        Log("========== REGISTRY AWAKE ==========");

        if (Instance != null && Instance != this)
        {
            LogError(
                "PHÁT HIỆN NHIỀU REGISTRY! " +
                $"Existing: {Instance.name} | New: {name}"
            );

            return;
        }

        Instance = this;

        EnsureInitialized();

        Log($"Registry initialized successfully. Lane Count: {laneCount}");
        LogAllLaneStatus();
    }

    private void OnDestroy()
    {
        Log("========== REGISTRY DESTROY ==========");

        if (Instance == this)
        {
            Instance = null;
            Log("Registry Instance set to null.");
        }
    }

    // =========================================================
    // REGISTER ZOMBIE
    // =========================================================

    public bool RegisterZombieEnteredBoard(CampaignZombie zombie)
    {
        Log("========== REGISTER ZOMBIE START ==========");

        EnsureInitialized();

        Log(
            $"Register request received. " +
            $"Zombie: {(zombie != null ? zombie.name : "NULL")}"
        );

        CleanupDestroyedObjects();

        if (zombie == null)
        {
            LogWarning("REGISTER FAILED: CampaignZombie is NULL.");
            return false;
        }

        GameObject zombieObject = zombie.gameObject;

        Log($"Zombie GameObject: {zombieObject.name}");
        Log($"Zombie Tag: {zombieObject.tag}");
        Log($"Zombie Instance ID: {zombieObject.GetInstanceID()}");

        // Check Lane Index.
        int laneIndex = zombie.LaneIndex;

        Log($"Zombie LaneIndex: {laneIndex}");

        if (laneIndex < 0 || laneIndex >= laneCount)
        {
            LogError(
                $"REGISTER FAILED: Invalid LaneIndex = {laneIndex}. " +
                $"Valid range: 0 - {laneCount - 1}.",
                zombie
            );

            return false;
        }

        // Check Tag.
        if (!string.IsNullOrEmpty(zombieTag))
        {
            if (!zombieObject.CompareTag(zombieTag))
            {
                LogError(
                    $"REGISTER FAILED: Incorrect Tag. " +
                    $"Expected: '{zombieTag}' | " +
                    $"Actual: '{zombieObject.tag}'.",
                    zombie
                );

                return false;
            }
        }

        // Check duplicate.
        if (laneByZombie.ContainsKey(zombieObject))
        {
            LogWarning(
                $"REGISTER FAILED: Zombie '{zombieObject.name}' " +
                "is already registered."
            );

            Log(
                $"Existing Lane: {laneByZombie[zombieObject]}",
                zombie
            );

            return false;
        }

        List<GameObject> laneZombies = zombiesByLane[laneIndex];

        bool laneWasEmpty = laneZombies.Count == 0;

        Log(
            $"Before registration | " +
            $"Lane: {laneIndex} | " +
            $"Zombie Count: {laneZombies.Count} | " +
            $"Lane Was Empty: {laneWasEmpty}"
        );

        // Register.
        laneZombies.Add(zombieObject);
        laneByZombie.Add(zombieObject, laneIndex);

        zombie.SetBoardRegistry(this);

        Log(
            $"REGISTER SUCCESS | " +
            $"Zombie: {zombieObject.name} | " +
            $"Lane: {laneIndex} | " +
            $"Zombies In Lane: {laneZombies.Count} | " +
            $"Total Zombies: {laneByZombie.Count}",
            zombie
        );

        // Empty -> Occupied.
        if (laneWasEmpty)
        {
            Log(
                $"LANE {laneIndex} EMPTY -> OCCUPIED. " +
                "Invoking Started event."
            );

            Log(
                $"Lane {laneIndex} Started Subscribers: " +
                $"{laneEvents[laneIndex].StartedSubscriberCount}"
            );

            RaiseLaneStarted(laneIndex);
        }
        else
        {
            Log(
                $"LANE {laneIndex} ALREADY OCCUPIED. " +
                "No Started event required."
            );
        }

        LogAllLaneStatus();

        Log("========== REGISTER ZOMBIE END ==========");

        return true;
    }

    // =========================================================
    // UNREGISTER ZOMBIE
    // =========================================================

    public bool UnregisterZombieFromBoard(CampaignZombie zombie)
    {
        Log("========== UNREGISTER ZOMBIE START ==========");

        if (zombie == null)
        {
            LogWarning("UNREGISTER FAILED: Zombie is NULL.");
            return false;
        }

        EnsureInitialized();

        GameObject zombieObject = zombie.gameObject;

        Log($"Unregister request: {zombieObject.name}");

        if (!laneByZombie.TryGetValue(zombieObject, out int laneIndex))
        {
            LogWarning(
                $"UNREGISTER FAILED: '{zombieObject.name}' " +
                "does not exist in registry.",
                zombie
            );

            return false;
        }

        if (!zombiesByLane.TryGetValue(laneIndex, out List<GameObject> laneZombies))
        {
            LogError($"UNREGISTER FAILED: Lane {laneIndex} does not exist.");
            return false;
        }

        laneByZombie.Remove(zombieObject);

        bool removed = laneZombies.Remove(zombieObject);

        zombie.ClearBoardRegistry(this);

        Log(
            $"UNREGISTER RESULT | " +
            $"Zombie: {zombieObject.name} | " +
            $"Removed From Lane List: {removed} | " +
            $"Lane: {laneIndex} | " +
            $"Remaining: {laneZombies.Count} | " +
            $"Total: {laneByZombie.Count}",
            zombie
        );

        // Occupied -> Empty.
        if (laneZombies.Count == 0)
        {
            Log(
                $"LANE {laneIndex} OCCUPIED -> EMPTY. " +
                "Invoking Stopped event."
            );

            Log(
                $"Lane {laneIndex} Stopped Subscribers: " +
                $"{laneEvents[laneIndex].StoppedSubscriberCount}"
            );

            RaiseLaneStopped(laneIndex);
        }

        LogAllLaneStatus();

        Log("========== UNREGISTER ZOMBIE END ==========");

        return true;
    }

    // =========================================================
    // LANE EVENTS
    // =========================================================

    private void RaiseLaneStarted(int laneIndex)
    {
        Log($"RAISE STARTED EVENT | Lane: {laneIndex}");

        try
        {
            laneEvents[laneIndex].RaiseStarted();

            Log($"STARTED EVENT COMPLETED | Lane: {laneIndex}");
        }
        catch (Exception exception)
        {
            LogError(
                $"EXCEPTION in Started event | Lane: {laneIndex}\n" +
                exception
            );
        }
    }

    private void RaiseLaneStopped(int laneIndex)
    {
        Log($"RAISE STOPPED EVENT | Lane: {laneIndex}");

        try
        {
            laneEvents[laneIndex].RaiseStopped();

            Log($"STOPPED EVENT COMPLETED | Lane: {laneIndex}");
        }
        catch (Exception exception)
        {
            LogError(
                $"EXCEPTION in Stopped event | Lane: {laneIndex}\n" +
                exception
            );
        }
    }

    // =========================================================
    // SUBSCRIBE
    // =========================================================

    public void SubscribeToLane(
        int laneIndex,
        Action onLaneStarted,
        Action onLaneStopped,
        bool reportCurrentState = true)
    {
        Log(
            $"========== SUBSCRIBE REQUEST | Lane: {laneIndex} =========="
        );

        EnsureInitialized();
        CleanupDestroyedObjects();

        if (!laneEvents.TryGetValue(laneIndex, out LaneStateEvents channel))
        {
            LogError(
                $"SUBSCRIBE FAILED: Invalid LaneIndex {laneIndex}. " +
                $"Valid range: 0 - {laneCount - 1}."
            );

            return;
        }

        if (onLaneStarted == null || onLaneStopped == null)
        {
            LogWarning(
                $"SUBSCRIBE WARNING: Null callback detected. " +
                $"Started: {onLaneStarted != null} | " +
                $"Stopped: {onLaneStopped != null}"
            );
        }

        channel.Subscribe(onLaneStarted, onLaneStopped);

        Log(
            $"SUBSCRIBE SUCCESS | " +
            $"Lane: {laneIndex} | " +
            $"Started Subscribers: {channel.StartedSubscriberCount} | " +
            $"Stopped Subscribers: {channel.StoppedSubscriberCount}"
        );

        int currentZombieCount = zombiesByLane[laneIndex].Count;

        Log(
            $"Current Lane State | " +
            $"Lane: {laneIndex} | " +
            $"Zombie Count: {currentZombieCount} | " +
            $"Report Current State: {reportCurrentState}"
        );

        if (reportCurrentState && currentZombieCount > 0)
        {
            Log(
                $"LANE {laneIndex} ALREADY OCCUPIED. " +
                "Immediately invoking new Started callback."
            );

            try
            {
                onLaneStarted?.Invoke();

                Log($"IMMEDIATE STARTED CALLBACK COMPLETED | Lane: {laneIndex}");
            }
            catch (Exception exception)
            {
                LogError(
                    $"EXCEPTION in immediate Started callback | Lane: {laneIndex}\n" +
                    exception
                );
            }
        }

        Log($"========== SUBSCRIBE END | Lane: {laneIndex} ==========");
    }

    // =========================================================
    // UNSUBSCRIBE
    // =========================================================

    public void UnsubscribeFromLane(
        int laneIndex,
        Action onLaneStarted,
        Action onLaneStopped)
    {
        Log(
            $"========== UNSUBSCRIBE REQUEST | Lane: {laneIndex} =========="
        );

        EnsureInitialized();

        if (!laneEvents.TryGetValue(laneIndex, out LaneStateEvents channel))
        {
            LogWarning($"UNSUBSCRIBE FAILED: Invalid LaneIndex {laneIndex}.");
            return;
        }

        int beforeStarted = channel.StartedSubscriberCount;
        int beforeStopped = channel.StoppedSubscriberCount;

        channel.Unsubscribe(onLaneStarted, onLaneStopped);

        Log(
            $"UNSUBSCRIBE COMPLETED | " +
            $"Lane: {laneIndex} | " +
            $"Started: {beforeStarted} -> {channel.StartedSubscriberCount} | " +
            $"Stopped: {beforeStopped} -> {channel.StoppedSubscriberCount}"
        );
    }

    // =========================================================
    // GET ZOMBIES
    // =========================================================

    public IReadOnlyList<GameObject> GetZombiesInLane(int laneIndex)
    {
        EnsureInitialized();
        CleanupDestroyedObjects();

        if (!zombiesByLane.TryGetValue(laneIndex, out List<GameObject> zombies))
        {
            LogWarning($"GET ZOMBIES FAILED: Invalid Lane {laneIndex}.");
            return Array.Empty<GameObject>();
        }

        if (logLaneDetails)
        {
            Log(
                $"GET ZOMBIES | Lane: {laneIndex} | Count: {zombies.Count}"
            );
        }

        return zombies;
    }

    public List<GameObject> GetAllZombiesOnBoard()
    {
        EnsureInitialized();
        CleanupDestroyedObjects();

        var all = new List<GameObject>(laneByZombie.Count);

        foreach (List<GameObject> laneZombies in zombiesByLane.Values)
        {
            all.AddRange(laneZombies);
        }

        Log($"GET ALL ZOMBIES | Total: {all.Count}");

        return all;
    }

    public bool TryGetLane(GameObject zombie, out int laneIndex)
    {
        EnsureInitialized();
        CleanupDestroyedObjects();

        bool found = laneByZombie.TryGetValue(zombie, out laneIndex);

        Log(
            $"TRY GET LANE | " +
            $"Zombie: {(zombie != null ? zombie.name : "NULL")} | " +
            $"Found: {found} | " +
            $"Lane: {(found ? laneIndex.ToString() : "N/A")}"
        );

        return found;
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void EnsureInitialized()
    {
        laneCount = Mathf.Max(1, laneCount);

        for (int lane = 0; lane < laneCount; lane++)
        {
            if (!zombiesByLane.ContainsKey(lane))
            {
                zombiesByLane.Add(lane, new List<GameObject>());

                Log($"Initialized Zombie List for Lane {lane}.");
            }

            if (!laneEvents.ContainsKey(lane))
            {
                laneEvents.Add(lane, new LaneStateEvents());

                Log($"Initialized Event Channel for Lane {lane}.");
            }
        }
    }

    // =========================================================
    // CLEANUP DESTROYED OBJECTS
    // =========================================================

    private void CleanupDestroyedObjects()
    {
        var destroyedKeys = new List<GameObject>();

        foreach (KeyValuePair<GameObject, int> pair in laneByZombie)
        {
            if (pair.Key == null)
            {
                destroyedKeys.Add(pair.Key);
            }
        }

        if (destroyedKeys.Count == 0)
            return;

        LogWarning(
            $"CLEANUP START | Destroyed Zombies Found: {destroyedKeys.Count}"
        );

        HashSet<int> affectedLanes = new();

        foreach (GameObject zombieObject in destroyedKeys)
        {
            if (!laneByZombie.TryGetValue(zombieObject, out int laneIndex))
                continue;

            laneByZombie.Remove(zombieObject);

            zombiesByLane[laneIndex].Remove(zombieObject);

            affectedLanes.Add(laneIndex);

            if (logCleanup)
            {
                Log(
                    $"DESTROYED ZOMBIE CLEANED | Lane: {laneIndex}"
                );
            }
        }

        foreach (int laneIndex in affectedLanes)
        {
            if (zombiesByLane[laneIndex].Count == 0)
            {
                Log(
                    $"LANE {laneIndex} EMPTY AFTER CLEANUP. " +
                    "Invoking Stopped event."
                );

                RaiseLaneStopped(laneIndex);
            }
        }

        LogAllLaneStatus();
    }

    // =========================================================
    // DEBUG LANE STATUS
    // =========================================================

    private void LogAllLaneStatus()
    {
        if (!enableDebugLog || !logLaneDetails)
            return;

        Log("========== CURRENT BOARD STATUS ==========");

        for (int lane = 0; lane < laneCount; lane++)
        {
            int count = zombiesByLane[lane].Count;

            Log(
                $"Lane {lane} | " +
                $"State: {(count > 0 ? "OCCUPIED" : "EMPTY")} | " +
                $"Zombie Count: {count} | " +
                $"Started Subscribers: {laneEvents[lane].StartedSubscriberCount} | " +
                $"Stopped Subscribers: {laneEvents[lane].StoppedSubscriberCount}"
            );

            foreach (GameObject zombie in zombiesByLane[lane])
            {
                Log(
                    $"    -> Zombie: {(zombie != null ? zombie.name : "DESTROYED")}"
                );
            }
        }

        Log($"TOTAL REGISTERED ZOMBIES: {laneByZombie.Count}");

        Log("===========================================");
    }

    // =========================================================
    // MANUAL DEBUG
    // =========================================================

    [ContextMenu("Debug/Print All Lane Status")]
    private void DebugPrintAllLaneStatus()
    {
        EnsureInitialized();
        CleanupDestroyedObjects();
        LogAllLaneStatus();
    }
}
