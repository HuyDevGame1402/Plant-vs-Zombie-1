using PvZ.Data;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public class BulletMovement : MonoBehaviour
{
    // ---------------------------------------------------------
    // STATE
    // ---------------------------------------------------------

    public enum BulletState
    {
        Idle,
        Flying,
        Hit
    }

    [Header("State")]
    [SerializeField] private BulletState currentState = BulletState.Idle;

    public BulletState CurrentState => currentState;

    // ---------------------------------------------------------
    // RIGIDBODY
    // ---------------------------------------------------------

    private Rigidbody2D rb;
    private CircleCollider2D circle;

    // ---------------------------------------------------------
    // MOVEMENT
    // ---------------------------------------------------------

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private Vector2 direction = Vector2.right;

    // ---------------------------------------------------------
    // COLLISION
    // ---------------------------------------------------------

    [Header("Collision")]
    [SerializeField] private string zombieTag = "Zombie";
    [SerializeField] private bool detectZombieCollision = true;

    [Tooltip("Quét đường đi của đạn mỗi FixedUpdate để chống xuyên zombie.")]
    [SerializeField] private bool useSweepCheck = true;

    [Tooltip("Dịch điểm splat theo tọa độ local của Zombie, tính bằng world units.")]
    [SerializeField] private Vector2 splatOffset = new Vector2(-0.03f, 0.08f);

    // ---------------------------------------------------------
    // VISUAL
    // ---------------------------------------------------------

    [Header("Visual")]
    [SerializeField] private PeaBulletSprite bulletVisual;

    // ---------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------

    [Header("Events")]
    public UnityEvent OnStartFlying;
    public UnityEvent OnHit;

    public event Action OnStartFlyingAction;
    public event Action OnHitAction;

    // ---------------------------------------------------------
    // RUNTIME
    // ---------------------------------------------------------

    private Coroutine returnRoutine;
    private BulletData bulletData;

    // Buffer cho sweep (không tạo rác mỗi frame)
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[16];

    // Parent trước khi gắn vào zombie, để trả lại khi reset/pool
    private Transform parentBeforeAttach;
    private bool attachedToZombie;

    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circle = GetComponent<CircleCollider2D>();

        if (bulletVisual == null)
            bulletVisual = GetComponent<PeaBulletSprite>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.velocity = Vector2.zero;

        if (bulletVisual != null)
        {
            bulletVisual.OnSplatCompleted -= HandleSplatCompleted;
            bulletVisual.OnSplatCompleted += HandleSplatCompleted;
        }

        bulletData = GetComponent<BulletData>();
    }

    private void OnDestroy()
    {
        if (bulletVisual != null)
            bulletVisual.OnSplatCompleted -= HandleSplatCompleted;
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        if (currentState != BulletState.Flying)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector2 dir = direction.normalized;

        // Quét quãng đường sắp đi để không bị tunneling.
        // Mask ~0 = mọi layer (kể cả Ignore Raycast); lọc zombie bằng TAG.
        if (useSweepCheck && detectZombieCollision && circle != null)
        {
            float dist = speed * Time.fixedDeltaTime;
            Vector2 origin = rb.position + (Vector2)transform.TransformVector(circle.offset);
            float radius = circle.radius * Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));

            int count = Physics2D.CircleCastNonAlloc(
                origin, radius, dir, sweepHits, dist, ~0);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = sweepHits[i].collider;

                if (col == null || col == circle)
                    continue;

                if (TryHitZombie(col, sweepHits[i].point))
                    return; // đã trúng, không di chuyển tiếp
            }
        }

        rb.velocity = dir * speed;
    }

    // ---------------------------------------------------------
    // START / STOP FLYING
    // ---------------------------------------------------------

    public void StartFlying()
    {
        if (currentState != BulletState.Idle)
            return;

        currentState = BulletState.Flying;

        ApplyState();

        OnStartFlying?.Invoke();
        OnStartFlyingAction?.Invoke();
    }

    public void StopFlying()
    {
        if (currentState != BulletState.Flying)
            return;

        currentState = BulletState.Idle;

        ApplyState();
    }

    // ---------------------------------------------------------
    // HIT
    // ---------------------------------------------------------

    public void Hit()
    {
        Hit(null);
    }

    public void Hit(Transform zombie)
    {
        Vector2 point = zombie != null
            ? ComputeVisualPoint(zombie, null, transform.position)
            : (Vector2)transform.position;

        Hit(zombie, point);
    }

    // visualPoint: điểm splat ĐÃ tính xong (đã cộng splatOffset và clamp)
    private void Hit(Transform zombie, Vector2 visualPoint)
    {
        if (currentState != BulletState.Flying)
            return;

        if (zombie != null)
            AttachToZombie(zombie, visualPoint);

        currentState = BulletState.Hit;

        ApplyState();

        OnHit?.Invoke();
        OnHitAction?.Invoke();

        if (bulletVisual != null)
            bulletVisual.Splat();
        else
            returnRoutine = StartCoroutine(ReturnAfterDelay());
    }

    // ---------------------------------------------------------
    // ATTACH TO ZOMBIE
    // ---------------------------------------------------------

    private Vector2 ComputeVisualPoint(Transform zombie, Collider2D hitCollider, Vector2 contactPoint)
    {
        Vector2 visualPoint = contactPoint + (Vector2)zombie.TransformVector(splatOffset);

        // Ưu tiên collider vừa trúng; chỉ clamp khi collider còn bật (bounds hợp lệ).
        Collider2D bodyCollider = hitCollider;
        if (bodyCollider == null || !bodyCollider.enabled)
            bodyCollider = FindEnabledZombieCollider(zombie);

        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            visualPoint.x = Mathf.Clamp(visualPoint.x, bounds.min.x, bounds.max.x);
            visualPoint.y = Mathf.Clamp(visualPoint.y, bounds.min.y, bounds.max.y);
        }

        return visualPoint;
    }

    private Collider2D FindEnabledZombieCollider(Transform zombie)
    {
        Collider2D[] cols = zombie.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i].enabled && cols[i] != circle)
                return cols[i];
        }
        return null;
    }

    private void AttachToZombie(Transform zombie, Vector2 visualPoint)
    {
        if (zombie == null)
            return;

        float bulletWorldZ = transform.position.z;

        parentBeforeAttach = transform.parent;
        attachedToZombie = true;

        transform.SetParent(zombie, true);
        transform.position = new Vector3(visualPoint.x, visualPoint.y, bulletWorldZ);
    }

    // ---------------------------------------------------------
    // SPLAT COMPLETED
    // ---------------------------------------------------------

    private void HandleSplatCompleted()
    {
        if (currentState != BulletState.Hit)
            return;

        ReturnToPool();
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(0.25f);

        returnRoutine = null;

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        // Trả parent trước khi vào pool để không bị Destroy theo zombie.
        RestoreParent();

        if (ProjectilePoolManager.Instance != null)
        {
            ProjectilePoolManager.Instance.ReturnToPool(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void RestoreParent()
    {
        if (!attachedToZombie)
            return;

        attachedToZombie = false;

        // parentBeforeAttach có thể là null (root) hoặc pool root.
        transform.SetParent(parentBeforeAttach, true);
        parentBeforeAttach = null;
    }

    // ---------------------------------------------------------
    // RESET
    // ---------------------------------------------------------

    public void ResetBullet()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        RestoreParent();

        currentState = BulletState.Idle;

        ApplyState();
    }

    public void PrepareForPool()
    {
        ResetBullet();

        if (bulletVisual != null)
            bulletVisual.ResetVisual();
    }

    // ---------------------------------------------------------
    // APPLY STATE
    // ---------------------------------------------------------

    private void ApplyState()
    {
        if (rb == null)
            return;

        switch (currentState)
        {
            case BulletState.Idle:
            case BulletState.Hit:
                rb.velocity = Vector2.zero;
                break;

            case BulletState.Flying:
                rb.velocity = direction.normalized * speed;
                break;
        }
    }

    // ---------------------------------------------------------
    // FIND ZOMBIE ROOT (lọc bằng TAG, không dùng layer)
    // ---------------------------------------------------------

    private Transform FindZombieRoot(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if (current.CompareTag(zombieTag))
                return current;

            current = current.parent;
        }

        return null;
    }

    // ---------------------------------------------------------
    // HIT HANDLING (dùng chung cho sweep + trigger + collision)
    // ---------------------------------------------------------

    private bool TryHitZombie(Collider2D other, Vector2 contactPoint)
    {
        if (currentState != BulletState.Flying)
            return false;

        // Chỉ collider hitbox có tag Zombie mới nhận đạn.
        // Attack trigger (không tag) bị bỏ qua, đạn bay xuyên qua nó.
        if (!other.CompareTag(zombieTag))
            return false;

        Transform zombie = FindZombieRoot(other.transform);
        if (zombie == null)
            return false;


        // QUAN TRỌNG: tính điểm splat khi collider zombie còn bật,
        // trước khi ApplyDamage (viên cuối có thể giết zombie và tắt collider).
        Vector2 visualPoint = ComputeVisualPoint(zombie, other, contactPoint);

        ZombieHealthRuntime health = zombie.GetComponent<ZombieHealthRuntime>();
        if (health == null)
            health = zombie.GetComponentInChildren<ZombieHealthRuntime>();

        if (health != null && bulletData != null)
            health.ApplyDamage(bulletData.Damage);
        else
            Debug.LogWarning(
                "BulletMovement: Zombie '" + zombie.name +
                "' thiếu ZombieHealthRuntime hoặc đạn thiếu BulletData.", zombie);

        Hit(zombie, visualPoint);
        return true;
    }

    // ---------------------------------------------------------
    // COLLISION
    // ---------------------------------------------------------

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!detectZombieCollision)
            return;

        TryHitZombie(other, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!detectZombieCollision)
            return;

        Vector2 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)transform.position;

        TryHitZombie(collision.collider, contactPoint);
    }

    // ---------------------------------------------------------
    // CONFIGURATION
    // ---------------------------------------------------------

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed);

        if (currentState == BulletState.Flying)
            ApplyState();
    }

    public void SetDirection(Vector2 newDirection)
    {
        if (newDirection.sqrMagnitude <= 0.0001f)
            return;

        direction = newDirection.normalized;

        if (currentState == BulletState.Flying)
            ApplyState();
    }

    public void SetSpeedAndDirection(
        float newSpeed,
        Vector2 newDirection
    )
    {
        SetSpeed(newSpeed);
        SetDirection(newDirection);
    }
}