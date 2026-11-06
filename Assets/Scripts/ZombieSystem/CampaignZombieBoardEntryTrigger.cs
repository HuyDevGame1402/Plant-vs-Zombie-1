
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CampaignZombieBoardEntryTrigger : MonoBehaviour
{
    [SerializeField] private CampaignZombieBoardRegistry registry;

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(
            $"[EntryTrigger] TRIGGER ENTERED! " +
            $"Object: {other.name} | Tag: {other.tag}",
            other.gameObject
        );

        if (registry == null)
        {
            Debug.LogError(
                "[EntryTrigger] Registry is NULL!",
                this
            );
            return;
        }

        Debug.Log("[EntryTrigger] Registry reference is valid.");

        CampaignZombie zombie =
            other.GetComponentInParent<CampaignZombie>();

        if (zombie == null)
        {
            Debug.LogWarning(
                $"[EntryTrigger] No CampaignZombie found on {other.name} or its parents.",
                other.gameObject
            );
            return;
        }

        Debug.Log(
            $"[EntryTrigger] Found CampaignZombie: {zombie.name} | Lane: {zombie.LaneIndex}",
            zombie.gameObject
        );

        bool registered = registry.RegisterZombieEnteredBoard(zombie);

        Debug.Log(
            $"[EntryTrigger] Registration result: {registered}",
            zombie.gameObject
        );
    }
}
