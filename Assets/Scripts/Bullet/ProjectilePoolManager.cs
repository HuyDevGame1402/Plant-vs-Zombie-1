
using System.Collections.Generic;
using UnityEngine;

public class ProjectilePoolManager : MonoBehaviour
{
    public static ProjectilePoolManager Instance { get; private set; }

    [System.Serializable]
    public class PoolEntry
    {
        [Tooltip("Prefab của loại đạn")]
        public GameObject prefab;

        [Min(0)]
        [Tooltip("Số lượng đạn tạo sẵn lúc khởi động")]
        public int initialSize = 10;

        [Min(1)]
        [Tooltip("Số lượng đạn tối đa được giữ trong Pool")]
        public int maxSize = 50;
    }

    private class ProjectilePool
    {
        public GameObject prefab;
        public Transform container;
        public Queue<GameObject> available = new Queue<GameObject>();
        public int maxSize;
    }

    [Header("Pool Configuration")]
    [SerializeField] private List<PoolEntry> poolEntries = new List<PoolEntry>();

    [Header("Settings")]
    [SerializeField] private bool createPoolsOnAwake = true;

    private readonly Dictionary<GameObject, ProjectilePool> pools =
        new Dictionary<GameObject, ProjectilePool>();

    private readonly Dictionary<GameObject, GameObject> instanceToPrefab =
        new Dictionary<GameObject, GameObject>();

    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[ProjectilePoolManager] Duplicate Manager!", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (createPoolsOnAwake)
            InitializePools();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---------------------------------------------------------
    // INITIALIZE
    // ---------------------------------------------------------

    public void InitializePools()
    {
        pools.Clear();
        instanceToPrefab.Clear();

        foreach (PoolEntry entry in poolEntries)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning("[ProjectilePoolManager] Có Pool chưa gán Prefab!", this);
                continue;
            }

            if (pools.ContainsKey(entry.prefab))
            {
                Debug.LogWarning(
                    $"[ProjectilePoolManager] Prefab {entry.prefab.name} bị đăng ký trùng!",
                    this
                );
                continue;
            }

            CreatePool(entry);
        }
    }

    private void CreatePool(PoolEntry entry)
    {
        GameObject containerObject = new GameObject(
            $"Pool_{entry.prefab.name}"
        );

        containerObject.transform.SetParent(transform, false);

        ProjectilePool pool = new ProjectilePool
        {
            prefab = entry.prefab,
            container = containerObject.transform,
            maxSize = Mathf.Max(1, entry.maxSize)
        };

        pools.Add(entry.prefab, pool);

        int initialSize = Mathf.Min(
            Mathf.Max(0, entry.initialSize),
            pool.maxSize
        );

        for (int i = 0; i < initialSize; i++)
        {
            GameObject projectile = CreateProjectile(pool);

            ReturnToPool(projectile);
        }

        Debug.Log(
            $"[ProjectilePoolManager] Initialized {entry.prefab.name} | " +
            $"Initial: {initialSize} | Max: {pool.maxSize}",
            this
        );
    }

    // ---------------------------------------------------------
    // CREATE PROJECTILE
    // ---------------------------------------------------------

    private GameObject CreateProjectile(ProjectilePool pool)
    {
        GameObject projectile = Instantiate(
            pool.prefab,
            pool.container
        );

        projectile.name = pool.prefab.name;

        projectile.SetActive(false);

        instanceToPrefab.Add(projectile, pool.prefab);

        return projectile;
    }

    // ---------------------------------------------------------
    // GET PROJECTILE
    // ---------------------------------------------------------

    public GameObject GetProjectile(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null
    )
    {
        if (prefab == null)
        {
            Debug.LogError("[ProjectilePoolManager] Prefab is null!");
            return null;
        }

        if (!pools.TryGetValue(prefab, out ProjectilePool pool))
        {
            Debug.LogError(
                $"[ProjectilePoolManager] Prefab {prefab.name} chưa được đăng ký!",
                this
            );

            return null;
        }

        GameObject projectile;

        if (pool.available.Count > 0)
        {
            projectile = pool.available.Dequeue();
        }
        else
        {
            projectile = CreateProjectile(pool);
        }

        Transform projectileTransform = projectile.transform;

        projectileTransform.SetParent(
            parent != null ? parent : pool.container,
            true
        );

        projectileTransform.SetPositionAndRotation(
            position,
            rotation
        );

        // Reset trạng thái trước khi kích hoạt.
        BulletMovement movement = projectile.GetComponent<BulletMovement>();

        if (movement != null)
        {
            movement.ResetBullet();
        }

        projectile.SetActive(true);

        return projectile;
    }

    // ---------------------------------------------------------
    // RETURN PROJECTILE
    // ---------------------------------------------------------

    public void ReturnToPool(GameObject projectile)
    {
        if (projectile == null)
            return;

        if (!instanceToPrefab.TryGetValue(
            projectile,
            out GameObject prefab
        ))
        {
            Debug.LogWarning(
                $"[ProjectilePoolManager] {projectile.name} không thuộc Pool!",
                projectile
            );

            return;
        }

        if (!pools.TryGetValue(prefab, out ProjectilePool pool))
            return;

        // Ngăn trả cùng một viên đạn nhiều lần.
        if (!projectile.activeSelf)
            return;

        BulletMovement movement = projectile.GetComponent<BulletMovement>();

        if (movement != null)
            movement.PrepareForPool();

        projectile.SetActive(false);

        projectile.transform.SetParent(
            pool.container,
            false
        );

        if (pool.available.Count >= pool.maxSize)
        {
            instanceToPrefab.Remove(projectile);
            Destroy(projectile);
            return;
        }

        pool.available.Enqueue(projectile);
    }

    // ---------------------------------------------------------
    // UTILITY
    // ---------------------------------------------------------

    public bool IsRegistered(GameObject prefab)
    {
        return prefab != null && pools.ContainsKey(prefab);
    }

    public int GetAvailableCount(GameObject prefab)
    {
        if (prefab == null)
            return 0;

        if (pools.TryGetValue(prefab, out ProjectilePool pool))
            return pool.available.Count;

        return 0;
    }
}
