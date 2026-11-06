
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
        if (currentState != BulletState.Flying)
            return;

        currentState = BulletState.Hit;

        ApplyState();

        OnHit?.Invoke();
        OnHitAction?.Invoke();

        if (bulletVisual != null)
        {
            bulletVisual.Splat();
        }
        else
        {
            returnRoutine = StartCoroutine(ReturnAfterDelay());
        }
    }

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
    // COLLISION
    // ---------------------------------------------------------

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!detectZombieCollision)
            return;

        if (currentState != BulletState.Flying)
            return;

        if (other.CompareTag(zombieTag))
        {
            Hit();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!detectZombieCollision)
            return;

        if (currentState != BulletState.Flying)
            return;

        if (collision.collider.CompareTag(zombieTag))
        {
            Hit();
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
