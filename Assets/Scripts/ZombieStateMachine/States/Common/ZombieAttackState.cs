using UnityEngine;

public class ZombieAttackState : ZombieState
{
    private readonly TriggerAttackZombie attackTrigger;
    private readonly float moveSpeed;

    public ZombieAttackState(
        BaseZombieReanim zombie,
        ZombieStateMachine stateMachine,
        TriggerAttackZombie attackTrigger,
        float moveSpeed)
        : base(zombie, stateMachine)
    {
        this.attackTrigger = attackTrigger;
        this.moveSpeed = moveSpeed;
    }

    public override void Enter()
    {
        zombie.SetAnimationSpeed(2.5f);
        zombie.StartEating();
    }

    public override void Update()
    {
        if (zombie.IsDeadOrDying)
            return;

        GameObject target = attackTrigger.CurrentTargetPlant;

        if (IsValidTarget(target))
            return;

        // Mục tiêu đã chết, bị tắt hoặc không còn hợp lệ.
        // Quét lại để chọn cây khác còn trong vùng nếu có.
        attackTrigger.RecalculateTargetPlant();

        target = attackTrigger.CurrentTargetPlant;

        if (IsValidTarget(target))
            return;

        stateMachine.ChangeState(
            new ZombieWalkState(zombie, stateMachine, attackTrigger, moveSpeed)
        );
    }

    public override void Exit()
    {
        zombie.StopEating();
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
            return false;

        PlantHealth health = target.GetComponent<PlantHealth>();
        return health != null && health.IsAlive();
    }
}