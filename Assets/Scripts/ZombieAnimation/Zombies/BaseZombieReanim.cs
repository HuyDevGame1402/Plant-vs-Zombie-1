
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logic dùng chung cho mọi loại zombie Reanim.
/// Animation, layer, mất tay, rụng đầu, chết và mảnh rơi.
/// </summary>
public abstract class BaseZombieReanim : MonoBehaviour
{
    public enum ZombieState
    {
        Idle,
        Walk,
        Eat,
        Dying,
        Dead
    }

    [Header("Profile")]
    [SerializeField] private ZombieAnimationProfile profile;

    [Header("Layer (Lane)")]
    [SerializeField]
    private ZombieLayerManager.ZombieLane currentLane =
        ZombieLayerManager.ZombieLane.Lane1;

    [Header("Animation")]
    [SerializeField] private float animationSpeed = 1f;
    [SerializeField] private float blendDuration = 0.12f;
    [SerializeField] private bool randomWalkVariant = true;
    [SerializeField] private string idleClip = "idle";
    [SerializeField] private string walkClip = "walk";
    [SerializeField] private string walkClip2 = "walk2";
    [SerializeField] private string eatClip = "eat";

    [Tooltip("Chết khi còn đầu.")]
    [SerializeField] private string deathClip = "death";

    [Tooltip("Ngã xuống khi đã mất đầu.")]
    [SerializeField] private string deathHeadlessClip = "death2";

    [Tooltip("Thời gian chờ sau khi rụng đầu trước khi ngã.")]
    [SerializeField] private float headlessFallDelay = 2f;

    [Header("Di chuyển")]
    [SerializeField] private bool moveWhileWalking = false;
    [SerializeField] private float walkSpeed = 0.3f;

    private float requestedWalkSpeed = -1f;

    [Header("Sau khi chết")]
    [SerializeField] private bool fadeAfterDeath = true;
    [SerializeField] private float lingerTime = 1.5f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private bool destroyAfterFade = true;

    [Header("Mảnh rơi (đầu / tay)")]
    [SerializeField] private float debrisGravity = 9f;

    [Header("Máu")]
    [SerializeField] private float maxHealth = 270f;
    [SerializeField] private float armLostHealth = 135f;

    // =========================================================
    // CONSTANTS
    // =========================================================

    private const string ArmUpper2ImageId =
        "IMAGE_REANIM_ZOMBIE_OUTERARM_UPPER2";

    private const string DefaultBodyTrack = "Zombie_body";

    protected static readonly HashSet<string> CommonZombieImageIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "IMAGE_REANIM_ZOMBIE_NECK",
        "IMAGE_REANIM_ZOMBIE_BODY",
        "IMAGE_REANIM_ZOMBIE_HEAD",
        "IMAGE_REANIM_ZOMBIE_JAW",

        "IMAGE_REANIM_ZOMBIE_INNERARM_HAND",
        "IMAGE_REANIM_ZOMBIE_INNERARM_LOWER",
        "IMAGE_REANIM_ZOMBIE_INNERARM_UPPER",

        "IMAGE_REANIM_ZOMBIE_INNERLEG_FOOT",
        "IMAGE_REANIM_ZOMBIE_INNERLEG_LOWER",
        "IMAGE_REANIM_ZOMBIE_INNERLEG_UPPER",

        "IMAGE_REANIM_ZOMBIE_OUTERARM_HAND",
        "IMAGE_REANIM_ZOMBIE_OUTERARM_LOWER",
        "IMAGE_REANIM_ZOMBIE_OUTERARM_UPPER",

        "IMAGE_REANIM_ZOMBIE_OUTERLEG_FOOT",
        "IMAGE_REANIM_ZOMBIE_OUTERLEG_LOWER",
        "IMAGE_REANIM_ZOMBIE_OUTERLEG_UPPER",

        "IMAGE_REANIM_ZOMBIE_OUTERARM_HAND2",
        "IMAGE_REANIM_ZOMBIE_OUTERARM_UPPER2"
    };

    protected abstract HashSet<string> AllowedImageIds { get; }

    protected virtual ZombiePartRole GetDefaultRoles(string trackName)
    {
        switch (trackName.ToLowerInvariant())
        {
            case "anim_head1":
            case "anim_head2":
            case "anim_tongue":
            case "zombie_mustache":
            case "anim_hair":
            case "zombie_santahat":
            case "anim_cone":
            case "anim_bucket":
                return ZombiePartRole.Head;

            case "zombie_outerarm_lower":
            case "zombie_outerarm_hand":
                return ZombiePartRole.DroppedArm |
                       ZombiePartRole.RaiseWhenEating;

            case "zombie_outerarm_upper":
                return ZombiePartRole.UpperArm;

            case "zombie_body":
                return ZombiePartRole.Body;

            default:
                return ZombiePartRole.None;
        }
    }

    private struct SortEntry
    {
        public MeshRenderer renderer;
        public int normalIndex;
        public int raisedIndex;
        public bool raiseWhenEating;
    }

    // =========================================================
    // RUNTIME
    // =========================================================

    private ReanimData data;
    private ReanimAnimationPlayer animationPlayer;
    private ReanimPartRenderer partRenderer;
    private Material runtimeMaterial;

    private readonly List<string> headParts = new List<string>();
    private readonly List<string> droppedArmParts = new List<string>();
    private readonly List<string> upperArmParts = new List<string>();
    private readonly List<string> heldItemParts = new List<string>();
    private readonly List<string> heldReplacedParts = new List<string>();

    private readonly List<ReanimAnimationPlayer> extraPlayers =
        new List<ReanimAnimationPlayer>();

    private readonly List<GameObject> debrisObjects =
        new List<GameObject>();

    private SortEntry[] sortEntries;

    private string bodyTrackName = DefaultBodyTrack;
    private int referenceFrame;

    private bool isReady;
    private string chosenWalkClip = "walk";
    private ZombieState currentState = ZombieState.Idle;

    private bool armLost;
    private bool headDetached;
    private bool headlessDeathPending;
    private float headlessFallDelayRemaining;

    private float currentHealth;
    private float deadTimer;
    private bool destroyQueued;
    private bool deathLocked;

    public ZombieState State => currentState;
    public bool IsReady => isReady;
    public bool HasArm => !armLost;

    public bool IsDeadOrDying =>
        deathLocked ||
        headlessDeathPending ||
        currentState == ZombieState.Dying ||
        currentState == ZombieState.Dead;

    protected ZombieAnimationProfile Profile => profile;
    protected ReanimPartRenderer PartRenderer => partRenderer;
    protected ReanimData Data => data;
    protected int ReferenceFrame => referenceFrame;

    protected bool HeadDetached => headDetached;

    // =========================================================
    // EVENTS
    // =========================================================

    public event Action OnArmLost;
    public event Action OnDeathStarted;
    public event Action OnDeathFinished;

    // =========================================================
    // HOOKS
    // =========================================================

    protected void AddTickPlayer(ReanimAnimationPlayer player)
    {
        if (player != null && !extraPlayers.Contains(player))
            extraPlayers.Add(player);
    }

    protected virtual void OnRendererReady() { }

    protected virtual void OnTick(float dt) { }

    protected virtual void OnArmLostHook() { }

    protected virtual void OnHeadDetachedHook() { }

    protected virtual void OnResetHook() { }

    protected virtual void HandleHeldItemsOnDeath(bool headless)
    {
        DropHeldItems();
    }

    // =========================================================
    // DEATH LOCK
    // =========================================================

    protected void EnterDeathLock()
    {
        deathLocked = true;
        currentState = ZombieState.Idle;
        PlayAction("idle", idleClip);
    }

    protected void ReleaseDeathLockAndDie(bool headless)
    {
        deathLocked = false;
        StartDeath(headless);
    }

    protected bool PlayCustomDeathAnimation(string clipName)
    {
        if (!isReady ||
            animationPlayer == null ||
            string.IsNullOrEmpty(clipName) ||
            data == null ||
            !data.Clips.ContainsKey(clipName))
        {
            return false;
        }

        deathLocked = false;
        currentState = ZombieState.Dying;

        animationPlayer.Play(clipName, false);

        OnDeathStarted?.Invoke();

        return true;
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (!isReady)
            return;

        float dt = Time.deltaTime;

        animationPlayer.AnimationSpeed = animationSpeed;

        if (currentState == ZombieState.Walk &&
            data.GroundTrack != null)
        {
            animationPlayer.AnimationSpeed =
                GetGroundMotionAnimationRate();
        }

        animationPlayer.BlendDuration = blendDuration;

        float previousClipTime = animationPlayer.ClipTime;

        animationPlayer.Tick(dt);

        for (int i = 0; i < extraPlayers.Count; i++)
        {
            extraPlayers[i].AnimationSpeed =
                animationPlayer.AnimationSpeed;

            extraPlayers[i].Tick(dt);
        }

        OnTick(dt);

        // Tự động chuyển từ rụng đầu sang ngã xuống.
        if (headlessDeathPending)
        {
            headlessFallDelayRemaining -= dt;

            if (headlessFallDelayRemaining <= 0f)
            {
                FallHeadless();
            }
        }

        // Hoàn thành animation chết.
        if (currentState == ZombieState.Dying &&
            animationPlayer.IsFinished)
        {
            currentState = ZombieState.Dead;
            deadTimer = 0f;

            OnDeathFinished?.Invoke();
        }

        // Di chuyển theo Ground Track.
        if (currentState == ZombieState.Walk &&
            data.GroundTrack != null)
        {
            ApplyGroundMotion(
                previousClipTime,
                animationPlayer.ClipTime);
        }
        else if (currentState == ZombieState.Walk &&
                 moveWhileWalking)
        {
            transform.position +=
                Vector3.left * (walkSpeed * animationSpeed * dt);
        }

        partRenderer.Render();

        UpdateSorting();

        UpdateDeathFade(dt);
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    private void Initialize()
    {
        if (profile == null)
        {
            Debug.LogError("ZombieReanim: Chưa gán Profile.", this);
            return;
        }

        if (!profile.TryGetData(out data))
        {
            Debug.LogError(
                "ZombieReanim: Không thể load ReanimData.",
                this);
            return;
        }

        if (profile.AtlasTexture == null ||
            profile.SharedMaterial == null)
        {
            Debug.LogError(
                "ZombieReanim: Atlas Texture hoặc Material null.",
                this);
            return;
        }

        if (!data.Clips.ContainsKey(idleClip))
        {
            Debug.LogError(
                "ZombieReanim: Không tìm thấy clip '" +
                idleClip + "'.",
                this);
            return;
        }

        runtimeMaterial = new Material(profile.SharedMaterial);
        runtimeMaterial.mainTexture = profile.AtlasTexture;

        animationPlayer = new ReanimAnimationPlayer();

        animationPlayer.AnimationSpeed = animationSpeed;
        animationPlayer.BlendDuration = blendDuration;
        animationPlayer.Initialize(data);

        chosenWalkClip =
            randomWalkVariant &&
            UnityEngine.Random.value < 0.5f
                ? walkClip2
                : walkClip;

        animationPlayer.Play(idleClip, true);

        partRenderer = new ReanimPartRenderer();

        bool rendererInitialized = partRenderer.Initialize(
            animationPlayer,
            transform,
            runtimeMaterial,
            profile.PixelPerUnit,
            profile.GlobalScale,
            profile.AnchorToFeet,
            profile.PivotOffset,
            profile.ShadowImageId,
            profile.ShadowOffset,
            AllowedImageIds
        );

        if (!rendererInitialized)
        {
            Debug.LogError(
                "ZombieReanim: Renderer khởi tạo thất bại.",
                this);

            Cleanup();
            return;
        }

        referenceFrame = data.Clips[idleClip].start;
        currentHealth = maxHealth;

        OnRendererReady();

        BuildPartRoles();
        ApplyInitialPartState();

        currentState = ZombieState.Idle;
        isReady = true;

        UpdateSorting();

        Debug.Log(
            "ZombieReanim: Khởi tạo thành công. Part count = " +
            partRenderer.PartCount,
            this);
    }

    // =========================================================
    // BUILD PART ROLES
    // =========================================================

    private void BuildPartRoles()
    {
        headParts.Clear();
        droppedArmParts.Clear();
        upperArmParts.Clear();
        heldItemParts.Clear();
        heldReplacedParts.Clear();

        bodyTrackName = DefaultBodyTrack;

        IReadOnlyList<string> names = partRenderer.PartNames;

        ZombiePartRole[] roles =
            new ZombiePartRole[names.Count];

        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];

            roles[i] = GetDefaultRoles(name);

            if (profile != null)
                roles[i] |= profile.GetPartRoles(name);

            if ((roles[i] & ZombiePartRole.Head) != 0)
                headParts.Add(name);

            if ((roles[i] & ZombiePartRole.DroppedArm) != 0)
                droppedArmParts.Add(name);

            if ((roles[i] & ZombiePartRole.HeldItem) != 0)
                heldItemParts.Add(name);

            if ((roles[i] & ZombiePartRole.HeldItemReplaces) != 0)
                heldReplacedParts.Add(name);

            if ((roles[i] & ZombiePartRole.UpperArm) != 0)
                upperArmParts.Add(name);

            if ((roles[i] & ZombiePartRole.Body) != 0)
                bodyTrackName = name;
        }

        sortEntries = new SortEntry[names.Count];

        int index = 0;

        // Các bộ phận không phải đầu.
        for (int i = 0; i < names.Count; i++)
        {
            if ((roles[i] & ZombiePartRole.Head) == 0)
                sortEntries[i].normalIndex = index++;
        }

        // Đầu nằm trên cùng.
        for (int i = 0; i < names.Count; i++)
        {
            if ((roles[i] & ZombiePartRole.Head) != 0)
                sortEntries[i].normalIndex = index++;
        }

        for (int i = 0; i < names.Count; i++)
        {
            sortEntries[i].renderer =
                partRenderer.GetPartRenderer(names[i]);

            bool raise =
                (roles[i] & ZombiePartRole.RaiseWhenEating) != 0;

            sortEntries[i].raiseWhenEating = raise;

            sortEntries[i].raisedIndex = raise
                ? index++
                : sortEntries[i].normalIndex;
        }
    }

    private void ApplyInitialPartState()
    {
        foreach (string name in heldReplacedParts)
            partRenderer.SetPartHidden(name, true);
    }

    // =========================================================
    // HELD ITEMS
    // =========================================================

    protected void DropHeldItems()
    {
        DropHeldItems(
            new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f),
                UnityEngine.Random.Range(1.8f, 2.4f),
                0f),
            UnityEngine.Random.Range(-250f, 250f));
    }

    protected void DropHeldItems(
        Vector3 velocity,
        float angularVelocity)
    {
        if (heldItemParts.Count == 0)
            return;

        SpawnDebris(
            heldItemParts,
            velocity,
            angularVelocity,
            1.3f,
            0.5f);

        foreach (string name in heldItemParts)
            partRenderer.SetPartHidden(name, true);

        foreach (string name in heldReplacedParts)
            partRenderer.SetPartHidden(name, false);
    }

    // =========================================================
    // SORTING
    // =========================================================

    private void UpdateSorting()
    {
        if (sortEntries == null)
            return;

        bool eating = currentState == ZombieState.Eat;

        float x = transform.position.x;

        ZombieLayerManager layerManager =
            ZombieLayerManager.Instance;

        for (int i = 0; i < sortEntries.Length; i++)
        {
            SortEntry entry = sortEntries[i];

            if (entry.renderer == null)
                continue;

            int index =
                eating && entry.raiseWhenEating
                    ? entry.raisedIndex
                    : entry.normalIndex;

            entry.renderer.sortingOrder =
                layerManager != null
                    ? layerManager.GetSortingOrder(
                        currentLane, index, x)
                    : ((int)currentLane * 1000) + index;
        }

        partRenderer.SetShadowSortingOrder(
            layerManager != null
                ? layerManager.GetShadowSortingOrder(currentLane)
                : ((int)currentLane * 1000) - 10);
    }

    // =========================================================
    // PLAY ACTION
    // =========================================================

    private void PlayAction(
        string actionName,
        string fallbackClip,
        bool loop = true)
    {
        if (!isReady || animationPlayer == null)
            return;

        string clipName = fallbackClip;

        if (profile != null)
        {
            string extensionClip;

            if (profile.TryGetAnimationClip(
                actionName,
                out extensionClip) &&
                !string.IsNullOrEmpty(extensionClip))
            {
                clipName = extensionClip;
            }
        }

        animationPlayer.Play(clipName, loop);
    }

    // =========================================================
    // MOVEMENT / EATING
    // =========================================================

    public void Idle()
    {
        if (!isReady || IsDeadOrDying)
            return;

        currentState = ZombieState.Idle;

        PlayAction("idle", idleClip);
    }

    public void Walk()
    {
        if (!isReady || IsDeadOrDying)
            return;

        currentState = ZombieState.Walk;

        PlayAction("walk", chosenWalkClip);
    }

    public void SetWalkMotionSpeed(float speed)
    {
        requestedWalkSpeed = Mathf.Max(0f, speed);
    }

    public bool HasGroundMotion =>
        data != null && data.GroundTrack != null;

    private float GetGroundMotionAnimationRate()
    {
        ReanimData.ReanimClip clip =
            animationPlayer.CurrentClip;

        if (clip == null || clip.Length <= 0)
            return animationSpeed;

        float stride = GetGroundStride(clip);

        float scale =
            profile.GlobalScale.x / profile.PixelPerUnit;

        float speedAtRateOne =
            Mathf.Abs(stride) * scale * data.Fps / clip.Length;

        float targetSpeed =
            requestedWalkSpeed >= 0f
                ? requestedWalkSpeed
                : walkSpeed;

        if (speedAtRateOne <= 0.0001f)
            return animationSpeed;

        return animationSpeed * targetSpeed / speedAtRateOne;
    }

    private float GetGroundStride(ReanimData.ReanimClip clip)
    {
        return SampleGroundX(clip.end) -
               SampleGroundX(clip.start);
    }

    private float SampleGroundX(float frame)
    {
        ReanimData.ReanimTrack track = data.GroundTrack;

        if (track == null || track.frames.Length == 0)
            return 0f;

        int a = Mathf.Clamp(
            Mathf.FloorToInt(frame),
            0,
            track.frames.Length - 1);

        int b = Mathf.Min(a + 1, track.frames.Length - 1);

        return Mathf.Lerp(
            track.frames[a].x,
            track.frames[b].x,
            frame - a);
    }

    private float GetUnwrappedGroundX(
        float elapsedFrames,
        ReanimData.ReanimClip clip)
    {
        float length = Mathf.Max(1f, clip.Length);

        float loops = Mathf.Floor(elapsedFrames / length);

        float frameInLoop =
            Mathf.Repeat(elapsedFrames, length);

        float startX = SampleGroundX(clip.start);

        float stride = GetGroundStride(clip);

        return loops * stride +
               SampleGroundX(clip.start + frameInLoop) -
               startX;
    }

    private void ApplyGroundMotion(
        float previousTime,
        float currentTime)
    {
        ReanimData.ReanimClip clip =
            animationPlayer.CurrentClip;

        if (clip == null || clip.Length <= 0)
            return;

        float scale =
            profile.GlobalScale.x / profile.PixelPerUnit;

        float previous = GetUnwrappedGroundX(
            previousTime * data.Fps, clip);

        float current = GetUnwrappedGroundX(
            currentTime * data.Fps, clip);

        float delta = current - previous;

        Transform movementRoot =
            transform.parent != null
                ? transform.parent
                : transform;

        movementRoot.position +=
            Vector3.left * (delta * scale);
    }

    public void SetWalkVariant(bool second)
    {
        chosenWalkClip = second ? walkClip2 : walkClip;

        if (currentState == ZombieState.Walk)
            Walk();
    }

    public void StartEating()
    {
        if (!isReady || IsDeadOrDying)
            return;

        currentState = ZombieState.Eat;

        PlayAction("eat", eatClip);
    }

    public void StopEating()
    {
        if (currentState == ZombieState.Eat)
            Walk();
    }

    // =========================================================
    // 1. LOSE ARM - RỤNG TAY
    // =========================================================

    /// <summary>
    /// Rụng cẳng tay và bàn tay ngoài.
    /// Bắp tay chuyển thành tay cụt.
    /// Không reset animation hiện tại.
    /// </summary>
    public void LoseArm()
    {
        if (!isReady || armLost || IsDeadOrDying)
            return;

        armLost = true;

        SpawnDebris(
            droppedArmParts,
            new Vector3(
                UnityEngine.Random.Range(-0.55f, 0.25f),
                UnityEngine.Random.Range(2.4f, 3.0f),
                0f),
            UnityEngine.Random.Range(-240f, 240f),
            1.25f,
            0.4f);

        foreach (string name in droppedArmParts)
            partRenderer.SetPartHidden(name, true);

        foreach (string name in upperArmParts)
        {
            partRenderer.SetPartImageOverride(
                name,
                ArmUpper2ImageId);
        }

        OnArmLostHook();

        OnArmLost?.Invoke();
    }

    // =========================================================
    // 2. DETACH HEAD - RỤNG ĐẦU
    // =========================================================

    /// <summary>
    /// Chỉ thực hiện hiệu ứng rụng đầu.
    /// Không chạy animation ngã.
    /// Không tự động thay đổi trạng thái Idle / Walk / Eat.
    /// </summary>
    public void DetachHead()
    {
        if (!isReady ||
            headDetached ||
            currentState == ZombieState.Dying ||
            currentState == ZombieState.Dead ||
            deathLocked)
        {
            return;
        }

        headDetached = true;

        SpawnDebris(
            headParts,
            new Vector3(
                -UnityEngine.Random.Range(0.9f, 1.4f),
                UnityEngine.Random.Range(4.2f, 5.0f),
                0f),
            UnityEngine.Random.Range(-420f, 420f),
            1.35f,
            0.45f,
            1f);

        foreach (string name in headParts)
            partRenderer.SetPartHidden(name, true);

        OnHeadDetachedHook();
    }

    // =========================================================
    // 3. FALL HEADLESS - NGÃ XUỐNG KHÔNG ĐẦU
    // =========================================================

    /// <summary>
    /// Chạy animation ngã xuống khi zombie đã rụng đầu.
    /// Không thực hiện hiệu ứng rụng đầu lần nữa.
    /// </summary>
    public void FallHeadless()
    {
        if (!isReady)
            return;

        if (!headDetached)
        {
            Debug.LogWarning(
                "ZombieReanim: Phải rụng đầu trước khi ngã.",
                this);

            return;
        }

        if (currentState == ZombieState.Dying ||
            currentState == ZombieState.Dead ||
            deathLocked)
        {
            return;
        }

        // Ghi nhận xem sự kiện chết đã được thông báo chưa.
        bool alreadyNotified = headlessDeathPending;

        headlessDeathPending = false;
        headlessFallDelayRemaining = 0f;

        // Các bộ phận đầu và vật cầm đã được xử lý trước đó.
        StartDeath(
            true,
            true,
            !alreadyNotified);
    }

    // =========================================================
    // DEATH API
    // =========================================================

    /// <summary>
    /// Chết khi còn đầu.
    /// </summary>
    public void Die()
    {
        BeginDeath(false);
    }

    /// <summary>
    /// Chết không đầu.
    /// Rụng đầu trước, chờ headlessFallDelay rồi ngã.
    /// </summary>
    public virtual void DieHeadless()
    {
        BeginDeath(true);
    }

    private void BeginDeath(bool headless)
    {
        if (!isReady || IsDeadOrDying)
            return;

        if (headless)
        {
            // Giai đoạn 1: Rụng đầu.
            DetachHead();

            // Xử lý vật đang cầm.
            HandleHeldItemsOnDeath(true);

            // Chờ trước khi ngã.
            headlessDeathPending = true;
            headlessFallDelayRemaining = headlessFallDelay;

            OnDeathStarted?.Invoke();

            // Nếu không cần chờ thì ngã ngay.
            if (headlessFallDelay <= 0f)
                FallHeadless();

            return;
        }

        StartDeath(false);
    }

    /// <summary>
    /// Bắt đầu animation ngã.
    /// </summary>
    private void StartDeath(
        bool headless,
        bool deathPartsAlreadyHandled = false,
        bool notifyDeathStarted = true)
    {
        if (!deathPartsAlreadyHandled)
        {
            if (headless || headDetached)
            {
                headless = true;
                DetachHead();
            }
            else
            {
                // Đầu dính theo thân khi chết còn đầu.
                partRenderer.SetAttachment(
                    headParts,
                    bodyTrackName,
                    referenceFrame);
            }

            HandleHeldItemsOnDeath(headless);
        }

        currentState = ZombieState.Dying;

        if (headless)
        {
            PlayAction(
                "deathHeadless",
                deathHeadlessClip,
                false);
        }
        else
        {
            PlayAction(
                "death",
                deathClip,
                false);
        }

        if (notifyDeathStarted)
            OnDeathStarted?.Invoke();
    }

    // =========================================================
    // HEALTH
    // =========================================================

    public void TakeDamage(
        float damage,
        bool removeHead = false)
    {
        if (!isReady || IsDeadOrDying)
            return;

        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            if (removeHead)
                DieHeadless();
            else
                Die();

            return;
        }

        if (!armLost && currentHealth <= armLostHealth)
            LoseArm();
    }

    // =========================================================
    // RESET ZOMBIE
    // =========================================================

    public void ResetZombie()
    {
        if (!isReady)
            return;

        foreach (GameObject go in debrisObjects)
        {
            if (go != null)
                Destroy(go);
        }

        debrisObjects.Clear();

        partRenderer.ClearOverrides();
        partRenderer.SetAlpha(1f);

        ApplyInitialPartState();

        armLost = false;
        headDetached = false;

        headlessDeathPending = false;
        headlessFallDelayRemaining = 0f;

        deathLocked = false;
        destroyQueued = false;

        deadTimer = 0f;
        currentHealth = maxHealth;

        currentState = ZombieState.Idle;

        PlayAction("idle", idleClip);

        OnResetHook();
    }

    // =========================================================
    // DEBRIS
    // =========================================================

    protected void SpawnDebris(
        IList<string> trackNames,
        Vector3 velocity,
        float angularVelocity,
        float lifetime,
        float fadeTime,
        float groundLinger = 0f)
    {
        GameObject debris = partRenderer.SpawnDebris(
            trackNames,
            velocity,
            debrisGravity,
            angularVelocity,
            lifetime,
            fadeTime,
            groundLinger);

        if (debris != null)
            debrisObjects.Add(debris);
    }

    // =========================================================
    // DEATH FADE
    // =========================================================

    private void UpdateDeathFade(float dt)
    {
        if (currentState != ZombieState.Dead ||
            !fadeAfterDeath)
        {
            return;
        }

        deadTimer += dt;

        if (deadTimer <= lingerTime)
            return;

        float k = fadeDuration <= 0f
            ? 1f
            : Mathf.Clamp01(
                (deadTimer - lingerTime) / fadeDuration);

        partRenderer.SetAlpha(1f - k);

        if (k >= 1f &&
            destroyAfterFade &&
            !destroyQueued)
        {
            destroyQueued = true;
            Destroy(gameObject);
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void Cleanup()
    {
        isReady = false;

        if (partRenderer != null)
        {
            partRenderer.Dispose();
            partRenderer = null;
        }

        animationPlayer = null;

        extraPlayers.Clear();

        sortEntries = null;

        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);

            runtimeMaterial = null;
        }
    }
}
