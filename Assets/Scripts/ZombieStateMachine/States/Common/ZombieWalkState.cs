using UnityEngine;

public class ZombieWalkState : ZombieState
{
    private readonly TriggerAttackZombie attackTrigger;
    private readonly float moveSpeed;

    public ZombieWalkState(
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
        zombie.SetAnimationSpeed(1f);
        zombie.SetWalkMotionSpeed(moveSpeed);
        zombie.Walk();
    }

    public override void Update()
    {
        if (zombie.IsDeadOrDying)
            return;

        GameObject target = attackTrigger.CurrentTargetPlant;

        if (IsValidTarget(target))
        {
            stateMachine.ChangeState(
                new ZombieAttackState(zombie, stateMachine, attackTrigger, moveSpeed)
            );
            return;
        }

        if (zombie.HasGroundMotion)
            return;

        zombie.transform.position += Vector3.left * moveSpeed * Time.deltaTime;
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
            return false;

        PlantHealth health = target.GetComponent<PlantHealth>();
        return health != null && health.IsAlive();
    }
}