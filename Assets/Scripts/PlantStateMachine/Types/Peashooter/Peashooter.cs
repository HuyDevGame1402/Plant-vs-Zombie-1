using UnityEngine;
using PlantReanimation.Controllers.Peashooter;

public class Peashooter : Plant, IIdle, IAttack
{
    [Header("References")]
    [SerializeField] private PeashooterController peashooterControllerAnimation;

    [Header("Lane Settings")]
    [SerializeField] private int laneIndex = 2;

    [Header("Projectile (dùng chung cho họ cây đậu)")]
    [Tooltip("Prefab đạn. Peashooter/Repeater/Gatling = đạn thường, Snow Pea = đạn băng...")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Điểm sinh đạn (Transform con đặt trước miệng cây). Để trống = dùng vị trí của cây.")]
    [SerializeField] private Transform muzzle;

    [Tooltip("Object cha chứa đạn khi spawn. Để trống = để ở root scene.")]
    [SerializeField] private Transform projectileParent;

    [Tooltip("Số đạn mỗi lượt tấn công. Peashooter = 1, Repeater = 2, Gatling = 4.")]
    [SerializeField, Min(1)] private int shotsPerBurst = 1;

    [Header("Attack Timing")]
    [Tooltip("Số giây tương ứng với một game tick.")]
    [SerializeField, Min(0f)]
    private float secondsPerGameTick = 0.01f;

    [Tooltip("Thời gian thử lại nếu Shoot() thất bại.")]
    [SerializeField, Min(0.01f)]
    private float retryDelay = 0.2f;

    [Header("Runtime Timing (Read Only)")]
    [SerializeField] private float initialAttackDelay;
    [SerializeField] private float attackDelayRemaining;

    private IdleState idleState;
    private AttackState attackState;

    private bool statesInitialized;
    private bool isSubscribed;
    private bool isAttacking;
    private bool hasZombie;

    protected override void Start()
    {
        // Tính Attack Delay duy nhất một lần khi bắt đầu.
        CalculateInitialAttackDelay();

        base.Start();

        // Đồng bộ số đạn mỗi lượt xuống controller animation.
        if (peashooterControllerAnimation != null)
            peashooterControllerAnimation.shotsPerBurst = Mathf.Max(1, shotsPerBurst);

        SubscribeToLane();
    }

    private void OnEnable()
    {
        SubscribeToAnimationEvents();

        if (statesInitialized)
        {
            SubscribeToLane();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromLane();
        UnsubscribeFromAnimationEvents();

        hasZombie = false;
        isAttacking = false;
        attackDelayRemaining = 0f;
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    protected override void InitializeStates()
    {
        idleState = new IdleState(this, this);
        attackState = new AttackState(this, this);

        statesInitialized = true;

        stateMachine.ChangeState(idleState);
    }

    // =========================================================
    // CALCULATE ATTACK DELAY ONLY ONCE
    // =========================================================

    private void CalculateInitialAttackDelay()
    {
        if (plantData == null)
        {
            Debug.LogError(
                "[Peashooter] PlantData chưa được gán!",
                this
            );

            initialAttackDelay = 0f;
            return;
        }

        int nominalTicks = Mathf.Max(
            0,
            plantData.nominalIntervalTicks
        );

        int variationTicks = Mathf.Max(
            0,
            plantData.intervalJitterTicks
        );

        int minTicks = Mathf.Max(
            0,
            nominalTicks - variationTicks
        );

        int maxTicks = nominalTicks + variationTicks;

        // Random đúng một lần duy nhất.
        int randomTicks = Random.Range(
            minTicks,
            maxTicks + 1
        );

        initialAttackDelay = randomTicks * secondsPerGameTick;

        Debug.Log(
            $"[Peashooter] INITIAL ATTACK DELAY | " +
            $"Nominal: {nominalTicks} ticks | " +
            $"Jitter: {variationTicks} ticks | " +
            $"Random: {randomTicks} ticks | " +
            $"Seconds Per Tick: {secondsPerGameTick} | " +
            $"Final Delay: {initialAttackDelay:F3}s",
            this
        );
    }

    // =========================================================
    // LANE SUBSCRIPTION
    // =========================================================

    private void SubscribeToLane()
    {
        if (isSubscribed)
            return;

        if (CampaignZombieBoardRegistry.Instance == null)
        {
            Debug.LogError(
                "[Peashooter] CampaignZombieBoardRegistry chưa được khởi tạo!",
                this
            );
            return;
        }

        CampaignZombieBoardRegistry.Instance.SubscribeToLane(
            laneIndex,
            OnLaneStarted,
            OnLaneStopped
        );

        isSubscribed = true;
    }

    private void UnsubscribeFromLane()
    {
        if (!isSubscribed)
            return;

        if (CampaignZombieBoardRegistry.Instance != null)
        {
            CampaignZombieBoardRegistry.Instance.UnsubscribeFromLane(
                laneIndex,
                OnLaneStarted,
                OnLaneStopped
            );
        }

        isSubscribed = false;
    }

    // =========================================================
    // ANIMATION EVENTS (OnFire + OnAttackCompleted)
    // =========================================================

    private void SubscribeToAnimationEvents()
    {
        if (peashooterControllerAnimation == null)
        {
            Debug.LogError(
                "[Peashooter] Chưa gán PeashooterController!",
                this
            );
            return;
        }

        // Gỡ trước rồi gắn lại để không bị đăng ký trùng.
        peashooterControllerAnimation.OnAttackCompleted -= HandleAttackCompleted;
        peashooterControllerAnimation.OnAttackCompleted += HandleAttackCompleted;

        peashooterControllerAnimation.OnFire -= HandleFire;
        peashooterControllerAnimation.OnFire += HandleFire;
    }

    private void UnsubscribeFromAnimationEvents()
    {
        if (peashooterControllerAnimation == null)
            return;

        peashooterControllerAnimation.OnAttackCompleted -= HandleAttackCompleted;
        peashooterControllerAnimation.OnFire -= HandleFire;
    }

    // =========================================================
    // SPAWN PROJECTILE
    // =========================================================

    // Được gọi mỗi lần controller bắn một viên (đúng frame fireFrame của clip).

    private void HandleFire()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning(
                "[Peashooter] Chưa gán bulletPrefab!",
                this
            );

            return;
        }

        if (ProjectilePoolManager.Instance == null)
        {
            Debug.LogError(
                "[Peashooter] ProjectilePoolManager chưa tồn tại!",
                this
            );

            return;
        }

        Transform spawnPoint = muzzle != null
            ? muzzle
            : transform;

        // Lấy đạn từ Pool.
        GameObject bullet =
            ProjectilePoolManager.Instance.GetProjectile(
                bulletPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                projectileParent
            );

        if (bullet == null)
            return;

        BulletMovement movement =
            bullet.GetComponent<BulletMovement>();

        if (movement == null)
        {
            Debug.LogError(
                "[Peashooter] Bullet Prefab thiếu BulletMovement!",
                bullet
            );

            ProjectilePoolManager.Instance.ReturnToPool(bullet);

            return;
        }

        // Bắt đầu bay.
        movement.StartFlying();

        Debug.Log(
            $"[Peashooter] Get bullet from Pool | " +
            $"Shot: {peashooterControllerAnimation.CurrentShotIndex + 1}/{shotsPerBurst}",
            this
        );
    }


    // =========================================================
    // LANE EVENTS
    // =========================================================

    private void OnLaneStarted()
    {
        hasZombie = true;

        // Sử dụng delay đã tính từ đầu game.
        // Không random lại.
        if (!isAttacking)
        {
            attackDelayRemaining = initialAttackDelay;
        }

        Debug.Log(
            $"[Peashooter] Zombie appeared | " +
            $"Initial Delay: {initialAttackDelay:F3}s",
            this
        );
    }

    private void OnLaneStopped()
    {
        hasZombie = false;
        attackDelayRemaining = 0f;

        Debug.Log(
            "[Peashooter] Lane không còn zombie.",
            this
        );
    }

    // =========================================================
    // IDLE STATE
    // =========================================================

    public void OnIdleEnter()
    {
        if (peashooterControllerAnimation == null)
            return;

        peashooterControllerAnimation.gameObject.SetActive(true);
        peashooterControllerAnimation.PlayAnimationIdle();

        Debug.Log("[Peashooter] Enter Idle", this);
    }

    public void OnIdleUpdate()
    {
        if (!hasZombie || isAttacking)
            return;

        attackDelayRemaining -= Time.deltaTime;

        if (attackDelayRemaining <= 0f)
        {
            attackDelayRemaining = 0f;

            stateMachine.ChangeState(attackState);
        }
    }

    public void OnIdleExit()
    {
        Debug.Log("[Peashooter] Exit Idle", this);
    }

    // =========================================================
    // ATTACK STATE
    // =========================================================

    public void OnAttackEnter()
    {
        Debug.Log("[Peashooter] Enter Attack", this);

        if (peashooterControllerAnimation == null)
        {
            isAttacking = false;
            attackDelayRemaining = retryDelay;
            return;
        }

        isAttacking = peashooterControllerAnimation.Shoot();

        if (!isAttacking)
        {
            Debug.LogWarning(
                "[Peashooter] Không thể bắt đầu bắn. Sẽ thử lại.",
                this
            );

            attackDelayRemaining = retryDelay;
        }
    }

    public void OnAttackUpdate()
    {
        if (!isAttacking)
        {
            stateMachine.ChangeState(idleState);
        }
    }

    public void OnAttackExit()
    {
        isAttacking = false;

        Debug.Log("[Peashooter] Exit Attack", this);
    }

    // =========================================================
    // ATTACK COMPLETED EVENT
    // =========================================================

    private void HandleAttackCompleted()
    {
        if (!isAttacking)
            return;

        Debug.Log(
            "[Peashooter] Attack animation completed",
            this
        );

        isAttacking = false;

        if (hasZombie)
        {
            // Sử dụng lại delay ban đầu.
            // Tuyệt đối không random lại.
            attackDelayRemaining = initialAttackDelay;

            Debug.Log(
                $"[Peashooter] Next attack delay: " +
                $"{attackDelayRemaining:F3}s",
                this
            );
        }
        else
        {
            attackDelayRemaining = 0f;
        }

        stateMachine.ChangeState(idleState);
    }
}