using System.Collections.Generic;
using PvZ.Data;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TriggerAttackZombie : MonoBehaviour
{
    [SerializeField] private string plantTag = "Plant";
    [SerializeField] private ZombieHealthRuntime zombieHealth;

    [Tooltip("Khoảng thời gian giữa các lần kiểm tra lại vùng trigger.")]
    [SerializeField, Min(0.02f)] private float rescanInterval = 0.1f;

    [SerializeField] private GameObject currentTargetPlant;
    public GameObject CurrentTargetPlant => currentTargetPlant;

    private Collider2D attackTrigger;
    private readonly List<Collider2D> overlappingColliders = new();
    private readonly HashSet<Plant> plantsInRange = new();

    private float rescanTimer;

    private void Awake()
    {
        attackTrigger = GetComponent<Collider2D>();
        attackTrigger.isTrigger = true;
    }

    private void OnEnable()
    {
        RescanPlantsInRange();
    }

    private void Update()
    {
        rescanTimer += Time.deltaTime;

        if (rescanTimer >= rescanInterval)
        {
            rescanTimer = 0f;
            RescanPlantsInRange();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Cập nhật mục tiêu sớm khi cây vừa vào vùng.
        RescanPlantsInRange();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Không xóa thẳng cây từ sự kiện Exit: một cây có thể có nhiều collider.
        // Quét lại sẽ xác định cây nào thực sự không còn trong vùng.
        RescanPlantsInRange();
    }

    private void OnDisable()
    {
        plantsInRange.Clear();
        overlappingColliders.Clear();
        currentTargetPlant = null;
    }

    public void RecalculateTargetPlant()
    {
        RescanPlantsInRange();
    }

    private void RescanPlantsInRange()
    {
        plantsInRange.Clear();
        overlappingColliders.Clear();

        if (attackTrigger == null || !attackTrigger.enabled || !gameObject.activeInHierarchy)
        {
            currentTargetPlant = null;
            return;
        }

        // Lấy lại toàn bộ collider đang chồng với vùng tấn công.
        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };

        attackTrigger.OverlapCollider(filter, overlappingColliders);

        foreach (Collider2D hit in overlappingColliders)
        {
            if (hit == null || !hit.CompareTag(plantTag))
                continue;

            Plant plant = hit.GetComponentInParent<Plant>();
            if (IsValidTarget(plant))
            {
                // HashSet loại bỏ trùng nếu một cây có nhiều collider.
                plantsInRange.Add(plant);
            }
        }

        currentTargetPlant = FindHighestPriorityPlant();
    }

    private bool IsValidTarget(Plant plant)
    {
        if (plant == null || !plant.gameObject.activeInHierarchy)
            return false;

        PlantHealth health = plant.GetComponent<PlantHealth>();
        if (health == null || !health.IsAlive())
            return false;

        CampaignZombie campaignZombie = transform.parent != null
            ? transform.parent.GetComponent<CampaignZombie>()
            : null;

        if (campaignZombie == null)
            return false;

        return plant.LaneIndex == campaignZombie.LaneIndex;
    }

    private GameObject FindHighestPriorityPlant()
    {
        Plant bestPlant = null;

        foreach (Plant plant in plantsInRange)
        {
            // Cây có EatPriority lớn hơn được chọn trước.
            if (bestPlant == null || plant.EatPriority > bestPlant.EatPriority)
            {
                bestPlant = plant;
            }
        }

        return bestPlant != null ? bestPlant.gameObject : null;
    }

    private bool IsZombieAliveAndHasHead()
    {
        // HasHead() chỉ kiểm tra zombie còn đầu hay không.
        // Nếu ZombieHealthRuntime có IsAlive(), hãy kết hợp thêm điều kiện đó tại đây.
        return zombieHealth != null && zombieHealth.HasHead();
    }

    private bool IsZombieInNormalState()
    {
        // Thay bằng kiểm tra trạng thái thật khi bạn triển khai choáng, bay, v.v.
        return true;
    }
}