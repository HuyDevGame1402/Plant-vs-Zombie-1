using UnityEngine;

/// <summary>
/// Mảnh rơi (đầu bị bật ra, tay rụng): bay theo vòng cung, xoay, mờ dần rồi tự huỷ.
/// Được tạo bởi ReanimPartRenderer.SpawnDebris().
/// </summary>
public class ReanimDebris : MonoBehaviour
{
    private Vector3 velocity;
    private float gravity;
    private float angularVelocity;
    private float lifetime;
    private float fadeTime;
    private float age;
    private float groundY;
    private int groundBounces;
    private float groundLinger;
    private float groundedAge = -1f;
    private Mesh[] meshes;

    private static readonly Color32[] colorBuffer = new Color32[4];

    public void Init(
        Vector3 velocity,
        float gravity,
        float angularVelocity,
        float lifetime,
        float fadeTime,
        float groundY,
        float groundLinger,
        Mesh[] meshes)
    {
        this.velocity = velocity;
        this.gravity = gravity;
        this.angularVelocity = angularVelocity;
        this.lifetime = lifetime;
        this.fadeTime = Mathf.Max(0.01f, fadeTime);
        this.groundY = groundY;
        this.groundLinger = Mathf.Max(0f, groundLinger);
        this.meshes = meshes;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        velocity.y -= gravity * dt;
        transform.position += velocity * dt;
        transform.Rotate(0f, 0f, angularVelocity * dt, Space.Self);

        // PvZ particle ground constraint: bounce on impact, then settle.
        if (transform.position.y <= groundY && velocity.y < 0f)
        {
            Vector3 position = transform.position;
            position.y = groundY;
            transform.position = position;

            if (groundBounces < 2 && Mathf.Abs(velocity.y) > 0.55f)
            {
                velocity.y = -velocity.y * 0.28f;
                velocity.x *= 0.55f;
                angularVelocity *= 0.55f;
                groundBounces++;
            }
            else
            {
                velocity.y = 0f;
                velocity.x = 0f;
                angularVelocity = 0f;
                if (groundedAge < 0f)
                    groundedAge = 0f;
            }
        }

        if (groundedAge >= 0f)
            groundedAge += dt;

        bool removeAfterGroundLinger =
            groundedAge >= 0f &&
            groundedAge >= groundLinger + fadeTime;

        if (removeAfterGroundLinger ||
            (groundLinger <= 0f && age >= lifetime))
        {
            Destroy(gameObject);
            return;
        }

        bool shouldFade = groundedAge >= 0f
            ? groundedAge >= groundLinger
            : groundLinger <= 0f && age > lifetime - fadeTime;

        if (shouldFade && meshes != null)
        {
            float fadeProgress = groundedAge >= 0f
                ? (groundedAge - groundLinger) / fadeTime
                : (age - (lifetime - fadeTime)) / fadeTime;
            byte a = (byte)Mathf.RoundToInt(255f * (1f - Mathf.Clamp01(fadeProgress)));

            for (int i = 0; i < 4; i++)
                colorBuffer[i] = new Color32(255, 255, 255, a);

            foreach (Mesh mesh in meshes)
            {
                if (mesh != null)
                    mesh.colors32 = colorBuffer;
            }
        }
    }

    private void OnDestroy()
    {
        if (meshes == null)
            return;

        foreach (Mesh mesh in meshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
