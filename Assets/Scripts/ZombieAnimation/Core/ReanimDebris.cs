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
    private Mesh[] meshes;

    private static readonly Color32[] colorBuffer = new Color32[4];

    public void Init(
        Vector3 velocity,
        float gravity,
        float angularVelocity,
        float lifetime,
        float fadeTime,
        Mesh[] meshes)
    {
        this.velocity = velocity;
        this.gravity = gravity;
        this.angularVelocity = angularVelocity;
        this.lifetime = lifetime;
        this.fadeTime = Mathf.Max(0.01f, fadeTime);
        this.meshes = meshes;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        velocity.y -= gravity * dt;
        transform.localPosition += velocity * dt;
        transform.Rotate(0f, 0f, angularVelocity * dt, Space.Self);

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (age > lifetime - fadeTime && meshes != null)
        {
            byte a = (byte)Mathf.RoundToInt(
                255f * Mathf.Clamp01((lifetime - age) / fadeTime));

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