using UnityEngine;
using PvZ.Data;

public class ZombieAttackState : ZombieState
{
    private const float GameTickSeconds = 0.01f;

    private readonly TriggerAttackZombie attackTrigger;
    private readonly float moveSpeed;
    private readonly ZombieStatsAsset zombieStats;

    private ZombieAttack plantChewAttack;
    private float chewTimer;

    public ZombieAttackState(
        BaseZombieReanim zombie,
        ZombieStateMachine stateMachine,
        TriggerAttackZombie attackTrigger,
        float moveSpeed,
        ZombieStatsAsset zombieStats)
        : base(zombie, stateMachine)
    {
        this.attackTrigger = attackTrigger;
        this.moveSpeed = moveSpeed;
        this.zombieStats = zombieStats;
    }

    public override void Enter()
    {
        plantChewAttack = FindPlantChewAttack();
        chewTimer = 0f;

        zombie.SetAnimationSpeed(3f);
        zombie.StartEating();
    }

    public override void Update()
    {
        if (zombie.IsDeadOrDying)
            return;

        GameObject target = attackTrigger.CurrentTargetPlant;

        // Mục tiêu hiện tại không còn hợp lệ thì quét lại,
        // phòng trường hợp còn cây khác trong vùng tấn công.
        if (!IsValidTarget(target))
        {
            attackTrigger.RecalculateTargetPlant();
            target = attackTrigger.CurrentTargetPlant;

            if (!IsValidTarget(target))
            {
                stateMachine.ChangeState(
                    new ZombieWalkState(
                        zombie,
                        stateMachine,
                        attackTrigger,
                        moveSpeed)
                );
                return;
            }
        }

        if (plantChewAttack == null ||
            !plantChewAttack.hasNumericDamage ||
            !plantChewAttack.hasInterval ||
            plantChewAttack.intervalTicks <= 0 ||
            plantChewAttack.damage <= 0f)
        {
            return;
        }

        float chewInterval =
            plantChewAttack.intervalTicks * GameTickSeconds;

        chewTimer += Time.deltaTime;

        // Bắt kịp các nhịp đã trôi qua nếu một frame bị lâu.
        while (chewTimer >= chewInterval)
        {
            chewTimer -= chewInterval;
            ApplyChewDamage();
        }
    }

    public override void Exit()
    {
        zombie.StopEating();
    }

    private void ApplyChewDamage()
    {
        GameObject target = attackTrigger.CurrentTargetPlant;

        if (!IsValidTarget(target))
            return;

        PlantHealth plantHealth = target.GetComponent<PlantHealth>();

        if (plantHealth == null || !plantHealth.IsAlive())
            return;

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt(plantChewAttack.damage)
        );

        plantHealth.TakeDamage(damage);

        // Cây vừa chết thì chọn lại cây khác nếu có.
        attackTrigger.RecalculateTargetPlant();
    }

    private ZombieAttack FindPlantChewAttack()
    {
        if (zombieStats == null || zombieStats.attacks == null)
            return null;

        foreach (ZombieAttack attack in zombieStats.attacks)
        {
            if (attack == null)
                continue;

            bool targetsPlant = string.Equals(
                attack.target,
                "plant",
                System.StringComparison.OrdinalIgnoreCase
            );

            bool isChewAttack = string.Equals(
                attack.attackType,
                "chew",
                System.StringComparison.OrdinalIgnoreCase
            );

            if (targetsPlant && isChewAttack)
                return attack;
        }

        return null;
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
            return false;

        PlantHealth plantHealth = target.GetComponent<PlantHealth>();

        return plantHealth != null && plantHealth.IsAlive();
    }
}