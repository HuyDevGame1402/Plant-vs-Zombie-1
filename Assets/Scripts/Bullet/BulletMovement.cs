
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

    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------


    private BulletData bulletData;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (bulletVisual == null)
            bulletVisual = GetComponent<PeaBulletSprite>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

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

        if (currentState == BulletState.Flying)
        {
            rb.velocity = direction.normalized * speed;
        }
        else
        {
            rb.velocity = Vector2.zero;
        }
    }

    // ---------------------------------------------------------
    // START FLYING
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
        Hit(zombie, (Vector2)transform.position);
    }

    private void Hit(Transform zombie, Vector2 contactPoint)
    {
        if (currentState != BulletState.Flying)
            return;

        // PvZ đặt splat dựa trên vị trí đạn ở thời điểm impact rồi cộng
        // một offset thị giác. Dùng điểm va chạm do Unity cung cấp thay vì
        // lấy X giữa zombie và Y của sprite đạn.
        if (zombie != null)
        {
            AttachToZombie(zombie, contactPoint);
        }

        currentState = BulletState.Hit;

        ApplyState();

        OnHit?.Invoke();
        OnHitAction?.Invoke();

        // Giữ nguyên hiệu ứng Splat hiện tại.
        if (bulletVisual != null)
        {
            bulletVisual.Splat();
        }
        else
        {
            returnRoutine = StartCoroutine(ReturnAfterDelay());
        }
    }

    // ---------------------------------------------------------
    // ATTACH TO ZOMBIE
    // ---------------------------------------------------------

    private void AttachToZombie(Transform zombie, Vector2 contactPoint)
    {
        if (zombie == null)
            return;

        // Chuyển offset nhỏ theo hướng/scale của zombie, tương tự offset splat
        // tương đối với thân trong PvZ. Collider bounds giữ Y trong vùng thân.
        Collider2D bodyCollider = zombie.GetComponentInChildren<Collider2D>();
        Vector2 visualPoint = contactPoint + (Vector2)zombie.TransformVector(splatOffset);
        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            visualPoint.x = Mathf.Clamp(visualPoint.x, bounds.min.x, bounds.max.x);
            visualPoint.y = Mathf.Clamp(visualPoint.y, bounds.min.y, bounds.max.y);
        }

        float bulletWorldZ = transform.position.z;
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
        if (ProjectilePoolManager.Instance != null)
        {
            ProjectilePoolManager.Instance.ReturnToPool(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
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
    // FIND ZOMBIE ROOT
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
    // COLLISION
    // ---------------------------------------------------------

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!detectZombieCollision)
            return;

        if (currentState != BulletState.Flying)
            return;

        Transform zombie = FindZombieRoot(other.transform);

        if (zombie != null)
        {
            zombie.GetComponent<ZombieHealthRuntime>().ApplyDamage(bulletData.Damage);
            Vector2 contactPoint = other.ClosestPoint(transform.position);
            Hit(zombie, contactPoint);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!detectZombieCollision)
            return;

        if (currentState != BulletState.Flying)
            return;

        Transform zombie = FindZombieRoot(collision.transform);

        if (zombie != null)
        {
            Vector2 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (Vector2)transform.position;
            Hit(zombie, contactPoint);
        }
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
