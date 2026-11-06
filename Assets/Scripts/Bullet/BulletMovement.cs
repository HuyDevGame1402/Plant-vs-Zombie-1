
using System;
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

    [Tooltip("Tốc độ bay của đạn")]
    [SerializeField] private float speed = 5f;

    [Tooltip("Hướng bay: (1,0) sang phải, (-1,0) sang trái")]
    [SerializeField] private Vector2 direction = Vector2.right;

    // ---------------------------------------------------------
    // COLLISION
    // ---------------------------------------------------------

    [Header("Collision")]

    [SerializeField] private string zombieTag = "Zombie";

    [SerializeField] private bool detectZombieCollision = true;

    // ---------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------

    [Header("Events")]

    [Tooltip("Gọi một lần khi bắt đầu bay")]
    public UnityEvent OnStartFlying;

    [Tooltip("Gọi một lần khi va chạm")]
    public UnityEvent OnHit;

    public event Action OnStartFlyingAction;
    public event Action OnHitAction;

    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        rb.velocity = Vector2.zero;
    }

    private void Start()
    {
        ApplyState();
    }

    // ---------------------------------------------------------
    // MOVEMENT
    // ---------------------------------------------------------

    private void FixedUpdate()
    {
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
    // STATE CONTROL
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

    public void Hit()
    {
        if (currentState == BulletState.Hit)
            return;

        currentState = BulletState.Hit;

        ApplyState();

        OnHit?.Invoke();
        OnHitAction?.Invoke();
    }

    public void ResetBullet()
    {
        currentState = BulletState.Idle;

        ApplyState();
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
    // COLLISION 2D
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
    }

    public void SetDirection(Vector2 newDirection)
    {
        if (newDirection.sqrMagnitude <= 0.0001f)
            return;

        direction = newDirection.normalized;

        if (currentState == BulletState.Flying)
        {
            ApplyState();
        }
    }

    public void SetSpeedAndDirection(float newSpeed, Vector2 newDirection)
    {
        SetSpeed(newSpeed);
        SetDirection(newDirection);
    }
}
